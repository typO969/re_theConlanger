using System.Text;
using Conlanger.SDK;

namespace EvolutionSimPlugin;

public sealed class Plugin : IPlugin, IPipelineExtensionProvider
{
    private ILabContext? _ctx;

    public string Id => "conlanger.evolution-sim";
    public string Name => "Evolution Simulator";
    public Version Version => new(2, 0, 0);
    public IReadOnlyList<IPluginAction> Actions { get; }

    public Plugin()
    {
        Actions =
        [
            new GenerateSamplesAction(() => _ctx!),
            new SimulateDriftAction(() => _ctx!)
        ];
    }

    public void Initialize(ILabContext context) => _ctx = context;

    public IReadOnlyList<ISyntaxGenerator> GetSyntaxGenerators() => Array.Empty<ISyntaxGenerator>();
    public IReadOnlyList<IMorphologyRealizer> GetMorphologyRealizers() => Array.Empty<IMorphologyRealizer>();
    public IReadOnlyList<IOrthographyRenderer> GetOrthographyRenderers() => Array.Empty<IOrthographyRenderer>();

    public IReadOnlyList<IPhonoRule> GetPhonoRules() => [new SpirantizationPluginRule()];

    private sealed class SpirantizationPluginRule : IPhonoRule
    {
        public string Id => "plugin-intervocalic-spirantization";
        public string Name => "Intervocalic Spirantization (Plugin)";
        public string Description => "Softens voiceless stops between vowels into fricatives (p->f, t->s, k->x / V__V).";
        public bool Enabled => true;

        public IReadOnlyList<string> Apply(IReadOnlyList<string> wordPhones, LanguageProject project, IReadOnlySet<int>? stressedIndices = null)
        {
            var vowels = project.Phonemes.Where(p => p.Type == "V").Select(p => p.Symbol).ToHashSet(StringComparer.Ordinal);
            var list = wordPhones.ToList();
            for (int i = 1; i < list.Count - 1; i++)
            {
                if (!vowels.Contains(list[i - 1]) || !vowels.Contains(list[i + 1])) continue;
                list[i] = list[i] switch
                {
                    "p" => "f",
                    "t" => "s",
                    "k" => "x",
                    _ => list[i]
                };
            }
            return list;
        }
    }

    private sealed class GenerateSamplesAction(Func<ILabContext> ctxAccessor) : IPluginAction
    {
        public string ActionId => "generate-samples";
        public string Title => "Generate sample words";
        public string Category => "Generate";
        public string Description => "Generates phonotactically legal sample words from the unified LanguageProject.";
        public IReadOnlyList<PluginArgSpec> Args =>
        [
            new("count", "Count", PluginArgType.Number, 10, null, true, "Number of samples to generate."),
            new("syllables", "Syllables", PluginArgType.Number, 2, null, true, "Syllables per word."),
            new("fileName", "File name", PluginArgType.Text, "samples.txt", null, true, "Output file name for samples."),
            new("format", "Format", PluginArgType.Select, "txt", ["txt", "csv"], false, "Output format.")
        ];

        public Task<PluginResult> RunAsync(LanguageProject project, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
        {
            int count = args.TryGetValue("count", out var cObj) && int.TryParse(cObj?.ToString(), out var c) ? Math.Clamp(c, 1, 500) : 10;
            int syllables = args.TryGetValue("syllables", out var sObj) && int.TryParse(sObj?.ToString(), out var s) ? Math.Clamp(s, 1, 8) : 2;
            string fileName = args.TryGetValue("fileName", out var fObj) && !string.IsNullOrWhiteSpace(fObj?.ToString()) ? fObj.ToString()! : "samples.txt";
            string format = args.TryGetValue("format", out var fmtObj) ? (fmtObj?.ToString() ?? "txt").ToLowerInvariant() : "txt";

            var ctx = ctxAccessor();
            var words = ctx.GenerateSampleWords(project, count, syllables);
            if (words.Count == 0)
                return Task.FromResult(new PluginResult(false, "Inventory has no phonemes to generate words."));

            string content = format == "csv" ? string.Join(",", words) : string.Join(Environment.NewLine, words);
            using var stream = ctx.CreateOutput(fileName);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(content);

            return Task.FromResult(new PluginResult(true, $"Generated {words.Count} samples to {fileName}"));
        }
    }

    private sealed class SimulateDriftAction(Func<ILabContext> ctxAccessor) : IPluginAction
    {
        public string ActionId => "simulate-drift";
        public string Title => "Simulate diachronic drift";
        public string Category => "Simulate";
        public string Description => "Evolves lexicon URs through N generations of enabled phonology rules.";
        public IReadOnlyList<PluginArgSpec> Args =>
        [
            new("generations", "Generations", PluginArgType.Number, 2, null, true, "Number of sound-change passes."),
            new("sampleCount", "Sample Stems", PluginArgType.Number, 15, null, false, "Sample generated roots to include.")
        ];

        public Task<PluginResult> RunAsync(LanguageProject project, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
        {
            int generations = args.TryGetValue("generations", out var gObj) && int.TryParse(gObj?.ToString(), out var g) ? Math.Clamp(g, 1, 20) : 2;
            int samples = args.TryGetValue("sampleCount", out var sObj) && int.TryParse(sObj?.ToString(), out var s) ? Math.Clamp(s, 1, 100) : 15;

            var ctx = ctxAccessor();
            var sb = new StringBuilder();
            sb.AppendLine($"Diachronic Evolution Report for {project.Name} ({generations} generations)");
            sb.AppendLine(new string('=', 60));

            foreach (var lex in project.Lexemes)
            {
                ct.ThrowIfCancellationRequested();
                var phones = lex.UR.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                var stages = new List<string> { string.Join("", phones) };
                for (int gen = 0; gen < generations; gen++)
                {
                    foreach (var rule in project.PhonologyRules.Where(r => r.Enabled))
                    {
                        for (int i = 0; i < phones.Count; i++)
                        {
                            if (phones[i] == rule.Target)
                                phones[i] = rule.Replacement is "∅" or "" ? "" : rule.Replacement;
                        }
                        phones = phones.Where(p => !string.IsNullOrEmpty(p)).ToList();
                    }
                    stages.Add(string.Join("", phones));
                }
                sb.AppendLine($"{lex.Lemma} ({lex.Pos}): {string.Join(" -> ", stages)}");
            }

            var generated = ctx.GenerateSampleWords(project, samples, 2);
            sb.AppendLine();
            sb.AppendLine("Generated Sample Roots:");
            foreach (var w in generated) sb.AppendLine($"  * {w}");

            using var stream = ctx.CreateOutput($"{project.Name}-evolution.txt");
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(sb.ToString());

            ctx.Log($"Evolution simulation wrote {generations} generations.");
            return Task.FromResult(new PluginResult(true, $"Simulated {generations} generations for {project.Lexemes.Count} lexemes and {generated.Count} roots."));
        }
    }
}
