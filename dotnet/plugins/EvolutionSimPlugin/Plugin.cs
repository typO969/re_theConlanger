using System.Text;
using Conlanger.SDK;

namespace EvolutionSimPlugin;

public sealed class Plugin : IPlugin
{
    private ILabContext? _ctx;

    public string Id => "conlanger.evolution-sim";
    public string Name => "Diachronic Evolution Simulator";
    public Version Version => new(2, 0, 0);
    public IReadOnlyList<IPluginAction> Actions { get; }

    public Plugin()
    {
        Actions = [new SimulateDriftAction(() => _ctx!)];
    }

    public void Initialize(ILabContext context) => _ctx = context;

    private sealed class SimulateDriftAction(Func<ILabContext> ctxAccessor) : IPluginAction
    {
        public string ActionId => "simulate-drift";
        public string Title => "Simulate Diachronic Drift";
        public string Description => "Evolves lexicon URs and sample generated stems through N generations of enabled phonology rules.";
        public IReadOnlyList<PluginArgSpec> Args =>
        [
            new("generations", "Generations", PluginArgType.Number, 2),
            new("sampleCount", "Sample Stems", PluginArgType.Number, 15)
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
