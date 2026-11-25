using System.Text;
using Conlanger.SDK;

namespace GraphvizPlugin;

public sealed class Plugin : IPlugin, IPipelineExtensionProvider
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

    public IReadOnlyList<ISyntaxGenerator> GetSyntaxGenerators() => Array.Empty<ISyntaxGenerator>();
    public IReadOnlyList<IMorphologyRealizer> GetMorphologyRealizers() => Array.Empty<IMorphologyRealizer>();
    public IReadOnlyList<IPhonoRule> GetPhonoRules() => Array.Empty<IPhonoRule>();
    public IReadOnlyList<IOrthographyRenderer> GetOrthographyRenderers() => [new SegmentedGlossOrthography()];

    private sealed class SegmentedGlossOrthography : IOrthographyRenderer
    {
        public string Id => "plugin-segmented-orthography";
        public string Name => "Segmented Phone-Dot Orthography (Plugin)";
        public string Description => "Joins surface phones within each word with interpuncts (·) for morphological clarity.";

        public OrthographyResult Render(IReadOnlyList<IReadOnlyList<string>> surfaceWords, LanguageProject project)
        {
            var words = new List<string>(surfaceWords.Count);
            var tokens = new List<GraphemeToken>();
            for (int w = 0; w < surfaceWords.Count; w++)
            {
                var phones = surfaceWords[w];
                words.Add(string.Join("·", phones));
                for (int i = 0; i < phones.Count; i++)
                {
                    string pos = phones.Count == 1 ? "isolated" : i == 0 ? "initial" : i == phones.Count - 1 ? "final" : "medial";
                    tokens.Add(new GraphemeToken(phones[i].ToLowerInvariant(), phones[i], w, pos));
                }
            }
            return new OrthographyResult(string.Join(" ", words), tokens);
        }
    }

    private sealed class ExportDotAction(Func<ILabContext> ctxAccessor) : IPluginAction
    {
        public string ActionId => "export-dot";
        public string Title => "Export sound rules as Graphviz";
        public string Category => "Export";
        public string Description => "Writes a Graphviz .dot pipeline showing ordered phonology rewrite rules.";
        public IReadOnlyList<PluginArgSpec> Args =>
        [
            new("fileName", "File name", PluginArgType.Text, "sound-rules.dot", null, true, "Output file name for the DOT graph.")
        ];

        public Task<PluginResult> RunAsync(LanguageProject project, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
        {
            string fileName = args.TryGetValue("fileName", out var fn) && !string.IsNullOrWhiteSpace(fn?.ToString()) ? fn.ToString()! : "sound-rules.dot";
            var sb = new StringBuilder();
            sb.AppendLine("digraph Rules { rankdir=LR; node [shape=box];");
            sb.AppendLine("  INPUT [shape=oval];");
            string prev = "INPUT";
            for (int idx = 0; idx < project.PhonologyRules.Count; idx++)
            {
                var r = project.PhonologyRules[idx];
                string id = $"R{idx}";
                string env = $"{r.PrevPhone?.Trim()}_{r.NextPhone?.Trim()}";
                string cond = r.Condition?.Trim() ?? "";
                string lbl = $"{(string.IsNullOrEmpty(r.Target) ? "∅" : r.Target)}→{(string.IsNullOrEmpty(r.Replacement) ? "∅" : r.Replacement)}\\n/{(env == "_" ? "" : env)}\\n{cond}".Trim();
                sb.AppendLine($"  {id} [label=\"{lbl}\"{(r.Enabled ? "" : ", style=dashed")}]; {prev} -> {id};");
                prev = id;
            }
            sb.AppendLine($"  {prev} -> OUTPUT; OUTPUT [shape=oval];");
            sb.AppendLine("}");

            var ctx = ctxAccessor();
            using var s = ctx.CreateOutput(fileName);
            using var w = new StreamWriter(s, Encoding.UTF8);
            w.Write(sb.ToString());
            return Task.FromResult(new PluginResult(true, $"Exported {fileName}"));
        }
    }

    private sealed class ExportFeaturesCsvAction(Func<ILabContext> ctxAccessor) : IPluginAction
    {
        public string ActionId => "export-features-csv";
        public string Title => "Export phoneme features as CSV";
        public string Category => "Export";
        public string Description => "Exports the distinctive phonological feature matrix CSV.";
        public IReadOnlyList<PluginArgSpec> Args =>
        [
            new("fileName", "File name", PluginArgType.Text, "phoneme-features.csv", null, true, "Output file name for the feature CSV.")
        ];

        public Task<PluginResult> RunAsync(LanguageProject project, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
        {
            string fileName = args.TryGetValue("fileName", out var fn) && !string.IsNullOrWhiteSpace(fn?.ToString()) ? fn.ToString()! : "phoneme-features.csv";
            var sb = new StringBuilder();
            sb.AppendLine("Symbol,Type,Weight,AllowOnset,AllowCoda,Voicing,Manner,Place,Height,Backness,Roundness,Custom");
            foreach (var p in project.Phonemes)
            {
                var f = p.Features;
                sb.AppendLine($"\"{p.Symbol}\",\"{p.Type}\",{p.Weight},{p.AllowOnset},{p.AllowCoda},\"{f.Voicing}\",\"{f.Manner}\",\"{f.Place}\",\"{f.Height}\",\"{f.Backness}\",\"{f.Roundness}\",\"{f.Custom}\"");
            }

            var ctx = ctxAccessor();
            using var s = ctx.CreateOutput(fileName);
            using var w = new StreamWriter(s, Encoding.UTF8);
            w.Write(sb.ToString());
            return Task.FromResult(new PluginResult(true, $"Exported {fileName}"));
        }
    }
}
