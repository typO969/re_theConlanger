using System.IO;
using System.Text.Json;
using Conlanger.Core;
using Conlanger.SDK;

namespace Conlanger.Desktop.Services;

public sealed class ProjectHistory
{
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void RecordSnapshot(LanguageProject project)
    {
        _undo.Push(ProjectFactory.ToJson(project));
        _redo.Clear();
    }

    public LanguageProject? Undo(LanguageProject current)
    {
        if (_undo.Count == 0) return null;
        _redo.Push(ProjectFactory.ToJson(current));
        return ProjectFactory.FromJson(_undo.Pop());
    }

    public LanguageProject? Redo(LanguageProject current)
    {
        if (_redo.Count == 0) return null;
        _undo.Push(ProjectFactory.ToJson(current));
        return ProjectFactory.FromJson(_redo.Pop());
    }
}

public static class ProjectPersistence
{
    private static string AppDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Conlanger"
    );

    private static string AutosavePath => Path.Combine(AppDir, "autosave.clproj.json");

    public static void SaveAutosave(LanguageProject project)
    {
        Directory.CreateDirectory(AppDir);
        File.WriteAllText(AutosavePath, ProjectFactory.ToJson(project));
    }

    public static LanguageProject LoadAutosaveOrStarter()
    {
        try
        {
            if (File.Exists(AutosavePath))
                return ProjectFactory.FromJson(File.ReadAllText(AutosavePath));
        }
        catch
        {
            // Fall back cleanly if autosave is corrupt
        }
        return ProjectFactory.CreateStarterProject();
    }
}

public sealed record DiscoveredPlugin(PluginManifest Manifest, string DllPath);

public sealed class PluginService
{
    public IReadOnlyList<DiscoveredPlugin> DiscoverPlugins()
    {
        var results = new List<DiscoveredPlugin>();
        var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginsRoot)) return results;

        foreach (var manifestFile in Directory.EnumerateFiles(pluginsRoot, "*.plugin.json", SearchOption.AllDirectories))
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestFile), ProjectFactory.JsonOptions);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.EntryAssembly)) continue;
                var dll = Path.Combine(Path.GetDirectoryName(manifestFile)!, manifest.EntryAssembly);
                if (File.Exists(dll)) results.Add(new DiscoveredPlugin(manifest, dll));
            }
            catch
            {
                // Ignore malformed manifest
            }
        }
        return results;
    }
}
