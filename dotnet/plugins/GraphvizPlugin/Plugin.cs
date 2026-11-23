using System.Text;
using Conlanger.SDK;

namespace GraphvizPlugin;

public sealed class Plugin : IPlugin
{
    private ILabContext? _ctx;

    public string Id => "conlanger.graphviz";
    public string Name => "Graphviz & Feature Exporter";
    public Version Version => new(2, 0, 0);
    public IReadOnlyList<IPluginAction> Actions { get; }

    public Plugin()
    {
        Actions =
        [
            new ExportDotAction(() => _ctx!),
            new ExportFeaturesCsvAction(() => _ctx!)
        ];
    }

    public void Initialize(ILabContext context) => _ctx = context;

    private sealed class ExportDotAction(Func<ILabContext> ctxAccessor) : IPluginAction
    {
        public string ActionId => "export-dot";
        public string Title => "Export Syllable & Rule DOT Graph";
        public string Description => "Writes a Graphviz .dot graph showing templates, onsets, codas, and active phonology rules.";
        public IReadOnlyList<PluginArgSpec> Args => Array.Empty<PluginArgSpec>();

        public Task<PluginResult> RunAsync(LanguageProject project, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"digraph \"{project.Name}\" {{");
            sb.AppendLine("  rankdir=LR;");
            foreach (var tmpl in project.SyllableTemplates)
                sb.AppendLine($"  \"Syllable\" -> \"{tmpl.Pattern}\" [label=\"w={tmpl.Weight}\"];");
            foreach (var rule in project.PhonologyRules.Where(r => r.Enabled))
                sb.AppendLine($"  \"{rule.Target}\" -> \"{rule.Replacement}\" [label=\"{rule.Name}\"];");
            sb.AppendLine("}");

            var ctx = ctxAccessor();
            using var s = ctx.CreateOutput($"{project.Name}-phonology.dot");
            using var w = new StreamWriter(s, Encoding.UTF8);
            w.Write(sb.ToString());
            return Task.FromResult(new PluginResult(true, $"Exported DOT graph for {project.SyllableTemplates.Count} templates."));
        }
    }

    private sealed class ExportFeaturesCsvAction(Func<ILabContext> ctxAccessor) : IPluginAction
    {
        public string ActionId => "export-features-csv";
        public string Title => "Export Distinctive Features CSV";
        public string Description => "Exports the phonological feature matrix for every phoneme in the inventory.";
        public IReadOnlyList<PluginArgSpec> Args => Array.Empty<PluginArgSpec>();

        public Task<PluginResult> RunAsync(LanguageProject project, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Symbol,Type,Weight,AllowOnset,AllowCoda,Voicing,Manner,Place,Height,Backness,Roundness,Custom");
            foreach (var p in project.Phonemes)
            {
                var f = p.Features;
                sb.AppendLine($"{p.Symbol},{p.Type},{p.Weight},{p.AllowOnset},{p.AllowCoda},{f.Voicing},{f.Manner},{f.Place},{f.Height},{f.Backness},{f.Roundness},{f.Custom}");
            }

            var ctx = ctxAccessor();
            using var s = ctx.CreateOutput($"{project.Name}-features.csv");
            using var w = new StreamWriter(s, Encoding.UTF8);
            w.Write(sb.ToString());
            return Task.FromResult(new PluginResult(true, $"Exported {project.Phonemes.Count} phoneme feature rows."));
        }
    }
}
