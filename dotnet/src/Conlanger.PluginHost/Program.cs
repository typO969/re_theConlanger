using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Conlanger.Core;
using Conlanger.SDK;

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }
    return null;
}

static Dictionary<string, object?> ReadArgs(string path)
{
    var json = File.ReadAllText(path);
    var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
    var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    foreach (var pair in raw)
    {
        result[pair.Key] = pair.Value.ValueKind switch
        {
            JsonValueKind.Number when pair.Value.TryGetInt32(out var i) => i,
            JsonValueKind.Number when pair.Value.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => pair.Value.GetString(),
            JsonValueKind.Null => null,
            _ => pair.Value.ToString()
        };
    }
    return result;
}

var pluginPath = GetArg(args, "--plugin");
var actionId = GetArg(args, "--action");
var projectPath = GetArg(args, "--project") ?? GetArg(args, "--model");
var argsPath = GetArg(args, "--args");
var resultPath = GetArg(args, "--result");
var outputDir = GetArg(args, "--outputDir");
var timeoutMsRaw = GetArg(args, "--timeoutMs");

if (string.IsNullOrWhiteSpace(pluginPath) || string.IsNullOrWhiteSpace(actionId) ||
    string.IsNullOrWhiteSpace(projectPath) || string.IsNullOrWhiteSpace(argsPath) ||
    string.IsNullOrWhiteSpace(resultPath))
{
    Console.Error.WriteLine("Missing required arguments (--plugin, --action, --project, --args, --result).");
    return 1;
}

int timeoutMs = int.TryParse(timeoutMsRaw, out var parsed) ? parsed : 10_000;
string outputRoot = string.IsNullOrWhiteSpace(outputDir)
    ? Path.Combine(Path.GetTempPath(), "Conlanger", "plugin-host")
    : outputDir;
Directory.CreateDirectory(outputRoot);

PluginResult result;
try
{
    var project = ProjectFactory.FromJson(File.ReadAllText(projectPath));
    var argValues = ReadArgs(argsPath);

    var ctx = new HostLabContext(outputRoot, Console.WriteLine);
    var plugin = LoadPlugin(pluginPath, ctx);
    if (plugin is null)
    {
        result = new PluginResult(false, "No exported IPlugin implementation found in assembly.");
    }
    else
    {
        var action = plugin.Actions.FirstOrDefault(a => string.Equals(a.ActionId, actionId, StringComparison.OrdinalIgnoreCase));
        if (action is null)
        {
            result = new PluginResult(false, $"Action '{actionId}' not found.");
        }
        else
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            result = await action.RunAsync(project, argValues, cts.Token);
        }
    }
}
catch (Exception ex)
{
    result = new PluginResult(false, ex.Message);
}

File.WriteAllText(resultPath, JsonSerializer.Serialize(result, ProjectFactory.JsonOptions));
return result.Success ? 0 : 1;

static IPlugin? LoadPlugin(string pluginPath, ILabContext context)
{
    var alc = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true);
    using var fs = new FileStream(pluginPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    var asm = alc.LoadFromStream(fs);
    foreach (var t in asm.GetExportedTypes())
    {
        if (typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract)
        {
            if (Activator.CreateInstance(t) is IPlugin plugin)
            {
                plugin.Initialize(context);
                return plugin;
            }
        }
    }
    return null;
}

sealed class HostLabContext(string appDataDir, Action<string> log) : ILabContext
{
    public string AppDataDir { get; } = appDataDir;

    public void Log(string message) => log(message);

    public Stream CreateOutput(string suggestedFileName)
    {
        Directory.CreateDirectory(AppDataDir);
        string safe = string.IsNullOrWhiteSpace(suggestedFileName) ? "plugin-output.txt" : Path.GetFileName(suggestedFileName);
        string path = Path.Combine(AppDataDir, safe);
        log($"Output: {path}");
        return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    public IReadOnlyList<string> GenerateSampleWords(LanguageProject project, int count, int syllablesPerWord, int seed = 42) =>
        WordGenerator.GenerateWords(project, count, syllablesPerWord, seed);
}
