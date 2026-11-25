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
    public virtual string Description => "Standard SVO/SOV/VSO clause planner with NP modifier order, case alignment, and mood.";

    protected static SyntaxConfig GetNormalizedConfig(LanguageProject project)
    {
        var cfg = project.SyntaxConfig ?? new SyntaxConfig();
        return new SyntaxConfig
        {
            Alignment = cfg.Alignment,
            AdjectiveOrder = cfg.AdjectiveOrder,
            DeterminerOrder = cfg.DeterminerOrder,
            AdpositionType = cfg.AdpositionType,
            QuestionStrategy = cfg.QuestionStrategy,
            ProDrop = cfg.ProDrop,
            PolypersonalAgreement = cfg.PolypersonalAgreement,
            AllowEmbeddedClauses = cfg.AllowEmbeddedClauses,
            AllowRelativeClauses = cfg.AllowRelativeClauses,
            AllowPassive = cfg.AllowPassive,
            MaxClauseDepth = Math.Clamp(cfg.MaxClauseDepth, 0, 3),
            CustomRules = cfg.CustomRules ?? new List<string>()
        };
    }

    protected static (string SubjCase, string ObjCase, string ObliqueCase) ResolveAlignmentCases(AlignmentSystem alignment, bool hasDirectObject) =>
        alignment switch
        {
            AlignmentSystem.ErgAbs => (hasDirectObject ? "Erg" : "Abs", "Abs", "Obl"),
            AlignmentSystem.Tripartite => (hasDirectObject ? "Erg" : "Intrans", "Acc", "Obl"),
            _ => ("Nom", "Acc", "Acc")
        };

    protected static Dictionary<string, string[]> ParseCustomCfgRules(IEnumerable<string>? rules)
    {
        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (rules is null) return map;
        foreach (var raw in rules)
        {
            var line = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal))
                continue;
            int arrowIdx = line.IndexOf("->", StringComparison.Ordinal);
            if (arrowIdx <= 0) continue;
            var lhs = line[..arrowIdx].Trim().ToUpperInvariant();
            var rhs = line[(arrowIdx + 2)..]
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToUpperInvariant())
                .Where(s => s.Length > 0)
                .ToArray();
            if (!string.IsNullOrEmpty(lhs) && rhs.Length > 0 && !map.ContainsKey(lhs))
                map[lhs] = rhs;
        }
        return map;
    }

    protected static string ResolveMoodFromSpec(SentenceSpec spec)
    {
        string raw = spec.Constraints is not null && spec.Constraints.TryGetValue("Mood", out var m)
            ? m.Trim().ToLowerInvariant()
            : "";
        string clauseLower = spec.ClauseType.ToLowerInvariant();
        if (raw is "ynquestion" or "question" or "interrogative" || clauseLower == "ynquestion")
            return "YNQuestion";
        if (raw is "imp" or "imperative" || clauseLower == "imp")
            return "Imp";
        return "Decl";
    }

    protected static string FormatAgrCode(string person, string number) =>
        $"{person}{(string.Equals(number, "pl", StringComparison.OrdinalIgnoreCase) ? "pl" : "sg")}";

    protected static string? GetConstraint(SentenceSpec spec, string key) =>
        spec.Constraints is not null && spec.Constraints.TryGetValue(key, out var val) ? val : null;

    public virtual SyntaxTree Generate(SentenceSpec spec, LanguageProject project, Random rng) =>
        new(BuildConfigurableClause(spec, project, rng, 0, null));

    private SyntaxNode BuildConfigurableClause(
        SentenceSpec spec,
        LanguageProject project,
        Random rng,
        int depth,
        (string HeadLemma, string Number, string? HeadClass)? relHead)
    {
        var cfg = GetNormalizedConfig(project);
        var cfgMap = ParseCustomCfgRules(cfg.CustomRules);
        var nouns = project.Lexemes.Where(l => string.Equals(l.Pos, "N", StringComparison.OrdinalIgnoreCase)).ToList();
        var adjs = project.Lexemes.Where(l => string.Equals(l.Pos, "ADJ", StringComparison.OrdinalIgnoreCase)).ToList();
        var verbs = project.Lexemes.Where(l => string.Equals(l.Pos, "V", StringComparison.OrdinalIgnoreCase)).ToList();
        var copulas = project.Lexemes.Where(l => string.Equals(l.Pos, "Cop", StringComparison.OrdinalIgnoreCase)).ToList();
        var comps = project.Lexemes.Where(l => string.Equals(l.Pos, "COMP", StringComparison.OrdinalIgnoreCase)).ToList();
        var rels = project.Lexemes.Where(l => string.Equals(l.Pos, "REL", StringComparison.OrdinalIgnoreCase)).ToList();
        var qParts = project.Lexemes.Where(l => string.Equals(l.Pos, "Q", StringComparison.OrdinalIgnoreCase) || string.Equals(l.Pos, "PART", StringComparison.OrdinalIgnoreCase)).ToList();
        var preps = project.Lexemes.Where(l => string.Equals(l.Pos, "PREP", StringComparison.OrdinalIgnoreCase) || string.Equals(l.Pos, "POSTP", StringComparison.OrdinalIgnoreCase)).ToList();

        string PickLemma(IReadOnlyList<LexemeEntry> list, string fallback) =>
            list.Count == 0 ? fallback : list[rng.Next(list.Count)].Lemma;

        string mood = depth == 0 ? ResolveMoodFromSpec(spec) : "Decl";
        string tense = GetConstraint(spec, "Tense") ?? "Pres";
        string? voiceConstraint = GetConstraint(spec, "Voice");
        bool forcePassive = string.Equals(voiceConstraint, "Passive", StringComparison.OrdinalIgnoreCase);
        bool forbidPassive = string.Equals(voiceConstraint, "Active", StringComparison.OrdinalIgnoreCase);
        bool isPassive = project.ClauseTemplate == ClauseTemplate.Transitive
            && relHead is null
            && mood != "Imp"
            && (forcePassive || (!forbidPassive && cfg.AllowPassive && rng.NextDouble() < 0.25));

        int maxDepth = Math.Max(0, cfg.MaxClauseDepth);
        string? embeddedConstraint = GetConstraint(spec, "Embedded");
        bool forceEmbedded = string.Equals(embeddedConstraint, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(embeddedConstraint, "Complement", StringComparison.OrdinalIgnoreCase);
        bool canEmbed = depth < maxDepth
            && project.ClauseTemplate == ClauseTemplate.Transitive
            && !isPassive
            && relHead is null
            && mood == "Decl"
            && (forceEmbedded || (cfg.AllowEmbeddedClauses && comps.Count > 0 && !string.Equals(embeddedConstraint, "false", StringComparison.OrdinalIgnoreCase) && rng.NextDouble() < 0.3));

        bool hasDirectObject = project.ClauseTemplate == ClauseTemplate.Transitive && !isPassive && !canEmbed;
        var cases = ResolveAlignmentCases(cfg.Alignment, hasDirectObject);

        string? numConstraint = GetConstraint(spec, "Number");
        string subjNumber = relHead is not null ? relHead.Value.Number : (numConstraint ?? (rng.NextDouble() < 0.35 ? "Pl" : "Sing"));
        string objNumber = numConstraint ?? (rng.NextDouble() < 0.35 ? "Pl" : "Sing");

        (SyntaxNode? Node, string Person, string Number, string HeadLemma, string? HeadClass) BuildBasicNp(
            string rolePrefix,
            string gramCase,
            string number,
            bool allowAdj,
            string? avoidLemma = null)
        {
            var pool = avoidLemma is null
                ? nouns
                : nouns.Where(n => !string.Equals(n.Lemma, avoidLemma, StringComparison.OrdinalIgnoreCase)).ToList();
            var nounList = pool.Count > 0 ? pool : nouns;
            var nounLex = nounList.Count == 0 ? null : nounList[rng.Next(nounList.Count)];
            string nounLemma = nounLex?.Lemma ?? "person";
            string? headClass = string.IsNullOrWhiteSpace(nounLex?.LexClass) ? null : nounLex!.LexClass!.Trim();

            var nFeats = new List<Feat>
            {
                new("Role", rolePrefix),
                new("Person", "3"),
                new("Number", number),
                new("Case", gramCase)
            };
            if (!string.IsNullOrEmpty(headClass)) nFeats.Add(new("Class", headClass));
            var nLeaf = new SyntaxNode("N", nounLemma, nFeats);

            SyntaxNode? adjLeaf = null;
            if (allowAdj && adjs.Count > 0)
            {
                var adjFeats = new List<Feat> { new("Role", "Mod"), new("Number", number) };
                if (!string.IsNullOrEmpty(headClass)) adjFeats.Add(new("Class", headClass));
                adjLeaf = new SyntaxNode("ADJ", PickLemma(adjs, "small"), adjFeats);
            }

            SyntaxNode? relCp = null;
            string? relConstraint = GetConstraint(spec, "Relative");
            bool forceRel = string.Equals(relConstraint, "true", StringComparison.OrdinalIgnoreCase) && rolePrefix == "Subj";
            bool wantRel = depth < maxDepth
                && relHead is null
                && rolePrefix == "Subj"
                && (forceRel || (cfg.AllowRelativeClauses && rels.Count + comps.Count > 0 && !string.Equals(relConstraint, "false", StringComparison.OrdinalIgnoreCase) && rng.NextDouble() < 0.3));
            if (wantRel)
            {
                var relWord = rels.FirstOrDefault() ?? comps.FirstOrDefault();
                var inner = BuildConfigurableClause(
                    new SentenceSpec("Transitive", 3, new Dictionary<string, string> { ["Tense"] = tense }),
                    project,
                    rng,
                    depth + 1,
                    (nounLemma, number, headClass));
                var relChildren = new List<SyntaxNode>();
                if (cfg.AdpositionType == AdpositionOrder.Postposition && project.WordOrder == WordOrder.SOV)
                {
                    relChildren.Add(inner);
                    if (relWord is not null) relChildren.Add(new SyntaxNode(relWord.Pos, relWord.Lemma, [new Feat("Role", "Rel")]));
                }
                else
                {
                    if (relWord is not null) relChildren.Add(new SyntaxNode(relWord.Pos, relWord.Lemma, [new Feat("Role", "Rel")]));
                    relChildren.Add(inner);
                }
                relCp = new SyntaxNode("CP", "RelCP", [new Feat("Role", "RelCP")], relChildren);
            }

            if (adjLeaf is null && relCp is null)
                return (nLeaf, "3", number, nounLemma, headClass);

            var orderedChildren = new List<SyntaxNode>();
            if (cfgMap.TryGetValue("NP", out var npCfg))
            {
                foreach (var sym in npCfg)
                {
                    if (sym == "ADJ" && adjLeaf is not null) orderedChildren.Add(adjLeaf);
                    else if (sym == "N") orderedChildren.Add(nLeaf);
                    else if ((sym == "REL" || sym == "CP") && relCp is not null) orderedChildren.Add(relCp);
                }
                if (!orderedChildren.Contains(nLeaf)) orderedChildren.Add(nLeaf);
                if (adjLeaf is not null && !orderedChildren.Contains(adjLeaf)) orderedChildren.Insert(0, adjLeaf);
                if (relCp is not null && !orderedChildren.Contains(relCp)) orderedChildren.Add(relCp);
            }
            else
            {
                if (relCp is not null && cfg.AdpositionType == AdpositionOrder.Postposition && project.WordOrder == WordOrder.SOV)
                    orderedChildren.Add(relCp);
                if (adjLeaf is not null && cfg.AdjectiveOrder == ModifierOrder.BeforeNoun)
                    orderedChildren.Add(adjLeaf);
                orderedChildren.Add(nLeaf);
                if (adjLeaf is not null && cfg.AdjectiveOrder == ModifierOrder.AfterNoun)
                    orderedChildren.Add(adjLeaf);
                if (relCp is not null && !(cfg.AdpositionType == AdpositionOrder.Postposition && project.WordOrder == WordOrder.SOV))
                    orderedChildren.Add(relCp);
            }

            var npNode = new SyntaxNode("NP", $"{rolePrefix}NP", [new Feat("Role", $"{rolePrefix}NP")], orderedChildren);
            return (npNode, "3", number, nounLemma, headClass);
        }

        var subj = relHead is not null
            ? ((SyntaxNode?)null, "3", relHead.Value.Number, relHead.Value.HeadLemma, relHead.Value.HeadClass)
            : BuildBasicNp("Subj", cases.SubjCase, subjNumber, spec.ApproxWords >= 4 && adjs.Count > 0);

        (SyntaxNode? Node, string Person, string Number, string HeadLemma, string? HeadClass)? objInfo = null;
        SyntaxNode? cpObjNode = null;

        if (canEmbed)
        {
            var compLex = comps.FirstOrDefault();
            var embedded = BuildConfigurableClause(
                new SentenceSpec("Transitive", Math.Max(3, spec.ApproxWords - 2), new Dictionary<string, string> { ["Tense"] = tense }),
                project,
                rng,
                depth + 1,
                null);
            var cpChildren = new List<SyntaxNode>();
            if (cfg.AdpositionType == AdpositionOrder.Postposition && project.WordOrder == WordOrder.SOV)
            {
                cpChildren.Add(embedded);
                if (compLex is not null) cpChildren.Add(new SyntaxNode("COMP", compLex.Lemma, [new Feat("Role", "Comp")]));
            }
            else
            {
                if (compLex is not null) cpChildren.Add(new SyntaxNode("COMP", compLex.Lemma, [new Feat("Role", "Comp")]));
                cpChildren.Add(embedded);
            }
            cpObjNode = new SyntaxNode("CP", "CompCP", [new Feat("Role", "CompCP")], cpChildren);
        }
        else if (project.ClauseTemplate == ClauseTemplate.Transitive && !isPassive)
        {
            objInfo = BuildBasicNp("Obj", cases.ObjCase, objNumber, spec.ApproxWords >= 5 && adjs.Count > 0, subj.Item4);
        }

        var verbLemma = project.ClauseTemplate == ClauseTemplate.Copular
            ? PickLemma(copulas, verbs.Any(v => v.Lemma == "be") ? "be" : "e")
            : PickLemma(verbs, "see");
        var verbLex = verbs.FirstOrDefault(l => string.Equals(l.Lemma, verbLemma, StringComparison.OrdinalIgnoreCase));
        string? verbClass = string.IsNullOrWhiteSpace(verbLex?.LexClass) ? null : verbLex!.LexClass!.Trim();

        var verbFeats = new List<Feat>
        {
            new("Role", project.ClauseTemplate == ClauseTemplate.Copular ? "Copula" : "Verb")
        };
        if (mood == "Imp")
        {
            verbFeats.Add(new("Mood", "Imp"));
            verbFeats.Add(new("Finiteness", "Bare"));
            verbFeats.Add(new("Person", "2"));
        }
        else
        {
            verbFeats.Add(new("Tense", tense));
            verbFeats.Add(new("Person", subj.Item2));
            verbFeats.Add(new("Number", subj.Item3));
        }
        if (isPassive)
        {
            verbFeats.Add(new("Voice", "Passive"));
            verbFeats.Add(new("Aspect", "Part"));
        }
        if (!string.IsNullOrEmpty(verbClass)) verbFeats.Add(new("Class", verbClass));
        if (!string.IsNullOrEmpty(subj.Item5)) verbFeats.Add(new("SubjectClass", subj.Item5));
        if (cfg.PolypersonalAgreement)
        {
            verbFeats.Add(new("SubjAgr", FormatAgrCode(mood == "Imp" ? "2" : subj.Item2, subj.Item3)));
            if (objInfo is not null)
            {
                verbFeats.Add(new("ObjectPerson", objInfo.Value.Person));
                verbFeats.Add(new("ObjectNumber", objInfo.Value.Number));
                verbFeats.Add(new("ObjAgr", FormatAgrCode(objInfo.Value.Person, objInfo.Value.Number)));
                if (!string.IsNullOrEmpty(objInfo.Value.HeadClass)) verbFeats.Add(new("ObjectClass", objInfo.Value.HeadClass!));
            }
        }
        var vNode = new SyntaxNode("V", verbLemma, verbFeats);

        var roles = new Dictionary<string, SyntaxNode> { ["V"] = vNode };
        if (subj.Item1 is not null && mood != "Imp") roles["S"] = subj.Item1;

        if (cpObjNode is not null)
        {
            roles["O"] = cpObjNode;
        }
        else if (project.ClauseTemplate == ClauseTemplate.Copular)
        {
            var predFeats = new List<Feat> { new("Role", "Pred"), new("Number", subj.Item3) };
            if (!string.IsNullOrEmpty(subj.Item5)) predFeats.Add(new("Class", subj.Item5));
            roles["O"] = new SyntaxNode("ADJ", PickLemma(adjs, nouns.FirstOrDefault()?.Lemma ?? "small"), predFeats);
        }
        else if (objInfo?.Node is not null)
        {
            roles["O"] = objInfo.Value.Node;
        }

        SyntaxNode? agentPp = null;
        if (isPassive && spec.ApproxWords >= 5 && preps.Count > 0 && nouns.Count > 0)
        {
            var agentPrep = preps.FirstOrDefault(p => string.Equals(p.Lemma, "by", StringComparison.OrdinalIgnoreCase)) ?? preps[0];
            var agentNp = BuildBasicNp("Obj", cases.ObliqueCase, "Sing", false, subj.Item4);
            var prepLeaf = new SyntaxNode(agentPrep.Pos, agentPrep.Lemma, [new Feat("Role", "Prep")]);
            var ppChildren = cfg.AdpositionType == AdpositionOrder.Postposition
                ? new[] { agentNp.Node!, prepLeaf }
                : new[] { prepLeaf, agentNp.Node! };
            agentPp = new SyntaxNode("PP", "AgentPP", [new Feat("Role", "PP")], ppChildren);
        }

        string[] canonicalOrder = project.WordOrder switch
        {
            WordOrder.SOV => ["S", "O", "V"],
            WordOrder.VSO => ["V", "S", "O"],
            WordOrder.VOS => ["V", "O", "S"],
            WordOrder.OVS => ["O", "V", "S"],
            WordOrder.OSV => ["O", "S", "V"],
            _ => ["S", "V", "O"]
        };

        string[] effectiveOrder = cfgMap.TryGetValue("CLAUSE", out var customClause)
            ? customClause
            : mood == "YNQuestion" && cfg.QuestionStrategy == QuestionStrategy.Inversion
                ? (project.WordOrder is WordOrder.VOS or WordOrder.OVS ? ["V", "O", "S"] : ["V", "S", "O"])
                : canonicalOrder.Where(roles.ContainsKey).ToArray();

        var clauseFeats = new List<Feat>();
        if (depth > 0) clauseFeats.Add(new("Role", "EmbeddedClause"));
        if (mood != "Decl") clauseFeats.Add(new("Mood", mood));
        if (isPassive) clauseFeats.Add(new("Voice", "Passive"));
        if (cfg.Alignment != AlignmentSystem.NomAcc) clauseFeats.Add(new("Alignment", cfg.Alignment.ToString()));

        var rootChildren = new List<SyntaxNode>();
        foreach (var r in effectiveOrder)
        {
            if (roles.TryGetValue(r, out var rn))
                rootChildren.Add(rn);
            else if (r == "PP" && agentPp is not null)
            {
                rootChildren.Add(agentPp);
                agentPp = null;
            }
            else if (r == "CP" && cpObjNode is not null && !rootChildren.Contains(cpObjNode))
                rootChildren.Add(cpObjNode);
        }
        if (agentPp is not null) rootChildren.Add(agentPp);

        if (mood == "YNQuestion" && cfg.QuestionStrategy == QuestionStrategy.Particle && qParts.Count > 0)
        {
            var qLeaf = new SyntaxNode(qParts[0].Pos, qParts[0].Lemma, [new Feat("Role", "QPart")]);
            if (project.WordOrder is WordOrder.VSO or WordOrder.VOS)
                rootChildren.Insert(0, qLeaf);
            else
                rootChildren.Add(qLeaf);
        }

        return new SyntaxNode("CLAUSE", isPassive ? "Passive" : project.ClauseTemplate.ToString(), clauseFeats, rootChildren);
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
    public override string Name => "Living-Language Phrase Structure (DET, PRON, AUX, PP, CP)";
    public override string Description => "Recursive clauses supporting determiners, pronouns, alignment, passive voice, questions, auxiliaries, and PPs.";

    public override SyntaxTree Generate(SentenceSpec spec, LanguageProject project, Random rng)
    {
        var cfg = GetNormalizedConfig(project);
        var conjs = project.Lexemes.Where(l => string.Equals(l.Pos, "CONJ", StringComparison.OrdinalIgnoreCase)).ToList();
        string mood = ResolveMoodFromSpec(spec);

        bool forceCoord = string.Equals(GetConstraint(spec, "Coordinated"), "true", StringComparison.OrdinalIgnoreCase);
        bool wantCoord = cfg.MaxClauseDepth >= 1
            && conjs.Count > 0
            && mood == "Decl"
            && (forceCoord || (cfg.AllowEmbeddedClauses && spec.ApproxWords >= 7 && rng.NextDouble() < 0.25));

        if (wantCoord)
        {
            var c1 = BuildPhraseClause(spec with { ApproxWords = Math.Max(4, spec.ApproxWords - 2) }, project, rng, 0, null);
            var conj = conjs[rng.Next(conjs.Count)];
            string tense = GetConstraint(spec, "Tense") ?? "Pres";
            var c2 = BuildPhraseClause(
                new SentenceSpec(project.ClauseTemplate.ToString(), Math.Max(3, spec.ApproxWords - 3), new Dictionary<string, string> { ["Tense"] = tense }),
                project,
                rng,
                1,
                null);
            return new SyntaxTree(new SyntaxNode(
                "CLAUSE",
                "Coordinated",
                [new Feat("Role", "CoordRoot")],
                [c1, new SyntaxNode("CONJ", conj.Lemma, [new Feat("Role", "Conj")]), c2]));
        }

        return new SyntaxTree(BuildPhraseClause(spec, project, rng, 0, null));
    }

    private SyntaxNode BuildPhraseClause(
        SentenceSpec spec,
        LanguageProject project,
        Random rng,
        int depth,
        (string HeadLemma, string Number, string? HeadClass)? relHead)
    {
        var cfg = GetNormalizedConfig(project);
        var cfgMap = ParseCustomCfgRules(cfg.CustomRules);

        var nouns = project.Lexemes.Where(l => string.Equals(l.Pos, "N", StringComparison.OrdinalIgnoreCase)).ToList();
        var verbs = project.Lexemes.Where(l => string.Equals(l.Pos, "V", StringComparison.OrdinalIgnoreCase)).ToList();
        var adjs = project.Lexemes.Where(l => string.Equals(l.Pos, "ADJ", StringComparison.OrdinalIgnoreCase)).ToList();
        var dets = project.Lexemes.Where(l => string.Equals(l.Pos, "DET", StringComparison.OrdinalIgnoreCase)).ToList();
        var prons = project.Lexemes.Where(l => string.Equals(l.Pos, "PRON", StringComparison.OrdinalIgnoreCase)).ToList();
        var auxes = project.Lexemes.Where(l => string.Equals(l.Pos, "AUX", StringComparison.OrdinalIgnoreCase)).ToList();
        var negs = project.Lexemes.Where(l => string.Equals(l.Pos, "NEG", StringComparison.OrdinalIgnoreCase)).ToList();
        var preps = project.Lexemes.Where(l => string.Equals(l.Pos, "PREP", StringComparison.OrdinalIgnoreCase) || string.Equals(l.Pos, "POSTP", StringComparison.OrdinalIgnoreCase)).ToList();
        var advs = project.Lexemes.Where(l => string.Equals(l.Pos, "ADV", StringComparison.OrdinalIgnoreCase)).ToList();
        var comps = project.Lexemes.Where(l => string.Equals(l.Pos, "COMP", StringComparison.OrdinalIgnoreCase)).ToList();
        var rels = project.Lexemes.Where(l => string.Equals(l.Pos, "REL", StringComparison.OrdinalIgnoreCase)).ToList();
        var qParts = project.Lexemes.Where(l => string.Equals(l.Pos, "Q", StringComparison.OrdinalIgnoreCase) || string.Equals(l.Pos, "PART", StringComparison.OrdinalIgnoreCase)).ToList();

        T? Pick<T>(IReadOnlyList<T> list) where T : class =>
            list.Count == 0 ? null : list[rng.Next(list.Count)];

        string mood = depth == 0 ? ResolveMoodFromSpec(spec) : "Decl";
        string tense = GetConstraint(spec, "Tense") ?? "Pres";
        string? constrainedNumber = GetConstraint(spec, "Number");
        bool isNeg = string.Equals(GetConstraint(spec, "Polarity"), "Neg", StringComparison.OrdinalIgnoreCase)
            || (negs.Count > 0 && rng.NextDouble() < 0.15);

        string requestedTemplate = spec.ClauseType is "Intransitive" or "Copular" or "Transitive"
            ? spec.ClauseType
            : project.ClauseTemplate.ToString();

        string? voiceConstraint = GetConstraint(spec, "Voice");
        bool forcePassive = string.Equals(voiceConstraint, "Passive", StringComparison.OrdinalIgnoreCase);
        bool forbidPassive = string.Equals(voiceConstraint, "Active", StringComparison.OrdinalIgnoreCase);
        bool isPassive = requestedTemplate == "Transitive"
            && relHead is null
            && mood != "Imp"
            && (forcePassive || (!forbidPassive && cfg.AllowPassive && depth == 0 && rng.NextDouble() < 0.22));

        int maxDepth = Math.Max(0, cfg.MaxClauseDepth);
        string? embeddedConstraint = GetConstraint(spec, "Embedded");
        bool forceEmbedded = string.Equals(embeddedConstraint, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(embeddedConstraint, "Complement", StringComparison.OrdinalIgnoreCase);
        bool canEmbed = depth < maxDepth
            && requestedTemplate == "Transitive"
            && !isPassive
            && relHead is null
            && mood == "Decl"
            && (forceEmbedded || (cfg.AllowEmbeddedClauses && comps.Count > 0 && !string.Equals(embeddedConstraint, "false", StringComparison.OrdinalIgnoreCase) && rng.NextDouble() < 0.28));

        bool hasDirectObject = requestedTemplate == "Transitive" && !isPassive && !canEmbed;
        var cases = ResolveAlignmentCases(cfg.Alignment, hasDirectObject);

        (SyntaxNode? Node, string Person, string Number, string HeadLemma, string? HeadClass, bool IsPron) BuildNp(
            string rolePrefix,
            string gramCase,
            string? avoidLemma = null,
            bool allowPronouns = true,
            bool allowRelative = true)
        {
            var eligibleProns = avoidLemma is null
                ? prons
                : prons.Where(p => !string.Equals(p.Lemma, avoidLemma, StringComparison.OrdinalIgnoreCase)).ToList();

            if (allowPronouns && eligibleProns.Count > 0 && rng.NextDouble() < 0.35)
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
                return (new SyntaxNode("PRON", pron.Lemma, pFeats), person, number, pron.Lemma, pronClass, true);
            }

            var pool = avoidLemma is null
                ? nouns
                : nouns.Where(n => !string.Equals(n.Lemma, avoidLemma, StringComparison.OrdinalIgnoreCase)).ToList();
            var noun = Pick(pool.Count > 0 ? pool : nouns);
            string nounLemma = noun?.Lemma ?? "person";
            string? headClass = string.IsNullOrWhiteSpace(noun?.LexClass) ? null : noun!.LexClass!.Trim();
            string num = constrainedNumber ?? (rng.NextDouble() < 0.35 ? "Pl" : "Sing");

            SyntaxNode? detNode = null;
            if (dets.Count > 0)
            {
                bool isMass = string.Equals(nounLemma, "water", StringComparison.OrdinalIgnoreCase);
                var compatDets = string.Equals(num, "Pl", StringComparison.OrdinalIgnoreCase) || isMass
                    ? dets.Where(d => !d.Lemma.Equals("a", StringComparison.OrdinalIgnoreCase) && !d.Lemma.Equals("an", StringComparison.OrdinalIgnoreCase)).ToList()
                    : dets;
                var det = Pick(compatDets.Count > 0 ? compatDets : dets);
                if (det is not null)
                {
                    var detFeats = new List<Feat> { new("Role", "Det"), new("Number", num) };
                    if (!string.IsNullOrEmpty(headClass)) detFeats.Add(new("Class", headClass));
                    detNode = new SyntaxNode("DET", det.Lemma, detFeats);
                }
            }

            SyntaxNode? adjNode = null;
            if (spec.ApproxWords >= 4 && adjs.Count > 0 && rng.NextDouble() < 0.65)
            {
                var adj = Pick(adjs)!;
                var adjFeats = new List<Feat> { new("Role", "Mod"), new("Number", num) };
                if (!string.IsNullOrEmpty(headClass)) adjFeats.Add(new("Class", headClass));
                adjNode = new SyntaxNode("ADJ", adj.Lemma, adjFeats);
            }

            var nFeats = new List<Feat>
            {
                new("Role", rolePrefix),
                new("Person", "3"),
                new("Number", num),
                new("Case", gramCase)
            };
            if (!string.IsNullOrEmpty(headClass)) nFeats.Add(new("Class", headClass));
            var nounNode = new SyntaxNode("N", nounLemma, nFeats);

            SyntaxNode? relCp = null;
            string? relConstraint = GetConstraint(spec, "Relative");
            bool forceRel = string.Equals(relConstraint, "true", StringComparison.OrdinalIgnoreCase) && rolePrefix == "Subj";
            bool wantRel = allowRelative
                && depth < maxDepth
                && relHead is null
                && rolePrefix == "Subj"
                && (forceRel || (cfg.AllowRelativeClauses && rels.Count + comps.Count > 0 && !string.Equals(relConstraint, "false", StringComparison.OrdinalIgnoreCase) && rng.NextDouble() < 0.25));

            if (wantRel)
            {
                var relWord = rels.FirstOrDefault() ?? comps.FirstOrDefault();
                var innerClause = BuildPhraseClause(
                    new SentenceSpec("Transitive", 3, new Dictionary<string, string>
                    {
                        ["Tense"] = tense,
                        ["Voice"] = "Active",
                        ["Relative"] = "false",
                        ["Embedded"] = "false"
                    }),
                    project,
                    rng,
                    depth + 1,
                    (nounLemma, num, headClass));
                var relChildren = new List<SyntaxNode>();
                if (cfg.AdpositionType == AdpositionOrder.Postposition && project.WordOrder == WordOrder.SOV)
                {
                    relChildren.Add(innerClause);
                    if (relWord is not null) relChildren.Add(new SyntaxNode(relWord.Pos, relWord.Lemma, [new Feat("Role", "Rel")]));
                }
                else
                {
                    if (relWord is not null) relChildren.Add(new SyntaxNode(relWord.Pos, relWord.Lemma, [new Feat("Role", "Rel")]));
                    relChildren.Add(innerClause);
                }
                relCp = new SyntaxNode("CP", "RelCP", [new Feat("Role", "RelCP")], relChildren);
            }

            var children = new List<SyntaxNode>();
            if (cfgMap.TryGetValue("NP", out var customNp))
            {
                foreach (var sym in customNp)
                {
                    if (sym == "DET" && detNode is not null) children.Add(detNode);
                    else if (sym == "ADJ" && adjNode is not null) children.Add(adjNode);
                    else if (sym == "N") children.Add(nounNode);
                    else if ((sym == "REL" || sym == "CP") && relCp is not null) children.Add(relCp);
                }
                if (!children.Contains(nounNode)) children.Add(nounNode);
                if (detNode is not null && !children.Contains(detNode)) children.Insert(0, detNode);
                if (adjNode is not null && !children.Contains(adjNode)) children.Add(adjNode);
                if (relCp is not null && !children.Contains(relCp)) children.Add(relCp);
            }
            else
            {
                if (detNode is not null && cfg.DeterminerOrder == ModifierOrder.BeforeNoun) children.Add(detNode);
                if (relCp is not null && cfg.AdpositionType == AdpositionOrder.Postposition && project.WordOrder == WordOrder.SOV) children.Add(relCp);
                if (adjNode is not null && cfg.AdjectiveOrder == ModifierOrder.BeforeNoun) children.Add(adjNode);
                children.Add(nounNode);
                if (adjNode is not null && cfg.AdjectiveOrder == ModifierOrder.AfterNoun) children.Add(adjNode);
                if (detNode is not null && cfg.DeterminerOrder == ModifierOrder.AfterNoun) children.Add(detNode);
                if (relCp is not null && !(cfg.AdpositionType == AdpositionOrder.Postposition && project.WordOrder == WordOrder.SOV)) children.Add(relCp);
            }

            var np = new SyntaxNode("NP", $"{rolePrefix}NP", [new Feat("Role", $"{rolePrefix}NP")], children);
            return (np, "3", num, nounLemma, headClass, false);
        }

        var subj = relHead is not null
            ? ((SyntaxNode?)null, "3", relHead.Value.Number, relHead.Value.HeadLemma, relHead.Value.HeadClass, false)
            : BuildNp("Subj", cases.SubjCase, null, !string.Equals(GetConstraint(spec, "Relative"), "true", StringComparison.OrdinalIgnoreCase), true);

        (SyntaxNode? Node, string Person, string Number, string HeadLemma, string? HeadClass, bool IsPron)? obj = null;
        SyntaxNode? cpObj = null;
        SyntaxNode? agentPp = null;

        if (canEmbed)
        {
            var compLex = comps.FirstOrDefault();
            var embeddedClause = BuildPhraseClause(
                new SentenceSpec("Transitive", Math.Max(3, spec.ApproxWords - 2), new Dictionary<string, string>
                {
                    ["Tense"] = tense,
                    ["Voice"] = "Active",
                    ["Embedded"] = "false"
                }),
                project,
                rng,
                depth + 1,
                null);
            var cpChildren = new List<SyntaxNode>();
            if (cfg.AdpositionType == AdpositionOrder.Postposition && project.WordOrder == WordOrder.SOV)
            {
                cpChildren.Add(embeddedClause);
                if (compLex is not null) cpChildren.Add(new SyntaxNode("COMP", compLex.Lemma, [new Feat("Role", "Comp")]));
            }
            else
            {
                if (compLex is not null) cpChildren.Add(new SyntaxNode("COMP", compLex.Lemma, [new Feat("Role", "Comp")]));
                cpChildren.Add(embeddedClause);
            }
            cpObj = new SyntaxNode("CP", "CompCP", [new Feat("Role", "CompCP")], cpChildren);
        }
        else if (isPassive)
        {
            if (spec.ApproxWords >= 5 && preps.Count > 0 && nouns.Count > 0)
            {
                var agentPrep = preps.FirstOrDefault(p => string.Equals(p.Lemma, "by", StringComparison.OrdinalIgnoreCase)) ?? Pick(preps)!;
                var agentNp = BuildNp("Obj", cases.ObliqueCase, subj.Item4, true, false);
                var prepLeaf = new SyntaxNode(agentPrep.Pos, agentPrep.Lemma, [new Feat("Role", "Prep")]);
                var ppChildren = cfg.AdpositionType == AdpositionOrder.Postposition
                    ? new[] { agentNp.Node!, prepLeaf }
                    : new[] { prepLeaf, agentNp.Node! };
                agentPp = new SyntaxNode("PP", "AgentPP", [new Feat("Role", "PP")], ppChildren);
            }
        }
        else if (requestedTemplate == "Transitive")
        {
            obj = BuildNp("Obj", cases.ObjCase, subj.Item4, true, false);
        }

        bool isCopular = requestedTemplate == "Copular";
        var verbPool = isCopular
            ? verbs.Where(v => string.Equals(v.Lemma, "be", StringComparison.OrdinalIgnoreCase)).ToList()
            : verbs.Where(v => !string.Equals(v.Lemma, "be", StringComparison.OrdinalIgnoreCase)).ToList();
        var chosenVerbLex = isCopular
            ? verbs.FirstOrDefault(v => string.Equals(v.Lemma, "be", StringComparison.OrdinalIgnoreCase))
            : Pick(verbPool.Count > 0 ? verbPool : verbs);
        string chosenVerb = chosenVerbLex?.Lemma ?? (isCopular ? "be" : "see");
        string? verbClass = string.IsNullOrWhiteSpace(chosenVerbLex?.LexClass) ? null : chosenVerbLex!.LexClass!.Trim();

        var beLex = verbs.FirstOrDefault(v => string.Equals(v.Lemma, "be", StringComparison.OrdinalIgnoreCase));
        bool wantFutAux = string.Equals(tense, "Fut", StringComparison.OrdinalIgnoreCase)
            && auxes.Any(a => string.Equals(a.Lemma, "will", StringComparison.OrdinalIgnoreCase))
            && mood != "Imp";
        var doAux = auxes.FirstOrDefault(a => string.Equals(a.Lemma, "do", StringComparison.OrdinalIgnoreCase));
        SyntaxNode? advNode = advs.Count > 0 && spec.ApproxWords >= 5 && rng.NextDouble() < 0.45
            ? new SyntaxNode("ADV", Pick(advs)!.Lemma, [new Feat("Role", "AdvMod")])
            : null;

        string effSubjPerson = mood == "Imp" ? "2" : subj.Item2;
        string effSubjNumber = mood == "Imp" ? "Sing" : subj.Item3;

        void AppendPolypersonal(List<Feat> feats)
        {
            if (!cfg.PolypersonalAgreement) return;
            feats.Add(new("SubjAgr", FormatAgrCode(effSubjPerson, effSubjNumber)));
            if (obj is not null)
            {
                feats.Add(new("ObjectPerson", obj.Value.Person));
                feats.Add(new("ObjectNumber", obj.Value.Number));
                feats.Add(new("ObjAgr", FormatAgrCode(obj.Value.Person, obj.Value.Number)));
                if (!string.IsNullOrEmpty(obj.Value.HeadClass)) feats.Add(new("ObjectClass", obj.Value.HeadClass!));
            }
        }

        SyntaxNode? frontedAuxOrVerb = null;
        var vpChildren = new List<SyntaxNode>();
        bool wantInversion = mood == "YNQuestion" && cfg.QuestionStrategy == QuestionStrategy.Inversion;

        if (isPassive && beLex is not null && mood != "Imp")
        {
            if (wantFutAux)
            {
                var willNode = new SyntaxNode("AUX", "will", [new Feat("Role", "Aux"), new Feat("Tense", "Pres")]);
                if (wantInversion) frontedAuxOrVerb = willNode;
                else vpChildren.Add(willNode);
                if (isNeg && negs.Count > 0) vpChildren.Add(new SyntaxNode("NEG", negs[0].Lemma, [new Feat("Role", "Neg")]));
                vpChildren.Add(new SyntaxNode("V", beLex.Lemma, [new Feat("Role", "PassiveAux"), new Feat("Finiteness", "Bare")]));
            }
            else
            {
                var beFeats = new List<Feat>
                {
                    new("Role", "PassiveAux"),
                    new("Tense", tense),
                    new("Person", effSubjPerson),
                    new("Number", effSubjNumber)
                };
                if (string.Equals(tense, "Pres", StringComparison.OrdinalIgnoreCase) && effSubjPerson == "3" && effSubjNumber == "Sing")
                    beFeats.Add(new("Agreement", "3sg"));
                var beNode = new SyntaxNode("V", beLex.Lemma, beFeats);
                if (wantInversion) frontedAuxOrVerb = beNode;
                else vpChildren.Add(beNode);
                if (isNeg && negs.Count > 0) vpChildren.Add(new SyntaxNode("NEG", negs[0].Lemma, [new Feat("Role", "Neg")]));
            }
            if (advNode is not null) vpChildren.Add(advNode);
            var partFeats = new List<Feat>
            {
                new("Role", "Verb"),
                new("Voice", "Passive"),
                new("Aspect", "Part"),
                new("Finiteness", "Part")
            };
            if (!string.IsNullOrEmpty(verbClass)) partFeats.Add(new("Class", verbClass));
            if (!string.IsNullOrEmpty(subj.Item5)) partFeats.Add(new("SubjectClass", subj.Item5));
            vpChildren.Add(new SyntaxNode("V", chosenVerb, partFeats));
        }
        else if (mood == "Imp")
        {
            bool isBe = string.Equals(chosenVerb, "be", StringComparison.OrdinalIgnoreCase);
            if (isNeg && negs.Count > 0 && !isBe && doAux is not null)
            {
                vpChildren.Add(new SyntaxNode("AUX", doAux.Lemma, [new Feat("Role", "Aux"), new Feat("Mood", "Imp"), new Feat("Finiteness", "Bare")]));
                vpChildren.Add(new SyntaxNode("NEG", negs[0].Lemma, [new Feat("Role", "Neg")]));
            }
            if (advNode is not null) vpChildren.Add(advNode);
            var impFeats = new List<Feat>
            {
                new("Role", isCopular ? "Copula" : "Verb"),
                new("Mood", "Imp"),
                new("Finiteness", "Bare"),
                new("Person", "2")
            };
            if (!string.IsNullOrEmpty(verbClass)) impFeats.Add(new("Class", verbClass));
            AppendPolypersonal(impFeats);
            vpChildren.Add(new SyntaxNode("V", chosenVerb, impFeats));
            if (isNeg && negs.Count > 0 && isBe)
                vpChildren.Add(new SyntaxNode("NEG", negs[0].Lemma, [new Feat("Role", "Neg")]));
        }
        else if (wantFutAux)
        {
            var willNode = new SyntaxNode("AUX", "will", [new Feat("Role", "Aux"), new Feat("Tense", "Pres")]);
            if (wantInversion) frontedAuxOrVerb = willNode;
            else vpChildren.Add(willNode);
            if (isNeg && negs.Count > 0) vpChildren.Add(new SyntaxNode("NEG", negs[0].Lemma, [new Feat("Role", "Neg")]));
            if (advNode is not null) vpChildren.Add(advNode);
            var vBareFeats = new List<Feat> { new("Role", "Verb"), new("Finiteness", "Bare") };
            if (!string.IsNullOrEmpty(verbClass)) vBareFeats.Add(new("Class", verbClass));
            AppendPolypersonal(vBareFeats);
            vpChildren.Add(new SyntaxNode("V", chosenVerb, vBareFeats));
        }
        else if (((isNeg && negs.Count > 0) || wantInversion) && !string.Equals(chosenVerb, "be", StringComparison.OrdinalIgnoreCase) && doAux is not null)
        {
            var auxFeats = new List<Feat>
            {
                new("Role", "Aux"),
                new("Tense", tense),
                new("Person", effSubjPerson),
                new("Number", effSubjNumber)
            };
            if (string.Equals(tense, "Pres", StringComparison.OrdinalIgnoreCase) && effSubjPerson == "3" && effSubjNumber == "Sing")
                auxFeats.Add(new("Agreement", "3sg"));
            var doNode = new SyntaxNode("AUX", doAux.Lemma, auxFeats);
            if (wantInversion) frontedAuxOrVerb = doNode;
            else vpChildren.Add(doNode);
            if (isNeg && negs.Count > 0) vpChildren.Add(new SyntaxNode("NEG", negs[0].Lemma, [new Feat("Role", "Neg")]));
            if (advNode is not null) vpChildren.Add(advNode);
            var vBareFeats = new List<Feat> { new("Role", "Verb"), new("Finiteness", "Bare") };
            if (!string.IsNullOrEmpty(verbClass)) vBareFeats.Add(new("Class", verbClass));
            AppendPolypersonal(vBareFeats);
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
                new("Person", effSubjPerson),
                new("Number", effSubjNumber)
            };
            if (string.Equals(tense, "Pres", StringComparison.OrdinalIgnoreCase) && effSubjPerson == "3" && effSubjNumber == "Sing")
                verbFeats.Add(new("Agreement", "3sg"));
            if (isPassive)
            {
                verbFeats.Add(new("Voice", "Passive"));
                verbFeats.Add(new("Aspect", "Part"));
            }
            if (!string.IsNullOrEmpty(verbClass)) verbFeats.Add(new("Class", verbClass));
            if (!string.IsNullOrEmpty(subj.Item5)) verbFeats.Add(new("SubjectClass", subj.Item5));
            AppendPolypersonal(verbFeats);
            var finiteV = new SyntaxNode("V", chosenVerb, verbFeats);
            if (wantInversion && isBe) frontedAuxOrVerb = finiteV;
            else vpChildren.Add(finiteV);
            if (isNeg && negs.Count > 0 && isBe) vpChildren.Add(new SyntaxNode("NEG", negs[0].Lemma, [new Feat("Role", "Neg")]));
            if (advNode is not null && isBe) vpChildren.Add(advNode);
        }

        SyntaxNode? vNode = vpChildren.Count switch
        {
            0 => null,
            1 => vpChildren[0],
            _ => new SyntaxNode("VP", "VP", [new Feat("Role", "VP")], vpChildren)
        };

        var predFeats = new List<Feat> { new("Role", "Pred"), new("Number", effSubjNumber) };
        if (!string.IsNullOrEmpty(subj.Item5)) predFeats.Add(new("Class", subj.Item5));

        string? proDropConstraint = GetConstraint(spec, "ProDrop");
        bool forbidProDrop = string.Equals(proDropConstraint, "false", StringComparison.OrdinalIgnoreCase);
        bool dropSubj = mood == "Imp" || (cfg.ProDrop && subj.Item6 && !forbidProDrop);
        bool dropObj = obj is not null && obj.Value.IsPron && cfg.ProDrop && cfg.PolypersonalAgreement && !forbidProDrop;

        var roles = new Dictionary<string, SyntaxNode>();
        if (!dropSubj && subj.Item1 is not null) roles["S"] = subj.Item1;
        if (vNode is not null) roles["V"] = vNode;

        if (cpObj is not null)
            roles["O"] = cpObj;
        else if (isCopular)
            roles["O"] = new SyntaxNode("ADJ", Pick(adjs)?.Lemma ?? "small", predFeats);
        else if (obj?.Node is not null && !dropObj)
            roles["O"] = obj.Value.Node;

        string[] canonicalOrder = project.WordOrder switch
        {
            WordOrder.SOV => ["S", "O", "V"],
            WordOrder.VSO => ["V", "S", "O"],
            WordOrder.VOS => ["V", "O", "S"],
            WordOrder.OVS => ["O", "V", "S"],
            WordOrder.OSV => ["O", "S", "V"],
            _ => ["S", "V", "O"]
        };

        string[] clauseOrder = cfgMap.TryGetValue("CLAUSE", out var customClause)
            ? customClause
            : wantInversion && frontedAuxOrVerb is null
                ? ["V", "S", "O"]
                : canonicalOrder;

        var clauseFeats = new List<Feat>();
        if (depth > 0) clauseFeats.Add(new("Role", "EmbeddedClause"));
        if (mood != "Decl") clauseFeats.Add(new("Mood", mood));
        if (isPassive) clauseFeats.Add(new("Voice", "Passive"));
        if (cfg.Alignment != AlignmentSystem.NomAcc) clauseFeats.Add(new("Alignment", cfg.Alignment.ToString()));

        var rootChildren = new List<SyntaxNode>();
        if (frontedAuxOrVerb is not null) rootChildren.Add(frontedAuxOrVerb);

        bool appendedPp = false;
        SyntaxNode? extraPp = agentPp;
        if (extraPp is null && !canEmbed && spec.ApproxWords >= 6 && preps.Count > 0 && nouns.Count > 0)
        {
            var prep = Pick(preps)!;
            var pobj = BuildNp("Obj", cases.ObliqueCase, subj.Item4, true, false);
            var prepLeaf = new SyntaxNode(prep.Pos, prep.Lemma, [new Feat("Role", "Prep")]);
            IReadOnlyList<SyntaxNode> ppChildren = cfgMap.TryGetValue("PP", out var customPp)
                ? customPp.SelectMany(sym => sym == "NP" ? new[] { pobj.Node! } : (sym is "PREP" or "POSTP") ? new[] { prepLeaf } : Array.Empty<SyntaxNode>()).ToList()
                : (cfg.AdpositionType == AdpositionOrder.Postposition
                    ? [pobj.Node!, prepLeaf]
                    : [prepLeaf, pobj.Node!]);
            extraPp = new SyntaxNode("PP", "PP", [new Feat("Role", "PP")], ppChildren);
        }

        foreach (var r in clauseOrder)
        {
            if (roles.TryGetValue(r, out var rn))
                rootChildren.Add(rn);
            else if (r == "PP" && extraPp is not null && !appendedPp)
            {
                rootChildren.Add(extraPp);
                appendedPp = true;
            }
            else if (r == "CP" && cpObj is not null && !rootChildren.Contains(cpObj))
                rootChildren.Add(cpObj);
        }
        if (extraPp is not null && !appendedPp)
            rootChildren.Add(extraPp);

        if (mood == "YNQuestion" && cfg.QuestionStrategy == QuestionStrategy.Particle && qParts.Count > 0)
        {
            var qLeaf = new SyntaxNode(qParts[0].Pos, qParts[0].Lemma, [new Feat("Role", "QPart")]);
            if (project.WordOrder is WordOrder.VSO or WordOrder.VOS)
                rootChildren.Insert(0, qLeaf);
            else
                rootChildren.Add(qLeaf);
        }

        return new SyntaxNode("CLAUSE", isPassive ? "Passive" : requestedTemplate, clauseFeats, rootChildren);
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

    public virtual (IReadOnlyList<(string Id, string Gloss, IReadOnlyList<string> Phones)> Morphemes, bool IsIrregular, string? Spelling) InflectSingleMorphemes(
        string lemma,
        string pos,
        IReadOnlyList<Feat>? features,
        LanguageProject project)
    {
        var lex = project.Lexemes.FirstOrDefault(l => string.Equals(l.Lemma, lemma, StringComparison.Ordinal))
            ?? project.Lexemes.FirstOrDefault(l => string.Equals(l.Lemma, lemma, StringComparison.OrdinalIgnoreCase));
        var (ur, matchedFeatures, isIrregular) = ResolveIrregularStem(lex, lemma, features);
        var morphemes = new List<(string Id, string Gloss, IReadOnlyList<string> Phones)>
        {
            (isIrregular ? "STEM_IRREG" : "STEM", lemma, SplitPhones(ur))
        };
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
                var currentFlat = morphemes.SelectMany(m => m.Phones).ToList();

                if (!string.IsNullOrWhiteSpace(rule.Condition))
                {
                    if (currentFlat.Count == 0 && !rule.Condition.Contains("class=", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var anchor = currentFlat.Count == 0
                        ? ""
                        : (kind is AffixType.Prefix or AffixType.Infix ? currentFlat[0] : currentFlat[^1]);
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
                    int targetIdx = morphemes.FindIndex(m => m.Id is "STEM" or "STEM_IRREG");
                    if (targetIdx < 0) targetIdx = 0;
                    var stem = morphemes[targetIdx];
                    var stemPhones = stem.Phones.ToList();
                    int splitIdx = stemPhones.FindIndex(p => PhonologyEngine.IsVowel(p, project));
                    if (splitIdx < 0) splitIdx = Math.Min(1, stemPhones.Count);

                    var replacement = new List<(string Id, string Gloss, IReadOnlyList<string> Phones)>();
                    if (splitIdx > 0) replacement.Add((stem.Id, stem.Gloss, stemPhones.Take(splitIdx).ToList()));
                    replacement.Add(($"AFX_{rule.Name}", rule.Name, infixPhones));
                    if (splitIdx < stemPhones.Count) replacement.Add((splitIdx > 0 ? $"{stem.Id}_CONT" : stem.Id, stem.Gloss, stemPhones.Skip(splitIdx).ToList()));
                    morphemes.RemoveAt(targetIdx);
                    morphemes.InsertRange(targetIdx, replacement);
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
                    if (prePhones.Count > 0) morphemes.Insert(0, ($"AFX_{rule.Name}_PRE", $"{rule.Name}.PRE", prePhones));
                    if (sufPhones.Count > 0) morphemes.Add(($"AFX_{rule.Name}_SUF", $"{rule.Name}.SUF", sufPhones));
                }
                else if (kind == AffixType.Template)
                {
                    int targetIdx = morphemes.FindIndex(m => m.Id is "STEM" or "STEM_IRREG");
                    if (targetIdx < 0) targetIdx = 0;
                    var stem = morphemes[targetIdx];
                    var radicals = stem.Phones.Where(p => !PhonologyEngine.IsVowel(p, project)).ToList();
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
                    morphemes[targetIdx] = ($"TEMPLATE_{rule.Name}", $"{stem.Gloss}.{rule.Name}", templated);
                }
                else if (kind == AffixType.Prefix)
                {
                    var affixPhones = SplitPhones(rule.Affix.Replace("-", "", StringComparison.Ordinal));
                    morphemes.Insert(0, ($"AFX_{rule.Name}", rule.Name, affixPhones));
                }
                else
                {
                    var affixPhones = SplitPhones(rule.Affix.Replace("-", "", StringComparison.Ordinal));
                    morphemes.Add(($"AFX_{rule.Name}", rule.Name, affixPhones));
                }

                handledFeatures.Add(rule.FeatureName);
            }
        }
        return (morphemes, isIrregular, lex?.Spelling);
    }

    public virtual (List<string> Phones, bool IsIrregular, string? Spelling) InflectSingleDetailed(
        string lemma,
        string pos,
        IReadOnlyList<Feat>? features,
        LanguageProject project)
    {
        var (morphemes, isIrregular, spelling) = InflectSingleMorphemes(lemma, pos, features, project);
        return (morphemes.SelectMany(m => m.Phones).ToList(), isIrregular, spelling);
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

    public override (IReadOnlyList<(string Id, string Gloss, IReadOnlyList<string> Phones)> Morphemes, bool IsIrregular, string? Spelling) InflectSingleMorphemes(
        string lemma,
        string pos,
        IReadOnlyList<Feat>? features,
        LanguageProject project)
    {
        var (morphemes, isIrreg, spelling) = base.InflectSingleMorphemes(lemma, pos, features, project);
        if (string.Equals(pos, "N", StringComparison.OrdinalIgnoreCase) &&
            features is not null &&
            features.Any(f => string.Equals(f.Name, "Number", StringComparison.OrdinalIgnoreCase) && string.Equals(f.Value, "Pl", StringComparison.OrdinalIgnoreCase)))
        {
            var stem = ResolveStemPhones(lemma, project);
            if (stem.Count >= 2)
            {
                var list = morphemes.ToList();
                list.Insert(0, ("REDUP_PL", "PL.REDUP", stem.Take(2).ToList()));
                return (list, isIrreg, spelling);
            }
        }
        return (morphemes, isIrreg, spelling);
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
