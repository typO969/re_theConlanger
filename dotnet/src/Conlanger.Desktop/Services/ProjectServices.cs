using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Conlanger.Core;
using Conlanger.SDK;

namespace Conlanger.Desktop.Services;

public sealed class ProjectHistory
{
    private const int MaxDepth = 60;
    private readonly List<string> _undo = new();
    private readonly List<string> _redo = new();

    public bool CanUndo => _undo.Count > 1;
    public bool CanRedo => _redo.Count > 0;

    public void PushSnapshot(LanguageProject project)
    {
        var json = ProjectFactory.ToJson(project);
        if (_undo.Count > 0 && _undo[^1] == json) return;
        _undo.Add(json);
        if (_undo.Count > MaxDepth) _undo.RemoveAt(0);
        _redo.Clear();
    }

    public LanguageProject? Undo(LanguageProject current)
    {
        if (_undo.Count <= 1) return null;
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(ProjectFactory.ToJson(current));
        return ProjectFactory.FromJson(_undo[^1]);
    }

    public LanguageProject? Redo()
    {
        if (_redo.Count == 0) return null;
        var nextJson = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(nextJson);
        return ProjectFactory.FromJson(nextJson);
    }
}

public sealed record RecentProjectRecord(string FilePath, string FileName, string ProjectName, DateTime UpdatedUtc);

public sealed record BackupSnapshotRecord(string Id, string FilePath, string Label, string CreatedAt);

public static class ProjectPersistence
{
    private static string AppDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Conlanger"
    );

    private static string AutosavePath => Path.Combine(AppDir, "autosave.clproj.json");
    private static string RecentPath => Path.Combine(AppDir, "recent-projects.json");
    private static string BackupsDir => Path.Combine(AppDir, "backups");

    public static async Task SaveAutosaveAsync(LanguageProject project, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(AppDir);
            await File.WriteAllTextAsync(AutosavePath, ProjectFactory.ToJson(project), ct).ConfigureAwait(false);
        }
        catch
        {
            // Silent UI persistence guard
        }
    }

    public static async Task<LanguageProject> LoadAutosaveOrStarterAsync(CancellationToken ct = default)
    {
        try
        {
            if (File.Exists(AutosavePath))
            {
                var json = await File.ReadAllTextAsync(AutosavePath, ct).ConfigureAwait(false);
                return ProjectFactory.FromJson(json);
            }
        }
        catch
        {
            // Fall back cleanly if autosave is corrupt
        }
        return ProjectFactory.CreateStarterProject();
    }

    public static async Task<List<RecentProjectRecord>> LoadRecentProjectsAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(RecentPath)) return new();
            var json = await File.ReadAllTextAsync(RecentPath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<RecentProjectRecord>>(json, ProjectFactory.JsonOptions) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public static async Task<List<RecentProjectRecord>> RecordRecentProjectAsync(string filePath, LanguageProject project, CancellationToken ct = default)
    {
        var list = await LoadRecentProjectsAsync(ct).ConfigureAwait(false);
        list.RemoveAll(r => string.Equals(r.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, new RecentProjectRecord(filePath, Path.GetFileName(filePath), project.Name, DateTime.UtcNow));
        if (list.Count > 10) list = list.Take(10).ToList();
        try
        {
            Directory.CreateDirectory(AppDir);
            await File.WriteAllTextAsync(RecentPath, JsonSerializer.Serialize(list, ProjectFactory.JsonOptions), ct).ConfigureAwait(false);
        }
        catch
        {
            // Ignore disk write issues
        }
        return list;
    }

    public static async Task<List<BackupSnapshotRecord>> SaveVersionedBackupAsync(LanguageProject project, string reason, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(BackupsDir);
            string safe = string.Concat((project.Name ?? "project").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{safe}_{reason}.clproj.json";
            string fullPath = Path.Combine(BackupsDir, fileName);
            await File.WriteAllTextAsync(fullPath, ProjectFactory.ToJson(project), ct).ConfigureAwait(false);

            var all = Directory.EnumerateFiles(BackupsDir, "*.clproj.json")
                .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var stale in all.Skip(15))
            {
                try { File.Delete(stale); } catch { }
            }
        }
        catch
        {
            // Silent UI backup guard
        }
        return LoadBackupSnapshots();
    }

    public static List<BackupSnapshotRecord> LoadBackupSnapshots()
    {
        try
        {
            if (!Directory.Exists(BackupsDir)) return new();
            return Directory.EnumerateFiles(BackupsDir, "*.clproj.json")
                .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                .Take(15)
                .Select(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f));
                    var ts = File.GetLastWriteTime(f).ToString("HH:mm:ss");
                    return new BackupSnapshotRecord(f, f, name, ts);
                })
                .ToList();
        }
        catch
        {
            return new();
        }
    }

    public static string ExportLexiconCsv(IEnumerable<LexemeEntry> lexemes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Lemma,Pos,UR");
        foreach (var l in lexemes)
            sb.AppendLine($"{l.Lemma},{l.Pos},{l.UR}");
        return sb.ToString();
    }

    public static string ExportLexiconJson(IEnumerable<LexemeEntry> lexemes) =>
        JsonSerializer.Serialize(lexemes.ToList(), ProjectFactory.JsonOptions);

    public static List<LexemeEntry> ImportLexiconFromText(string fileName, string content)
    {
        try
        {
            if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = JsonSerializer.Deserialize<List<LexemeEntry>>(content, ProjectFactory.JsonOptions) ?? new();
                return parsed.Where(l => !string.IsNullOrWhiteSpace(l.Lemma)).ToList();
            }

            return content.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Select(line => line.Split(','))
                .Where(parts => parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[0]))
                .Select(parts => new LexemeEntry
                {
                    Lemma = parts[0].Trim(),
                    Pos = string.IsNullOrWhiteSpace(parts[1]) ? "N" : parts[1].Trim(),
                    UR = parts[2].Trim()
                })
                .ToList();
        }
        catch
        {
            return new();
        }
    }
}

public sealed record DiscoveredAction(
    string PluginId,
    string PluginName,
    string ActionId,
    string Title,
    string Category,
    string Description,
    IReadOnlyList<PluginArgSpec> Args
);

public sealed record DiscoveredPlugin(
    PluginManifest Manifest,
    string DllPath,
    IReadOnlyList<DiscoveredAction> Actions
);

public sealed record ExecutedPluginOutput(
    string FileName,
    string Content,
    string CreatedAt,
    string PluginName
);

public sealed class PluginService
{
    public static IReadOnlyList<string> GetPluginSearchPaths()
    {
        var paths = new List<string>();
        var localPlugins = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (Directory.Exists(localPlugins)) paths.Add(localPlugins);

        // Allow automatic discovery when launching from Visual Studio bin/Debug/net10.0-windows/
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir.FullName, "plugins");
            if (Directory.Exists(candidate) && !paths.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                paths.Add(candidate);
            dir = dir.Parent;
        }
        return paths;
    }

    public IReadOnlyList<DiscoveredPlugin> DiscoverPlugins()
    {
        var results = new List<DiscoveredPlugin>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetPluginSearchPaths())
        {
            foreach (var manifestFile in Directory.EnumerateFiles(root, "*.plugin.json", SearchOption.AllDirectories))
            {
                try
                {
                    var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestFile), ProjectFactory.JsonOptions);
                    if (manifest is null || string.IsNullOrWhiteSpace(manifest.EntryAssembly)) continue;
                    if (!seenIds.Add(manifest.Id)) continue;

                    string manifestDir = Path.GetDirectoryName(manifestFile)!;
                    string? dll = LocateCompiledPluginDll(manifestDir, manifest.EntryAssembly);
                    if (dll is null) continue;

                    var actions = InspectPluginActions(manifest, dll);
                    results.Add(new DiscoveredPlugin(manifest, dll, actions));
                }
                catch
                {
                    // Ignore malformed or unloaded plugin assemblies silently
                }
            }
        }
        return results;
    }

    private static string? LocateCompiledPluginDll(string manifestDir, string entryAssembly)
    {
        var direct = Path.Combine(manifestDir, entryAssembly);
        if (File.Exists(direct)) return direct;

        var binDir = Path.Combine(manifestDir, "bin");
        if (!Directory.Exists(binDir)) return null;
        return Directory.EnumerateFiles(binDir, entryAssembly, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static IReadOnlyList<DiscoveredAction> InspectPluginActions(PluginManifest manifest, string dllPath)
    {
        var alc = new AssemblyLoadContext($"inspect-{manifest.Id}", isCollectible: true);
        try
        {
            using var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var asm = alc.LoadFromStream(fs);
            foreach (var t in asm.GetExportedTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract)
                {
                    if (Activator.CreateInstance(t) is IPlugin plugin)
                    {
                        return plugin.Actions
                            .Select(a => new DiscoveredAction(manifest.Id, manifest.Name, a.ActionId, a.Title, a.Category, a.Description, a.Args))
                            .ToList();
                    }
                }
            }
        }
        catch
        {
            // Ignore inspection errors
        }
        finally
        {
            alc.Unload();
        }
        return Array.Empty<DiscoveredAction>();
    }

    public static async Task<(PluginResult Result, IReadOnlyList<ExecutedPluginOutput> Files, string LogLine)> ExecutePluginActionAsync(
        DiscoveredPlugin plugin,
        DiscoveredAction action,
        LanguageProject project,
        IReadOnlyDictionary<string, object?> args,
        CancellationToken ct = default)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "Conlanger", "plugin-runs", Guid.NewGuid().ToString("N"));
        string outDir = Path.Combine(tempRoot, "out");
        Directory.CreateDirectory(outDir);

        try
        {
            string? hostExe = FindPluginHostExecutable();
            if (hostExe is not null)
            {
                string projPath = Path.Combine(tempRoot, "project.json");
                string argsPath = Path.Combine(tempRoot, "args.json");
                string resultPath = Path.Combine(tempRoot, "result.json");

                await File.WriteAllTextAsync(projPath, ProjectFactory.ToJson(project), ct).ConfigureAwait(false);
                await File.WriteAllTextAsync(argsPath, JsonSerializer.Serialize(args, ProjectFactory.JsonOptions), ct).ConfigureAwait(false);

                var psi = new ProcessStartInfo
                {
                    FileName = hostExe,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add("--plugin"); psi.ArgumentList.Add(plugin.DllPath);
                psi.ArgumentList.Add("--action"); psi.ArgumentList.Add(action.ActionId);
                psi.ArgumentList.Add("--project"); psi.ArgumentList.Add(projPath);
                psi.ArgumentList.Add("--args"); psi.ArgumentList.Add(argsPath);
                psi.ArgumentList.Add("--result"); psi.ArgumentList.Add(resultPath);
                psi.ArgumentList.Add("--outputDir"); psi.ArgumentList.Add(outDir);
                psi.ArgumentList.Add("--timeoutMs"); psi.ArgumentList.Add("10000");

                using var proc = Process.Start(psi);
                if (proc is not null)
                {
                    await proc.WaitForExitAsync(ct).ConfigureAwait(false);
                    if (File.Exists(resultPath))
                    {
                        var resJson = await File.ReadAllTextAsync(resultPath, ct).ConfigureAwait(false);
                        var res = JsonSerializer.Deserialize<PluginResult>(resJson, ProjectFactory.JsonOptions)
                            ?? new PluginResult(false, "Malformed PluginHost output.");
                        var files = await ReadOutputsAsync(outDir, plugin.Manifest.Name, ct).ConfigureAwait(false);
                        return (res, files, $"[OutOfProcess] {plugin.Manifest.Name}: {res.Message}");
                    }
                }
            }

            // Fallback: collectible in-process AssemblyLoadContext when Conlanger.PluginHost.exe isn't beside Desktop
            var inProcRes = await RunInCollectibleAlcAsync(plugin, action.ActionId, project, args, outDir, ct).ConfigureAwait(false);
            var inProcFiles = await ReadOutputsAsync(outDir, plugin.Manifest.Name, ct).ConfigureAwait(false);
            return (inProcRes, inProcFiles, $"[CollectibleALC] {plugin.Manifest.Name}: {inProcRes.Message}");
        }
        catch (Exception ex)
        {
            return (new PluginResult(false, ex.Message), Array.Empty<ExecutedPluginOutput>(), $"[Error] {plugin.Manifest.Name}: {ex.Message}");
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    private static string? FindPluginHostExecutable()
    {
        var adjacent = Path.Combine(AppContext.BaseDirectory, "Conlanger.PluginHost.exe");
        if (File.Exists(adjacent)) return adjacent;
        var adjacentDll = Path.Combine(AppContext.BaseDirectory, "Conlanger.PluginHost.dll");
        if (File.Exists(adjacentDll) && File.Exists(Path.ChangeExtension(adjacentDll, ".exe")))
            return Path.ChangeExtension(adjacentDll, ".exe");
        return null;
    }

    private static async Task<PluginResult> RunInCollectibleAlcAsync(
        DiscoveredPlugin pluginMeta,
        string actionId,
        LanguageProject project,
        IReadOnlyDictionary<string, object?> args,
        string outDir,
        CancellationToken ct)
    {
        var alc = new AssemblyLoadContext($"run-{pluginMeta.Manifest.Id}", isCollectible: true);
        try
        {
            using var fs = new FileStream(pluginMeta.DllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var asm = alc.LoadFromStream(fs);
            foreach (var t in asm.GetExportedTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract)
                {
                    if (Activator.CreateInstance(t) is IPlugin instance)
                    {
                        var ctx = new DesktopLabContext(outDir);
                        instance.Initialize(ctx);
                        var act = instance.Actions.FirstOrDefault(a => string.Equals(a.ActionId, actionId, StringComparison.OrdinalIgnoreCase));
                        if (act is null) return new PluginResult(false, $"Action '{actionId}' not found.");
                        return await act.RunAsync(project, args, ct).ConfigureAwait(false);
                    }
                }
            }
            return new PluginResult(false, "No IPlugin found.");
        }
        finally
        {
            alc.Unload();
        }
    }

    private static async Task<IReadOnlyList<ExecutedPluginOutput>> ReadOutputsAsync(string outDir, string pluginName, CancellationToken ct)
    {
        var list = new List<ExecutedPluginOutput>();
        if (!Directory.Exists(outDir)) return list;
        foreach (var file in Directory.EnumerateFiles(outDir))
        {
            var text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            list.Add(new ExecutedPluginOutput(Path.GetFileName(file), text, DateTime.Now.ToLongTimeString(), pluginName));
        }
        return list;
    }

    private sealed class DesktopLabContext(string outputDir) : ILabContext
    {
        public string AppDataDir { get; } = outputDir;
        public void Log(string message) { }
        public Stream CreateOutput(string suggestedFileName)
        {
            Directory.CreateDirectory(AppDataDir);
            string safe = string.IsNullOrWhiteSpace(suggestedFileName) ? "plugin-output.txt" : Path.GetFileName(suggestedFileName);
            return new FileStream(Path.Combine(AppDataDir, safe), FileMode.Create, FileAccess.Write, FileShare.Read);
        }

        public IReadOnlyList<string> GenerateSampleWords(LanguageProject project, int count, int syllablesPerWord, int seed = 42) =>
            WordGenerator.GenerateWords(project, count, syllablesPerWord, seed);
    }
}
