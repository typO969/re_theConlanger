using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Conlanger.SDK;

namespace Conlanger.Core;

public class ConfigurableSyntaxGenerator : ISyntaxGenerator
{
    public virtual string Id => "configurable-syntax";
    public virtual string Name => "Configurable Syntax (Default)";
    public virtual string Description => "Standard SVO/SOV/VSO clause planner with NP adjective expansion.";

    public virtual SyntaxTree Generate(SentenceSpec spec, LanguageProject project, Random rng)
    {
        var nouns = project.Lexemes.Where(l => string.Equals(l.Pos, "N", StringComparison.OrdinalIgnoreCase)).Select(l => l.Lemma).ToList();
        var adjs = project.Lexemes.Where(l => string.Equals(l.Pos, "ADJ", StringComparison.OrdinalIgnoreCase)).Select(l => l.Lemma).ToList();
        var verbs = project.Lexemes.Where(l => string.Equals(l.Pos, "V", StringComparison.OrdinalIgnoreCase)).Select(l => l.Lemma).ToList();
        var copulas = project.Lexemes.Where(l => string.Equals(l.Pos, "Cop", StringComparison.OrdinalIgnoreCase)).Select(l => l.Lemma).ToList();

        string Pick(IReadOnlyList<string> list, string fallback) =>
            list.Count == 0 ? fallback : list[rng.Next(list.Count)];

        string? Constraint(string key) =>
            spec.Constraints is not null && spec.Constraints.TryGetValue(key, out var val) ? val : null;

        SyntaxNode BuildNp(bool plural, bool includeAdj, string rolePrefix)
        {
            var noun = Pick(nouns, "person");
            var feats = new List<Feat> { new("Role", rolePrefix), new("Number", plural ? "Pl" : "Sing") };
            var nLeaf = new SyntaxNode("N", noun, feats);
            if (!includeAdj || adjs.Count == 0)
                return nLeaf;
            var adjLeaf = new SyntaxNode("ADJ", Pick(adjs, "small"), new[] { new Feat("Role", "Mod") });
            return new SyntaxNode("NP", $"{rolePrefix}NP", new[] { new Feat("Role", $"{rolePrefix}NP") }, new[] { adjLeaf, nLeaf });
        }

        string numSubjStr = Constraint("Number") ?? (rng.NextDouble() < 0.35 ? "Pl" : "Sing");
        string numObjStr = Constraint("Number") ?? (rng.NextDouble() < 0.35 ? "Pl" : "Sing");
        bool subjPl = string.Equals(numSubjStr, "Pl", StringComparison.OrdinalIgnoreCase);
        bool objPl = string.Equals(numObjStr, "Pl", StringComparison.OrdinalIgnoreCase);

        bool useAdjs = spec.ApproxWords >= 4 && adjs.Count > 0;
        var subj = BuildNp(subjPl, useAdjs, "Subj");
        string tenseVal = Constraint("Tense") ?? "Pres";
        var tenseFeats = new[] { new Feat("Role", string.Equals(spec.ClauseType, "Copular", StringComparison.OrdinalIgnoreCase) ? "Copula" : "Verb"), new Feat("Tense", tenseVal) };
        var verbLemma = string.Equals(spec.ClauseType, "Copular", StringComparison.OrdinalIgnoreCase)
            ? Pick(copulas, project.Lexemes.Any(l => l.Lemma == "be") ? "be" : "e")
            : Pick(verbs, "see");
        var vNode = new SyntaxNode("V", verbLemma, tenseFeats);

        if (string.Equals(spec.ClauseType, "Intransitive", StringComparison.OrdinalIgnoreCase))
        {
            var children = project.WordOrder is WordOrder.VSO or WordOrder.VOS
                ? new[] { vNode, subj }
                : new[] { subj, vNode };
            return new SyntaxTree(new SyntaxNode("CLAUSE", spec.ClauseType, null, children));
        }

        if (string.Equals(spec.ClauseType, "Copular", StringComparison.OrdinalIgnoreCase))
        {
            var pred = new SyntaxNode("ADJ", Pick(adjs, "small"), new[] { new Feat("Role", "Pred") });
            var children = project.WordOrder switch
            {
                WordOrder.SOV or WordOrder.OSV => new[] { subj, pred, vNode },
                WordOrder.VSO or WordOrder.VOS => new[] { vNode, subj, pred },
                _ => new[] { subj, vNode, pred }
            };
            return new SyntaxTree(new SyntaxNode("CLAUSE", spec.ClauseType, null, children));
        }

        var obj = BuildNp(objPl, spec.ApproxWords >= 5 && adjs.Count > 0, "Obj");
        var ordered = project.WordOrder switch
        {
            WordOrder.SOV => new[] { subj, obj, vNode },
            WordOrder.VSO => new[] { vNode, subj, obj },
            WordOrder.VOS => new[] { vNode, obj, subj },
            WordOrder.OVS => new[] { obj, vNode, subj },
            WordOrder.OSV => new[] { obj, subj, vNode },
            _ => new[] { subj, vNode, obj }
        };
        return new SyntaxTree(new SyntaxNode("CLAUSE", spec.ClauseType, null, ordered));
    }

    public static string FormatSyntaxTree(SyntaxNode node, int depth = 0, bool parenthesesRole = false)
    {
        var indent = new string(' ', depth * 2);
        var role = node.Features?.FirstOrDefault(f => f.Name == "Role")?.Value;
        string header = string.IsNullOrEmpty(role)
            ? $"{node.Category}:{node.Lemma}"
            : parenthesesRole
                ? $"{node.Category}:{node.Lemma} ({role})"
                : $"{node.Category}:{node.Lemma} [{role}]";

        var sb = new StringBuilder();
        sb.Append(indent).Append(header);
        if (node.Children is { Count: > 0 })
        {
            foreach (var child in node.Children)
                sb.AppendLine().Append(FormatSyntaxTree(child, depth + 1, parenthesesRole));
        }
        return sb.ToString();
    }
}

public sealed class ExpandedModifierSyntaxGenerator : ConfigurableSyntaxGenerator
{
    public override string Id => "rich-np-syntax";
    public override string Name => "Expanded Modifier Syntax (Phase 4)";
    public override string Description => "Always expands subject/object NPs with adjective modifiers regardless of word target.";

    public override SyntaxTree Generate(SentenceSpec spec, LanguageProject project, Random rng) =>
        base.Generate(spec with { ApproxWords = Math.Max(spec.ApproxWords, 5) }, project, rng);
}

public class MorphologyEngine : IMorphologyRealizer
{
    public virtual string Id => "standard-morphology";
    public virtual string Name => "Affixation Realizer (Default)";
    public virtual string Description => "Lexicon-backed stem plus ordered prefix/suffix rules.";

    public static List<string> SplitPhones(string source)
    {
        var trimmed = source.Trim();
        if (string.IsNullOrEmpty(trimmed)) return new List<string>();
        if (trimmed.Contains(' '))
            return trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
        return trimmed.Select(ch => ch.ToString()).ToList();
    }

    protected static List<string> ResolveStemPhones(string lemma, LanguageProject project)
    {
        var lex = project.Lexemes.FirstOrDefault(l => string.Equals(l.Lemma, lemma, StringComparison.Ordinal));
        return lex is not null && !string.IsNullOrWhiteSpace(lex.UR)
            ? SplitPhones(lex.UR)
            : SplitPhones(lemma);
    }

    public virtual List<string> InflectSingle(string lemma, string pos, IReadOnlyList<Feat>? features, LanguageProject project)
    {
        var wordPhones = ResolveStemPhones(lemma, project);
        if (features is not null)
        {
            foreach (var rule in project.Affixes.Where(a => a.Enabled))
            {
                bool posMatch = string.Equals(rule.Pos, pos, StringComparison.OrdinalIgnoreCase)
                    || (string.Equals(pos, "V", StringComparison.OrdinalIgnoreCase) && string.Equals(rule.Pos, "Cop", StringComparison.OrdinalIgnoreCase));
                if (!posMatch) continue;
                bool featMatch = features.Any(f =>
                    string.Equals(f.Name, rule.FeatureName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Value, rule.FeatureValue, StringComparison.OrdinalIgnoreCase));
                if (!featMatch) continue;

                var affixPhones = SplitPhones(rule.Affix.Replace("-", ""));
                if (rule.IsPrefix) wordPhones.InsertRange(0, affixPhones);
                else wordPhones.AddRange(affixPhones);
            }
        }
        return wordPhones;
    }

    public virtual IReadOnlyList<IReadOnlyList<string>> RealizeWords(SyntaxTree tree, LanguageProject project)
    {
        var words = new List<IReadOnlyList<string>>();
        foreach (var leaf in tree.Leaves())
        {
            if (string.IsNullOrWhiteSpace(leaf.Lemma)) continue;
            words.Add(InflectSingle(leaf.Lemma, leaf.Category, leaf.Features, project));
        }
        return words;
    }

    public virtual string RealizeForPreview(string lemma, string pos, IReadOnlyList<Feat> features, LanguageProject project) =>
        string.Join(" ", InflectSingle(lemma, pos, features, project));
}

public sealed class ReduplicativePluralMorphology : MorphologyEngine
{
    public override string Id => "reduplicative-plural";
    public override string Name => "CV Reduplication + Affixes (Phase 4)";
    public override string Description => "Prepends initial CV stem reduplication for Number=Pl alongside active affixes.";

    public override List<string> InflectSingle(string lemma, string pos, IReadOnlyList<Feat>? features, LanguageProject project)
    {
        var phones = base.InflectSingle(lemma, pos, features, project);
        if (string.Equals(pos, "N", StringComparison.OrdinalIgnoreCase) &&
            features is not null &&
            features.Any(f => string.Equals(f.Name, "Number", StringComparison.OrdinalIgnoreCase) && string.Equals(f.Value, "Pl", StringComparison.OrdinalIgnoreCase)))
        {
            var stem = ResolveStemPhones(lemma, project);
            if (stem.Count >= 2)
                phones.InsertRange(0, stem.Take(2));
        }
        return phones;
    }
}

public sealed class BuiltInPhonoRule(string id, string name, string description, Func<IReadOnlyList<string>, LanguageProject, IReadOnlySet<int>?, IReadOnlyList<string>> fn) : IPhonoRule
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;
    public bool Enabled { get; set; } = true;

    public IReadOnlyList<string> Apply(IReadOnlyList<string> wordPhones, LanguageProject project, IReadOnlySet<int>? stressedIndices = null) =>
        fn(wordPhones, project, stressedIndices);
}

public sealed class PipelineRegistry
{
    private readonly List<ISyntaxGenerator> _syntaxGenerators = new();
    private readonly List<IMorphologyRealizer> _morphologyRealizers = new();
    private readonly List<IOrthographyRenderer> _orthographyRenderers = new();
    private readonly List<IPhonoRule> _phonoRules = new();

    public IReadOnlyList<ISyntaxGenerator> SyntaxGenerators => _syntaxGenerators;
    public IReadOnlyList<IMorphologyRealizer> MorphologyRealizers => _morphologyRealizers;
    public IReadOnlyList<IOrthographyRenderer> OrthographyRenderers => _orthographyRenderers;
    public IReadOnlyList<IPhonoRule> PhonoRules => _phonoRules;

    public static PipelineRegistry CreateDefaultWithPlugins(string? pluginsDirectory = null)
    {
        var reg = new PipelineRegistry();
        reg._syntaxGenerators.Add(new ConfigurableSyntaxGenerator());
        reg._syntaxGenerators.Add(new ExpandedModifierSyntaxGenerator());

        reg._morphologyRealizers.Add(new MorphologyEngine());
        reg._morphologyRealizers.Add(new ReduplicativePluralMorphology());

        reg._orthographyRenderers.Add(new OrthographyEngine());
        reg._orthographyRenderers.Add(new IpaBroadOrthography());

        reg._phonoRules.Add(new BuiltInPhonoRule(
            "intervocalic-stop-lenition",
            "Intervocalic Stop Voicing",
            "Voiceless stops become voiced between vowels (p->b, t->d, k->g / V__V).",
            (phones, proj, stressed) =>
            {
                var cur = phones;
                foreach (var (s, d) in new[] { ("p", "b"), ("t", "d"), ("k", "g") })
                    cur = PhonologyEngine.ApplyRule(cur, new PhonologyRuleSpec { Name = "Lenition", Target = s, Replacement = d, PrevPhone = "V", NextPhone = "V", Enabled = true }, proj, stressed);
                return cur;
            }));

        reg._phonoRules.Add(new BuiltInPhonoRule(
            "final-obstruent-devoicing",
            "Word-Final Obstruent Devoicing",
            "Voiced obstruents devoice word-finally (b->p, d->t, g->k, z->s, v->f / __#).",
            (phones, proj, stressed) =>
            {
                var cur = phones;
                foreach (var (s, d) in new[] { ("b", "p"), ("d", "t"), ("g", "k"), ("z", "s"), ("v", "f") })
                    cur = PhonologyEngine.ApplyRule(cur, new PhonologyRuleSpec { Name = "FinalDevoicing", Target = s, Replacement = d, NextPhone = "#", Enabled = true }, proj, stressed);
                return cur;
            }));

        reg._phonoRules.Add(new BuiltInPhonoRule(
            "unstressed-vowel-reduction",
            "Unstressed Vowel Reduction (-> ə)",
            "Reduces unstressed vowels to ə when StressPattern is active ([-stressed]).",
            (phones, proj, stressed) =>
            {
                if (stressed is null || stressed.Count == 0) return phones;
                return PhonologyEngine.ApplyRule(phones, new PhonologyRuleSpec { Name = "SchwaReduction", Target = "V", Replacement = "ə", Condition = "[-stressed]", Enabled = true }, proj, stressed);
            }));

        if (!string.IsNullOrWhiteSpace(pluginsDirectory) && Directory.Exists(pluginsDirectory))
            reg.LoadExtensionsFromDirectory(pluginsDirectory);

        return reg;
    }

    public void LoadExtensionsFromDirectory(string pluginsDirectory)
    {
        foreach (var dll in Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                var alc = new AssemblyLoadContext($"ext-{Path.GetFileNameWithoutExtension(dll)}", isCollectible: true);
                using var fs = new FileStream(dll, FileMode.Open, FileAccess.Read, FileShare.Read);
                var asm = alc.LoadFromStream(fs);
                foreach (var t in asm.GetExportedTypes())
                {
                    if (typeof(IPipelineExtensionProvider).IsAssignableFrom(t) && !t.IsAbstract)
                    {
                        if (Activator.CreateInstance(t) is IPipelineExtensionProvider provider)
                        {
                            foreach (var sg in provider.GetSyntaxGenerators())
                                if (_syntaxGenerators.All(x => x.Id != sg.Id)) _syntaxGenerators.Add(sg);
                            foreach (var mr in provider.GetMorphologyRealizers())
                                if (_morphologyRealizers.All(x => x.Id != mr.Id)) _morphologyRealizers.Add(mr);
                            foreach (var or in provider.GetOrthographyRenderers())
                                if (_orthographyRenderers.All(x => x.Id != or.Id)) _orthographyRenderers.Add(or);
                            foreach (var pr in provider.GetPhonoRules())
                                if (_phonoRules.All(x => x.Id != pr.Id)) _phonoRules.Add(pr);
                        }
                    }
                }
            }
            catch
            {
                // Keep UI resilient if an external plugin assembly fails to load
            }
        }
    }

    public ISyntaxGenerator ResolveSyntax(string? id) =>
        _syntaxGenerators.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)) ?? _syntaxGenerators[0];

    public IMorphologyRealizer ResolveMorphology(string? id) =>
        _morphologyRealizers.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)) ?? _morphologyRealizers[0];

    public IOrthographyRenderer ResolveOrthography(string? id) =>
        _orthographyRenderers.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)) ?? _orthographyRenderers[0];

    public IReadOnlyList<IPhonoRule> ResolvePhonoRules(IEnumerable<string>? enabledIds)
    {
        if (enabledIds is null) return Array.Empty<IPhonoRule>();
        var set = enabledIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _phonoRules.Where(r => set.Contains(r.Id)).ToList();
    }
}
