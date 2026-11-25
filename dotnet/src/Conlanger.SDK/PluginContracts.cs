namespace Conlanger.SDK;

public enum PluginArgType { Text, Number, Bool, Select }

public sealed record PluginArgSpec(
    string Key,
    string Label,
    PluginArgType Type,
    object? DefaultValue = null,
    IReadOnlyList<string>? Choices = null,
    bool Required = false,
    string Description = ""
);

public sealed record PluginResult(bool Success, string Message);

public sealed class PluginManifest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "2.0.0";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Website { get; set; } = "";
    public string IsolationMode { get; set; } = "OutOfProcess";
    public List<string> Capabilities { get; set; } = new();
    public string EntryAssembly { get; set; } = "";
    public string PluginTypeName { get; set; } = "";
}

public interface ILabContext
{
    string AppDataDir { get; }
    void Log(string message);
    Stream CreateOutput(string suggestedFileName);
    IReadOnlyList<string> GenerateSampleWords(LanguageProject project, int count, int syllablesPerWord, int seed = 42);
}

public interface IPluginAction
{
    string ActionId { get; }
    string Title { get; }
    string Category { get; }
    string Description { get; }
    IReadOnlyList<PluginArgSpec> Args { get; }
    Task<PluginResult> RunAsync(LanguageProject project, IReadOnlyDictionary<string, object?> args, CancellationToken ct);
}

public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    Version Version { get; }
    IReadOnlyList<IPluginAction> Actions { get; }
    void Initialize(ILabContext context);
}
