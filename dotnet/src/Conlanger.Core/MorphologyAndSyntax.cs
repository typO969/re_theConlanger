using Conlanger.SDK;

namespace Conlanger.Core;

public sealed class ConfigurableSyntaxGenerator : ISyntaxGenerator
{
    public string Id => "configurable-syntax";

    public SyntaxTree Generate(SentenceSpec spec, LanguageProject project, Random rng)
    {
        var nouns = project.Lexemes.Where(l => l.Pos == "N").Select(l => l.Lemma).ToList();
        var adjs = project.Lexemes.Where(l => l.Pos == "Adj").Select(l => l.Lemma).ToList();
        var verbs = project.Lexemes.Where(l => l.Pos == "V").Select(l => l.Lemma).ToList();
        var copulas = project.Lexemes.Where(l => l.Pos == "Cop").Select(l => l.Lemma).ToList();

        string Pick(IReadOnlyList<string> list, string fallback) =>
            list.Count == 0 ? fallback : list[rng.Next(list.Count)];

        SyntaxNode BuildNp(bool plural, bool includeAdj)
        {
            var noun = Pick(nouns, "cat");
            var feats = plural ? new[] { new Feat("num", "PL") } : Array.Empty<Feat>();
            var nLeaf = new SyntaxNode("N", noun, feats);
            if (!includeAdj || adjs.Count == 0)
                return new SyntaxNode("NP", null, null, new[] { nLeaf });
            var adjLeaf = new SyntaxNode("Adj", Pick(adjs, "big"));
            return new SyntaxNode("NP", null, null, new[] { adjLeaf, nLeaf });
        }

        bool useAdjs = spec.ApproxWords >= 4 && adjs.Count > 0;
        var subj = BuildNp(rng.NextDouble() > 0.65, useAdjs);
        var tenseFeats = rng.NextDouble() > 0.5 ? new[] { new Feat("tense", "PST") } : Array.Empty<Feat>();
        var verbLemma = string.Equals(spec.ClauseType, "Copular", StringComparison.OrdinalIgnoreCase)
            ? Pick(copulas, Pick(verbs, "be"))
            : Pick(verbs, "see");
        var vNode = new SyntaxNode("V", verbLemma, tenseFeats);

        if (string.Equals(spec.ClauseType, "Intransitive", StringComparison.OrdinalIgnoreCase))
        {
            var vp = new SyntaxNode("VP", null, null, new[] { vNode });
            return project.WordOrder switch
            {
                WordOrder.VSO or WordOrder.VOS => new SyntaxTree(new SyntaxNode("S", null, null, new[] { vp, subj })),
                _ => new SyntaxTree(new SyntaxNode("S", null, null, new[] { subj, vp }))
            };
        }

        if (string.Equals(spec.ClauseType, "Copular", StringComparison.OrdinalIgnoreCase))
        {
            SyntaxNode pred = adjs.Count > 0 && rng.NextDouble() > 0.45
                ? new SyntaxNode("AP", null, null, new[] { new SyntaxNode("Adj", Pick(adjs, "big")) })
                : BuildNp(false, false);

            var children = project.WordOrder switch
            {
                WordOrder.SOV or WordOrder.OSV => new[] { subj, pred, vNode },
                WordOrder.VSO or WordOrder.VOS => new[] { vNode, subj, pred },
                _ => new[] { subj, vNode, pred }
            };
            return new SyntaxTree(new SyntaxNode("S", null, null, children));
        }

        // Transitive
        var obj = BuildNp(rng.NextDouble() > 0.55, spec.ApproxWords >= 5 && adjs.Count > 0);
        var ordered = project.WordOrder switch
        {
            WordOrder.SOV => new[] { subj, obj, vNode },
            WordOrder.VSO => new[] { vNode, subj, obj },
            WordOrder.VOS => new[] { vNode, obj, subj },
            WordOrder.OVS => new[] { obj, vNode, subj },
            WordOrder.OSV => new[] { obj, subj, vNode },
            _ => new[] { subj, vNode, obj }
        };
        return new SyntaxTree(new SyntaxNode("S", null, null, ordered));
    }
}

public sealed class MorphologyEngine : IMorphologyRealizer
{
    public IReadOnlyList<IReadOnlyList<string>> RealizeWords(SyntaxTree tree, LanguageProject project)
    {
        var words = new List<IReadOnlyList<string>>();
        foreach (var leaf in tree.Leaves())
        {
            if (string.IsNullOrWhiteSpace(leaf.Lemma)) continue;
            var lex = project.Lexemes.FirstOrDefault(l => string.Equals(l.Lemma, leaf.Lemma, StringComparison.Ordinal));
            var basePhones = lex is not null && !string.IsNullOrWhiteSpace(lex.UR)
                ? lex.UR.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
                : leaf.Lemma.Select(ch => ch.ToString()).ToList();

            var wordPhones = new List<string>(basePhones);
            if (leaf.Features is not null)
            {
                foreach (var rule in project.Affixes.Where(a => a.Enabled))
                {
                    bool posMatch = rule.Pos == leaf.Category || (leaf.Category == "V" && rule.Pos == "Cop");
                    if (!posMatch) continue;
                    bool featMatch = leaf.Features.Any(f => f.Name == rule.FeatureName && f.Value == rule.FeatureValue);
                    if (!featMatch) continue;

                    var affixPhones = rule.Affix.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (rule.IsPrefix) wordPhones.InsertRange(0, affixPhones);
                    else wordPhones.AddRange(affixPhones);
                }
            }
            words.Add(wordPhones);
        }
        return words;
    }
}
