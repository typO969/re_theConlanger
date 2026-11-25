using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
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

        SyntaxNode BuildNp(bool plural, bool includeAdj, string rolePrefix, out string? headClass)
        {
            var nounLex = nouns.Count == 0
                ? null
                : project.Lexemes.FirstOrDefault(l => string.Equals(l.Lemma, Pick(nouns, "person"), StringComparison.OrdinalIgnoreCase) && string.Equals(l.Pos, "N", StringComparison.OrdinalIgnoreCase));
            var noun = nounLex?.Lemma ?? Pick(nouns, "person");
            headClass = string.IsNullOrWhiteSpace(nounLex?.LexClass) ? null : nounLex!.LexClass!.Trim();
            var feats = new List<Feat> { new("Role", rolePrefix), new("Number", plural ? "Pl" : "Sing") };
            if (!string.IsNullOrEmpty(headClass)) feats.Add(new("Class", headClass));
            var nLeaf = new SyntaxNode("N", noun, feats);
            if (!includeAdj || adjs.Count == 0)
                return nLeaf;
            var adjFeats = new List<Feat> { new("Role", "Mod"), new("Number", plural ? "Pl" : "Sing") };
            if (!string.IsNullOrEmpty(headClass)) adjFeats.Add(new("Class", headClass));
            var adjLeaf = new SyntaxNode("ADJ", Pick(adjs, "small"), adjFeats);
            return new SyntaxNode("NP", $"{rolePrefix}NP", new[] { new Feat("Role", $"{rolePrefix}NP") }, new[] { adjLeaf, nLeaf });
        }

        string numSubjStr = Constraint("Number") ?? (rng.NextDouble() < 0.35 ? "Pl" : "Sing");
        string numObjStr = Constraint("Number") ?? (rng.NextDouble() < 0.35 ? "Pl" : "Sing");
        bool subjPl = string.Equals(numSubjStr, "Pl", StringComparison.OrdinalIgnoreCase);
        bool objPl = string.Equals(numObjStr, "Pl", StringComparison.OrdinalIgnoreCase);

        bool useAdjs = spec.ApproxWords >= 4 && adjs.Count > 0;
        var subj = BuildNp(subjPl, useAdjs, "Subj", out var subjClass);
        string tenseVal = Constraint("Tense") ?? "Pres";
        var verbLemma = string.Equals(spec.ClauseType, "Copular", StringComparison.OrdinalIgnoreCase)
            ? Pick(copulas, project.Lexemes.Any(l => l.Lemma == "be") ? "be" : "e")
            : Pick(verbs, "see");
        var verbLex = project.Lexemes.FirstOrDefault(l => string.Equals(l.Lemma, verbLemma, StringComparison.OrdinalIgnoreCase) && string.Equals(l.Pos, "V", StringComparison.OrdinalIgnoreCase));
        var tenseFeats = new List<Feat> { new("Role", string.Equals(spec.ClauseType, "Copular", StringComparison.OrdinalIgnoreCase) ? "Copula" : "Verb"), new("Tense", tenseVal) };
        if (!string.IsNullOrWhiteSpace(verbLex?.LexClass)) tenseFeats.Add(new("Class", verbLex!.LexClass!.Trim()));
        if (!string.IsNullOrEmpty(subjClass)) tenseFeats.Add(new("SubjectClass", subjClass));
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
            var predFeats = new List<Feat> { new("Role", "Pred"), new("Number", subjPl ? "Pl" : "Sing") };
            if (!string.IsNullOrEmpty(subjClass)) predFeats.Add(new("Class", subjClass));
            var pred = new SyntaxNode("ADJ", Pick(adjs, "small"), predFeats);
            var children = project.WordOrder switch
            {
                WordOrder.SOV or WordOrder.OSV => new[] { subj, pred, vNode },
                WordOrder.VSO or WordOrder.VOS => new[] { vNode, subj, pred },
                _ => new[] { subj, vNode, pred }
            };
            return new SyntaxTree(new SyntaxNode("CLAUSE", spec.ClauseType, null, children));
        }

        var obj = BuildNp(objPl, spec.ApproxWords >= 5 && adjs.Count > 0, "Obj", out _);
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
        var extras = node.Features is null
            ? ""
            : string.Join(", ", node.Features.Where(f => f.Name != "Role").Select(f => $"{f.Name}={f.Value}"));
        string header = string.IsNullOrEmpty(role)
            ? $"{node.Category}:{node.Lemma}"
            : parenthesesRole
                ? $"{node.Category}:{node.Lemma} ({role})"
                : $"{node.Category}:{node.Lemma} [{role}]";
        if (!string.IsNullOrEmpty(extras))
            header += $" {{{extras}}}";

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

public sealed class PhraseStructureSyntaxGenerator : ConfigurableSyntaxGenerator
{
    public override string Id => "phrase-structure-syntax";
    public override string Name => "Living-Language Phrase Structure (DET, PRON, AUX, PP)";
    public override string Description => "Hierarchical clauses supporting determiners, pronouns with Case/Person/Number, auxiliaries, negation, and PPs.";

    public override SyntaxTree Generate(SentenceSpec spec, LanguageProject project, Random rng)
    {
        var nouns = project.Lexemes.Where(l => string.Equals(l.Pos, "N", StringComparison.OrdinalIgnoreCase)).ToList();
        var verbs = project.Lexemes.Where(l => string.Equals(l.Pos, "V", StringComparison.OrdinalIgnoreCase)).ToList();
        var adjs = project.Lexemes.Where(l => string.Equals(l.Pos, "ADJ", StringComparison.OrdinalIgnoreCase)).ToList();
        var dets = project.Lexemes.Where(l => string.Equals(l.Pos, "DET", StringComparison.OrdinalIgnoreCase)).ToList();
        var prons = project.Lexemes.Where(l => string.Equals(l.Pos, "PRON", StringComparison.OrdinalIgnoreCase)).ToList();
        var auxes = project.Lexemes.Where(l => string.Equals(l.Pos, "AUX", StringComparison.OrdinalIgnoreCase)).ToList();
        var negs = project.Lexemes.Where(l => string.Equals(l.Pos, "NEG", StringComparison.OrdinalIgnoreCase)).ToList();
        var preps = project.Lexemes.Where(l => string.Equals(l.Pos, "PREP", StringComparison.OrdinalIgnoreCase)).ToList();
        var advs = project.Lexemes.Where(l => string.Equals(l.Pos, "ADV", StringComparison.OrdinalIgnoreCase)).ToList();

        T? Pick<T>(IReadOnlyList<T> list) where T : class =>
            list.Count == 0 ? null : list[rng.Next(list.Count)];

        string? Constraint(string key) =>
            spec.Constraints is not null && spec.Constraints.TryGetValue(key, out var val) ? val : null;

        string tense = Constraint("Tense") ?? "Pres";
        string? constrainedNumber = Constraint("Number");
        bool isNeg = string.Equals(Constraint("Polarity"), "Neg", StringComparison.OrdinalIgnoreCase)
            || (negs.Count > 0 && rng.NextDouble() < 0.15);

        (SyntaxNode Node, string Person, string Number, string HeadLemma, string? HeadClass) BuildNp(string rolePrefix, string gramCase, string? avoidLemma = null)
        {
            var eligibleProns = avoidLemma is null
                ? prons
                : prons.Where(p => !string.Equals(p.Lemma, avoidLemma, StringComparison.OrdinalIgnoreCase)).ToList();

            if (eligibleProns.Count > 0 && rng.NextDouble() < 0.35)
            {
                var pron = Pick(eligibleProns)!;
                var lower = pron.Lemma.ToLowerInvariant();
                string person = lower is "i" or "we" ? "1" : lower == "you" ? "2" : "3";
                string number = constrainedNumber ?? (lower is "they" or "we" or "you" ? "Pl" : "Sing");
                string? pronClass = string.IsNullOrWhiteSpace(pron.LexClass) ? null : pron.LexClass!.Trim();
                var pFeats = new List<Feat>
                {
                    new("Role", rolePrefix),
                    new("Person", person),
                    new("Number", number),
                    new("Case", gramCase)
                };
                if (!string.IsNullOrEmpty(pronClass)) pFeats.Add(new("Class", pronClass));
                var pNode = new SyntaxNode("PRON", pron.Lemma, pFeats);
                return (pNode, person, number, pron.Lemma, pronClass);
            }

            var pool = avoidLemma is null
                ? nouns
                : nouns.Where(n => !string.Equals(n.Lemma, avoidLemma, StringComparison.OrdinalIgnoreCase)).ToList();
            var noun = Pick(pool.Count > 0 ? pool : nouns);
            string nounLemma = noun?.Lemma ?? "person";
            string? headClass = string.IsNullOrWhiteSpace(noun?.LexClass) ? null : noun!.LexClass!.Trim();
            string num = constrainedNumber ?? (rng.NextDouble() < 0.35 ? "Pl" : "Sing");
            var children = new List<SyntaxNode>();

            if (dets.Count > 0)
            {
                bool isMass = string.Equals(nounLemma, "water", StringComparison.OrdinalIgnoreCase);
                var compatDets = string.Equals(num, "Pl", StringComparison.OrdinalIgnoreCase) || isMass
                    ? dets.Where(d => !d.Lemma.Equals("a", StringComparison.OrdinalIgnoreCase) && !d.Lemma.Equals("an", StringComparison.OrdinalIgnoreCase)).ToList()
                    : dets;
                var det = Pick(compatDets.Count > 0 ? compatDets : dets);
                if (det is not null)
                {
                    var detFeats = new List<Feat>
                    {
                        new("Role", "Det"),
                        new("Number", num)
                    };
                    if (!string.IsNullOrEmpty(headClass)) detFeats.Add(new("Class", headClass));
                    children.Add(new SyntaxNode("DET", det.Lemma, detFeats));
                }
            }

            if (spec.ApproxWords >= 4 && adjs.Count > 0 && rng.NextDouble() < 0.65)
            {
                var adj = Pick(adjs)!;
                var adjFeats = new List<Feat> { new("Role", "Mod"), new("Number", num) };
                if (!string.IsNullOrEmpty(headClass)) adjFeats.Add(new("Class", headClass));
                children.Add(new SyntaxNode("ADJ", adj.Lemma, adjFeats));
            }

            var nFeats = new List<Feat>
            {
                new("Role", rolePrefix),
                new("Person", "3"),
                new("Number", num),
                new("Case", gramCase)
            };
            if (!string.IsNullOrEmpty(headClass)) nFeats.Add(new("Class", headClass));
            children.Add(new SyntaxNode("N", nounLemma, nFeats));

            var np = new SyntaxNode("NP", $"{rolePrefix}NP", new[] { new Feat("Role", $"{rolePrefix}NP") }, children);
            return (np, "3", num, nounLemma, headClass);
        }

        var subj = BuildNp("Subj", "Nom");
        var obj = BuildNp("Obj", "Acc", subj.HeadLemma);

        bool isCopular = string.Equals(spec.ClauseType, "Copular", StringComparison.OrdinalIgnoreCase);
        var verbPool = isCopular
            ? verbs.Where(v => string.Equals(v.Lemma, "be", StringComparison.OrdinalIgnoreCase)).ToList()
            : verbs.Where(v => !string.Equals(v.Lemma, "be", StringComparison.OrdinalIgnoreCase)).ToList();
        var chosenVerbLex = isCopular
            ? verbs.FirstOrDefault(v => string.Equals(v.Lemma, "be", StringComparison.OrdinalIgnoreCase))
            : Pick(verbPool.Count > 0 ? verbPool : verbs);
        string chosenVerb = chosenVerbLex?.Lemma ?? (isCopular ? "be" : "see");
        string? verbClass = string.IsNullOrWhiteSpace(chosenVerbLex?.LexClass) ? null : chosenVerbLex!.LexClass!.Trim();

        var vpChildren = new List<SyntaxNode>();
        bool wantFutAux = string.Equals(tense, "Fut", StringComparison.OrdinalIgnoreCase)
            && auxes.Any(a => string.Equals(a.Lemma, "will", StringComparison.OrdinalIgnoreCase));
        var doAux = auxes.FirstOrDefault(a => string.Equals(a.Lemma, "do", StringComparison.OrdinalIgnoreCase));
        SyntaxNode? advNode = advs.Count > 0 && spec.ApproxWords >= 5 && rng.NextDouble() < 0.45
            ? new SyntaxNode("ADV", Pick(advs)!.Lemma, new[] { new Feat("Role", "AdvMod") })
            : null;

        if (wantFutAux)
        {
            vpChildren.Add(new SyntaxNode("AUX", "will", new[] { new Feat("Role", "Aux"), new Feat("Tense", "Pres") }));
            if (isNeg && negs.Count > 0)
                vpChildren.Add(new SyntaxNode("NEG", negs[0].Lemma, new[] { new Feat("Role", "Neg") }));
            if (advNode is not null) vpChildren.Add(advNode);
            var vBareFeats = new List<Feat> { new("Role", "Verb"), new("Finiteness", "Bare") };
            if (!string.IsNullOrEmpty(verbClass)) vBareFeats.Add(new("Class", verbClass));
            vpChildren.Add(new SyntaxNode("V", chosenVerb, vBareFeats));
        }
        else if (isNeg && negs.Count > 0 && !string.Equals(chosenVerb, "be", StringComparison.OrdinalIgnoreCase) && doAux is not null)
        {
            var auxFeats = new List<Feat>
            {
                new("Role", "Aux"),
                new("Tense", tense),
                new("Person", subj.Person),
                new("Number", subj.Number)
            };
            if (string.Equals(tense, "Pres", StringComparison.OrdinalIgnoreCase) && subj.Person == "3" && subj.Number == "Sing")
                auxFeats.Add(new Feat("Agreement", "3sg"));
            vpChildren.Add(new SyntaxNode("AUX", doAux.Lemma, auxFeats));
            vpChildren.Add(new SyntaxNode("NEG", negs[0].Lemma, new[] { new Feat("Role", "Neg") }));
            if (advNode is not null) vpChildren.Add(advNode);
            var vBareFeats = new List<Feat> { new("Role", "Verb"), new("Finiteness", "Bare") };
            if (!string.IsNullOrEmpty(verbClass)) vBareFeats.Add(new("Class", verbClass));
            vpChildren.Add(new SyntaxNode("V", chosenVerb, vBareFeats));
        }
        else
        {
            bool isBe = string.Equals(chosenVerb, "be", StringComparison.OrdinalIgnoreCase);
            if (advNode is not null && !isBe) vpChildren.Add(advNode);
            var verbFeats = new List<Feat>
            {
                new("Role", isCopular ? "Copula" : "Verb"),
                new("Tense", tense),
                new("Person", subj.Person),
                new("Number", subj.Number)
            };
            if (string.Equals(tense, "Pres", StringComparison.OrdinalIgnoreCase) && subj.Person == "3" && subj.Number == "Sing")
                verbFeats.Add(new Feat("Agreement", "3sg"));
            if (!string.IsNullOrEmpty(verbClass)) verbFeats.Add(new Feat("Class", verbClass));
            if (!string.IsNullOrEmpty(subj.HeadClass)) verbFeats.Add(new Feat("SubjectClass", subj.HeadClass));
            vpChildren.Add(new SyntaxNode("V", chosenVerb, verbFeats));
            if (isNeg && negs.Count > 0 && isBe)
                vpChildren.Add(new SyntaxNode("NEG", negs[0].Lemma, new[] { new Feat("Role", "Neg") }));
            if (advNode is not null && isBe) vpChildren.Add(advNode);
        }

        var vNode = vpChildren.Count == 1
            ? vpChildren[0]
            : new SyntaxNode("VP", "VP", new[] { new Feat("Role", "VP") }, vpChildren);

        var predFeats = new List<Feat> { new("Role", "Pred"), new("Number", subj.Number) };
        if (!string.IsNullOrEmpty(subj.HeadClass)) predFeats.Add(new Feat("Class", subj.HeadClass));

        bool isIntrans = string.Equals(spec.ClauseType, "Intransitive", StringComparison.OrdinalIgnoreCase);
        var roles = new Dictionary<string, SyntaxNode> { ["S"] = subj.Node, ["V"] = vNode };
        if (!isIntrans)
        {
            roles["O"] = isCopular
                ? new SyntaxNode("ADJ", Pick(adjs)?.Lemma ?? "small", predFeats)
                : obj.Node;
        }

        string[] order = project.WordOrder switch
        {
            WordOrder.SOV => ["S", "O", "V"],
            WordOrder.VSO => ["V", "S", "O"],
            WordOrder.VOS => ["V", "O", "S"],
            WordOrder.OVS => ["O", "V", "S"],
            WordOrder.OSV => ["O", "S", "V"],
            _ => ["S", "V", "O"]
        };

        var rootChildren = new List<SyntaxNode>();
        foreach (var r in order)
        {
            if (roles.TryGetValue(r, out var rn))
                rootChildren.Add(rn);
        }

        if (spec.ApproxWords >= 6 && preps.Count > 0 && nouns.Count > 0)
        {
            var prep = Pick(preps)!;
            var pobj = BuildNp("Obj", "Acc");
            rootChildren.Add(new SyntaxNode("PP", "PP", new[] { new Feat("Role", "PP") }, new[]
            {
                new SyntaxNode("PREP", prep.Lemma, new[] { new Feat("Role", "Prep") }),
                pobj.Node
            }));
        }

        return new SyntaxTree(new SyntaxNode("CLAUSE", spec.ClauseType, null, rootChildren));
    }
}

public class MorphologyEngine : IMorphologyRealizer
{
    public virtual string Id => "standard-morphology";
    public virtual string Name => "Affixation Realizer (Default)";
    public virtual string Description => "Lexicon-backed stem, irregular suppletion, and conditioned prefix/suffix allomorphy.";

    private static readonly HashSet<string> MultiCharSegments = new(StringComparer.Ordinal)
    {
        "iː", "eɪ", "oʊ", "ɔː", "uː", "ɑː", "ɝː", "aɪ", "aʊ", "ɔɪ",
        "yː", "øː",
        "tʃ", "dʒ", "pʰ", "tʰ", "kʰ"
    };

    private static bool IsCombiningOrChao(char ch) =>
        ch is 'ː' or 'ʰ' or '˥' or '˦' or '˧' or '˨' or '˩' ||
        (ch >= '\u0300' && ch <= '\u036f');

    public static List<string> SplitPhones(string source)
    {
        var trimmed = source.Trim();
        if (string.IsNullOrEmpty(trimmed)) return new List<string>();
        if (trimmed.Contains(' '))
            return trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();

        var noSupra = PhonologyEngine.StripSuprasegmentals(trimmed);
        if (MultiCharSegments.Contains(noSupra) || MultiCharSegments.Contains(PhonologyEngine.StripStressMark(trimmed)))
            return [trimmed];

        var result = new List<string>();
        for (int i = 0; i < trimmed.Length; i++)
        {
            char ch = trimmed[i];
            if (ch == 'ˈ' && i + 1 < trimmed.Length)
            {
                result.Add($"{ch}{trimmed[++i]}");
            }
            else if (IsCombiningOrChao(ch) && result.Count > 0)
            {
                result[^1] += ch;
            }
            else
            {
                result.Add(ch.ToString());
            }
        }
        return result;
    }

    public static AffixType GetEffectiveAffixType(AffixEntry rule) => rule.EffectiveAffixType;

    private static List<string> SplitTemplateSlots(string pattern)
    {
        var trimmed = pattern.Trim();
        if (string.IsNullOrEmpty(trimmed)) return new List<string>();
        if (trimmed.IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
            return trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();

        var slots = new List<string>();
        for (int i = 0; i < trimmed.Length; i++)
        {
            char ch = trimmed[i];
            if ((ch == 'C' || ch == 'c') && i + 1 < trimmed.Length && trimmed[i + 1] >= '1' && trimmed[i + 1] <= '9')
            {
                slots.Add($"C{trimmed[++i]}");
            }
            else if (IsCombiningOrChao(ch) && slots.Count > 0 && !Regex.IsMatch(slots[^1], @"^C[1-9]$"))
            {
                slots[^1] += ch;
            }
            else if (ch != '-' && ch != '.')
            {
                slots.Add(ch.ToString());
            }
        }
        return slots;
    }

    protected static List<string> ResolveStemPhones(string lemma, LanguageProject project)
    {
        var lex = project.Lexemes.FirstOrDefault(l => string.Equals(l.Lemma, lemma, StringComparison.Ordinal));
        return lex is not null && !string.IsNullOrWhiteSpace(lex.UR)
            ? SplitPhones(lex.UR)
            : SplitPhones(lemma);
    }

    private static (string Ur, HashSet<string> MatchedFeatures, bool IsIrregular) ResolveIrregularStem(
        LexemeEntry? lex,
        string fallbackLemma,
        IReadOnlyList<Feat>? features)
    {
        if (lex?.Irregulars is { Count: > 0 } && features is { Count: > 0 })
        {
            int bestScore = 0;
            string bestUr = "";
            var bestKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (keySpec, form) in lex.Irregulars)
            {
                var clauses = keySpec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(c => c.Split('=', 2))
                    .Where(p => p.Length == 2 && !string.IsNullOrWhiteSpace(p[0]) && !string.IsNullOrWhiteSpace(p[1]))
                    .Select(p => (Key: p[0].Trim(), Val: p[1].Trim()))
                    .ToList();
                if (clauses.Count == 0) continue;

                bool allMatch = clauses.All(cl => features.Any(f =>
                    string.Equals(f.Name, cl.Key, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Value, cl.Val, StringComparison.OrdinalIgnoreCase)));
                if (allMatch && clauses.Count > bestScore)
                {
                    bestScore = clauses.Count;
                    bestUr = form;
                    bestKeys = new HashSet<string>(clauses.Select(c => c.Key), StringComparison.OrdinalIgnoreCase);
                }
            }

            if (bestScore > 0)
            {
                if (bestKeys.Contains("Person") || bestKeys.Contains("Tense"))
                    bestKeys.Add("Agreement");
                return (bestUr, bestKeys, true);
            }
        }

        var defaultUr = lex is not null && !string.IsNullOrWhiteSpace(lex.UR) ? lex.UR : fallbackLemma;
        return (defaultUr, new HashSet<string>(StringComparer.OrdinalIgnoreCase), false);
    }

    public virtual (List<string> Phones, bool IsIrregular, string? Spelling) InflectSingleDetailed(
        string lemma,
        string pos,
        IReadOnlyList<Feat>? features,
        LanguageProject project)
    {
        var lex = project.Lexemes.FirstOrDefault(l => string.Equals(l.Lemma, lemma, StringComparison.Ordinal))
            ?? project.Lexemes.FirstOrDefault(l => string.Equals(l.Lemma, lemma, StringComparison.OrdinalIgnoreCase));
        var (ur, matchedFeatures, isIrregular) = ResolveIrregularStem(lex, lemma, features);
        var wordPhones = SplitPhones(ur);
        var handledFeatures = new HashSet<string>(matchedFeatures, StringComparer.OrdinalIgnoreCase);

        string? effectiveClass = !string.IsNullOrWhiteSpace(lex?.LexClass)
            ? lex!.LexClass!.Trim()
            : features?.FirstOrDefault(f => string.Equals(f.Name, "Class", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

        var effectiveFeatures = new List<Feat>(features ?? Array.Empty<Feat>());
        if (!string.IsNullOrEmpty(effectiveClass) &&
            !effectiveFeatures.Any(f => string.Equals(f.Name, "Class", StringComparison.OrdinalIgnoreCase)))
        {
            effectiveFeatures.Add(new Feat("Class", effectiveClass));
        }

        if (effectiveFeatures.Count > 0)
        {
            foreach (var rule in project.Affixes.Where(a => a.Enabled))
            {
                bool posMatch = string.Equals(rule.Pos, pos, StringComparison.OrdinalIgnoreCase)
                    || (string.Equals(pos, "V", StringComparison.OrdinalIgnoreCase) && string.Equals(rule.Pos, "Cop", StringComparison.OrdinalIgnoreCase));
                if (!posMatch) continue;
                if (handledFeatures.Contains(rule.FeatureName)) continue;

                bool featMatch = effectiveFeatures.Any(f =>
                    string.Equals(f.Name, rule.FeatureName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.Value, rule.FeatureValue, StringComparison.OrdinalIgnoreCase));
                if (!featMatch) continue;

                var kind = GetEffectiveAffixType(rule);

                if (!string.IsNullOrWhiteSpace(rule.Condition))
                {
                    if (wordPhones.Count == 0 && !rule.Condition.Contains("class=", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var anchor = wordPhones.Count == 0
                        ? ""
                        : (kind is AffixType.Prefix or AffixType.Infix ? wordPhones[0] : wordPhones[^1]);
                    if (!PhonologyEngine.MatchesCondition(anchor, rule.Condition, project, lexClass: effectiveClass))
                        continue;
                }

                if (kind == AffixType.Infix)
                {
                    var cleaned = rule.Affix.Replace("-", "", StringComparison.Ordinal)
                        .Replace("<", "", StringComparison.Ordinal)
                        .Replace(">", "", StringComparison.Ordinal)
                        .Trim();
                    var infixPhones = SplitPhones(cleaned);
                    int splitIdx = wordPhones.FindIndex(p => PhonologyEngine.IsVowel(p, project));
                    if (splitIdx < 0) splitIdx = Math.Min(1, wordPhones.Count);
                    wordPhones.InsertRange(splitIdx, infixPhones);
                }
                else if (kind == AffixType.Circumfix)
                {
                    var raw = rule.Affix.Trim();
                    string preRaw = "";
                    string sufRaw = "";
                    if (raw.Contains("...", StringComparison.Ordinal))
                    {
                        var parts = raw.Split("...", 2);
                        preRaw = parts[0];
                        sufRaw = parts.Length > 1 ? parts[1] : "";
                    }
                    else if (raw.Contains('+'))
                    {
                        var parts = raw.Split('+', 2);
                        preRaw = parts[0];
                        sufRaw = parts.Length > 1 ? parts[1] : "";
                    }
                    else if (raw.Contains('/'))
                    {
                        var parts = raw.Split('/', 2);
                        preRaw = parts[0];
                        sufRaw = parts.Length > 1 ? parts[1] : "";
                    }
                    else
                    {
                        preRaw = raw;
                    }

                    string CleanPart(string s) => s.Replace("-", "", StringComparison.Ordinal)
                        .Replace("+", "", StringComparison.Ordinal)
                        .Replace("/", "", StringComparison.Ordinal)
                        .Trim();

                    var prePhones = SplitPhones(CleanPart(preRaw));
                    var sufPhones = SplitPhones(CleanPart(sufRaw));
                    if (prePhones.Count > 0) wordPhones.InsertRange(0, prePhones);
                    if (sufPhones.Count > 0) wordPhones.AddRange(sufPhones);
                }
                else if (kind == AffixType.Template)
                {
                    var radicals = wordPhones.Where(p => !PhonologyEngine.IsVowel(p, project)).ToList();
                    var slots = SplitTemplateSlots(rule.Affix);
                    var templated = new List<string>();
                    foreach (var slot in slots)
                    {
                        var m = Regex.Match(slot, @"^C([1-9])$", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            int rIdx = int.Parse(m.Groups[1].Value) - 1;
                            if (rIdx >= 0 && rIdx < radicals.Count)
                                templated.Add(radicals[rIdx]);
                        }
                        else
                        {
                            templated.AddRange(SplitPhones(slot));
                        }
                    }
                    wordPhones = templated;
                }
                else if (kind == AffixType.Prefix)
                {
                    var affixPhones = SplitPhones(rule.Affix.Replace("-", "", StringComparison.Ordinal));
                    wordPhones.InsertRange(0, affixPhones);
                }
                else
                {
                    var affixPhones = SplitPhones(rule.Affix.Replace("-", "", StringComparison.Ordinal));
                    wordPhones.AddRange(affixPhones);
                }

                handledFeatures.Add(rule.FeatureName);
            }
        }
        return (wordPhones, isIrregular, lex?.Spelling);
    }

    public virtual List<string> InflectSingle(string lemma, string pos, IReadOnlyList<Feat>? features, LanguageProject project) =>
        InflectSingleDetailed(lemma, pos, features, project).Phones;

    public virtual IReadOnlyList<IReadOnlyList<string>> RealizeWords(SyntaxTree tree, LanguageProject project)
    {
        var words = new List<IReadOnlyList<string>>();
        foreach (var leaf in tree.Leaves())
        {
            if (string.IsNullOrWhiteSpace(leaf.Lemma)) continue;
            words.Add(InflectSingleDetailed(leaf.Lemma, leaf.Category, leaf.Features, project).Phones);
        }
        return words;
    }

    public virtual IReadOnlyList<InflectedWord> RealizeDetailed(SyntaxTree tree, LanguageProject project)
    {
        var words = new List<InflectedWord>();
        foreach (var leaf in tree.Leaves())
        {
            if (string.IsNullOrWhiteSpace(leaf.Lemma)) continue;
            var (phones, isIrreg, spelling) = InflectSingleDetailed(leaf.Lemma, leaf.Category, leaf.Features, project);
            words.Add(new InflectedWord(phones, leaf.Lemma, leaf.Category, leaf.Features, spelling, isIrreg));
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

    public override (List<string> Phones, bool IsIrregular, string? Spelling) InflectSingleDetailed(
        string lemma,
        string pos,
        IReadOnlyList<Feat>? features,
        LanguageProject project)
    {
        var (phones, isIrreg, spelling) = base.InflectSingleDetailed(lemma, pos, features, project);
        if (string.Equals(pos, "N", StringComparison.OrdinalIgnoreCase) &&
            features is not null &&
            features.Any(f => string.Equals(f.Name, "Number", StringComparison.OrdinalIgnoreCase) && string.Equals(f.Value, "Pl", StringComparison.OrdinalIgnoreCase)))
        {
            var stem = ResolveStemPhones(lemma, project);
            if (stem.Count >= 2)
                phones.InsertRange(0, stem.Take(2));
        }
        return (phones, isIrreg, spelling);
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
        reg._syntaxGenerators.Add(new PhraseStructureSyntaxGenerator());

        reg._morphologyRealizers.Add(new MorphologyEngine());
        reg._morphologyRealizers.Add(new ReduplicativePluralMorphology());

        reg._orthographyRenderers.Add(new OrthographyEngine());
        reg._orthographyRenderers.Add(new MorphophonemicOrthography());
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
        if (string.IsNullOrWhiteSpace(pluginsDirectory) || !Directory.Exists(pluginsDirectory))
            return;

        foreach (var dll in Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories))
        {
            if (IsIgnoredBuildPath(dll)) continue;
            LoadExtensionAssembly(dll);
        }
    }

    public void LoadExtensionAssembly(string dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath) || IsIgnoredBuildPath(dllPath))
            return;

        var fileName = Path.GetFileName(dllPath);
        if (fileName.StartsWith("Conlanger.", StringComparison.OrdinalIgnoreCase))
            return;

        var alc = new AssemblyLoadContext($"ext-{Path.GetFileNameWithoutExtension(dllPath)}-{Guid.NewGuid():N}", isCollectible: true);
        bool registeredAny = false;
        try
        {
            using var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var asm = alc.LoadFromStream(fs);
            foreach (var t in asm.GetExportedTypes())
            {
                if (typeof(IPipelineExtensionProvider).IsAssignableFrom(t) && !t.IsAbstract)
                {
                    if (Activator.CreateInstance(t) is IPipelineExtensionProvider provider)
                    {
                        registeredAny = true;
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
        finally
        {
            if (!registeredAny)
                alc.Unload();
        }
    }

    private static bool IsIgnoredBuildPath(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}obj{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}ref{sep}", StringComparison.OrdinalIgnoreCase);
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
