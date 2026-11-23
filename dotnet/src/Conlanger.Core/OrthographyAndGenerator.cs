using System.Text;
using Conlanger.SDK;

namespace Conlanger.Core;

public sealed class OrthographyEngine : IOrthographyRenderer
{
    public OrthographyResult Render(IReadOnlyList<IReadOnlyList<string>> surfaceWords, LanguageProject project)
    {
        var activeRules = project.OrthographyRules.Where(r => r.Enabled).ToList();
        var renderedWords = new List<string>(surfaceWords.Count);
        var tokens = new List<GraphemeToken>();

        for (int wordIdx = 0; wordIdx < surfaceWords.Count; wordIdx++)
        {
            var wordPhones = surfaceWords[wordIdx];
            var graphemeUnits = new List<string>(wordPhones.Count);

            for (int i = 0; i < wordPhones.Count; i++)
            {
                var p = wordPhones[i];
                var prev = i > 0 ? wordPhones[i - 1] : null;
                var next = i < wordPhones.Count - 1 ? wordPhones[i + 1] : null;

                var match = activeRules.FirstOrDefault(r =>
                    r.Phone == p &&
                    (string.IsNullOrEmpty(r.PrevContext) || r.PrevContext == prev) &&
                    (string.IsNullOrEmpty(r.NextContext) || r.NextContext == next)
                ) ?? activeRules.FirstOrDefault(r => r.Phone == p && string.IsNullOrEmpty(r.PrevContext) && string.IsNullOrEmpty(r.NextContext));

                graphemeUnits.Add(match?.Grapheme ?? p);
            }

            var sb = new StringBuilder(string.Concat(graphemeUnits));
            foreach (var ex in project.OrthographyExceptions)
            {
                if (!string.IsNullOrEmpty(ex.Input))
                    sb.Replace(ex.Input, ex.Output);
            }

            var finalWord = sb.ToString();
            if (project.Capitalization == CapitalizationMode.AllWords ||
                (project.Capitalization == CapitalizationMode.SentenceInitial && wordIdx == 0))
            {
                if (finalWord.Length > 0)
                    finalWord = char.ToUpperInvariant(finalWord[0]) + finalWord[1..];
            }

            renderedWords.Add(finalWord);

            for (int gIdx = 0; gIdx < graphemeUnits.Count; gIdx++)
            {
                string pos = graphemeUnits.Count == 1 ? "isolated"
                    : gIdx == 0 ? "initial"
                    : gIdx == graphemeUnits.Count - 1 ? "final"
                    : "medial";
                tokens.Add(new GraphemeToken(graphemeUnits[gIdx], graphemeUnits[gIdx], wordIdx, pos));
            }
        }

        return new OrthographyResult(string.Join(" ", renderedWords), tokens);
    }
}

public static class WordGenerator
{
    private static Phoneme? PickWeightedPhoneme(IReadOnlyList<Phoneme> pool, Random rng)
    {
        if (pool.Count == 0) return null;
        int total = pool.Sum(p => Math.Max(1, p.Weight));
        int roll = rng.Next(total);
        int acc = 0;
        foreach (var item in pool)
        {
            acc += Math.Max(1, item.Weight);
            if (roll < acc) return item;
        }
        return pool[^1];
    }

    private static SyllableTemplate PickWeightedTemplate(IReadOnlyList<SyllableTemplate> templates, Random rng)
    {
        if (templates.Count == 0) return new SyllableTemplate { Pattern = "CV", Weight = 1 };
        int total = templates.Sum(t => Math.Max(1, t.Weight));
        int roll = rng.Next(total);
        int acc = 0;
        foreach (var t in templates)
        {
            acc += Math.Max(1, t.Weight);
            if (roll < acc) return t;
        }
        return templates[^1];
    }

    public static IReadOnlyList<string> GenerateWords(LanguageProject project, int count, int syllablesPerWord, int seed = 42)
    {
        var rng = new Random(seed);
        var vowels = project.Phonemes.Where(p => p.Type == "V").ToList();
        var onsets = project.Phonemes.Where(p => p.Type == "C" && p.AllowOnset).ToList();
        var codas = project.Phonemes.Where(p => p.Type == "C" && p.AllowCoda).ToList();
        if (vowels.Count == 0 || onsets.Count == 0) return Array.Empty<string>();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<string>(count);
        int maxAttempts = Math.Max(40, count * 25);

        for (int attempt = 0; attempt < maxAttempts && results.Count < count; attempt++)
        {
            var syllableStrings = new List<string>();
            var flatPhones = new List<string>();

            for (int s = 0; s < Math.Max(1, syllablesPerWord); s++)
            {
                var tmpl = PickWeightedTemplate(project.SyllableTemplates, rng);
                var syl = new List<string>();
                for (int idx = 0; idx < tmpl.Pattern.Length; idx++)
                {
                    char slot = char.ToUpperInvariant(tmpl.Pattern[idx]);
                    if (slot == 'V')
                    {
                        var v = PickWeightedPhoneme(vowels, rng);
                        if (v is not null) syl.Add(v.Symbol);
                    }
                    else if (slot == 'C')
                    {
                        bool isCoda = idx == tmpl.Pattern.Length - 1 && tmpl.Pattern.IndexOf('V', StringComparison.OrdinalIgnoreCase) >= 0;
                        var pool = isCoda ? codas : onsets;
                        var c = PickWeightedPhoneme(pool, rng);
                        if (c is not null) syl.Add(c.Symbol);
                    }
                }
                if (syl.Count > 0)
                {
                    syllableStrings.Add(string.Concat(syl));
                    flatPhones.AddRange(syl);
                }
            }

            if (flatPhones.Count == 0) continue;
            if (PhonologyEngine.ValidateWordPhonotactics(flatPhones, project).Count > 0) continue;

            string word = project.GeneratorOptions.HyphenateSyllables
                ? string.Join("-", syllableStrings)
                : string.Concat(syllableStrings);

            if (!project.GeneratorOptions.AllowDuplicates && !seen.Add(word)) continue;
            seen.Add(word);
            results.Add(word);
        }

        return results;
    }
}

public sealed record SentenceGenerationResult(
    string Written,
    IReadOnlyList<GraphemeToken> Tokens,
    IReadOnlyList<IReadOnlyList<string>> SurfaceWords,
    IReadOnlyList<string> Issues
);

public sealed class LangEngine
{
    private readonly ISyntaxGenerator _syntax = new ConfigurableSyntaxGenerator();
    private readonly IMorphologyRealizer _morphology = new MorphologyEngine();
    private readonly IOrthographyRenderer _orthography = new OrthographyEngine();

    public SentenceGenerationResult GenerateSentence(LanguageProject project, int seed = 42, int approxWords = 4)
    {
        var rng = new Random(seed);
        var spec = new SentenceSpec(project.ClauseTemplate.ToString(), approxWords);
        var tree = _syntax.Generate(spec, project, rng);
        var urWords = _morphology.RealizeWords(tree, project);

        var surfaceWords = new List<IReadOnlyList<string>>(urWords.Count);
        var allIssues = new List<string>();
        foreach (var word in urWords)
        {
            var derived = PhonologyEngine.Derive(word, project);
            surfaceWords.Add(derived.SurfacePhones);
            foreach (var issue in derived.PhonotacticIssues)
                allIssues.Add($"{string.Concat(derived.SurfacePhones)}: {issue}");
        }

        var orth = _orthography.Render(surfaceWords, project);
        return new SentenceGenerationResult(orth.Text, orth.Tokens, surfaceWords, allIssues);
    }
}

public static class ProjectValidator
{
    public static IReadOnlyList<DiagnosticIssue> Validate(LanguageProject project)
    {
        var issues = new List<DiagnosticIssue>();
        if (project.Phonemes.Count(p => p.Type == "C") == 0)
            issues.Add(new(DiagnosticSeverity.Error, "Phonology", "No consonants defined in inventory."));
        if (project.Phonemes.Count(p => p.Type == "V") == 0)
            issues.Add(new(DiagnosticSeverity.Error, "Phonology", "No vowels defined in inventory."));
        if (project.SyllableTemplates.Count == 0)
            issues.Add(new(DiagnosticSeverity.Warning, "Phonotactics", "No syllable templates defined; WordGenerator falls back to CV."));
        if (project.Lexemes.Count == 0)
            issues.Add(new(DiagnosticSeverity.Error, "Morphology", "Lexicon is empty; sentence generation needs at least one lexeme."));
        if (!project.Lexemes.Any(l => l.Pos == "N"))
            issues.Add(new(DiagnosticSeverity.Warning, "Syntax", "No noun (N) lexemes in lexicon; syntax falls back to 'cat'."));
        if (!project.Lexemes.Any(l => l.Pos == "V" || l.Pos == "Cop"))
            issues.Add(new(DiagnosticSeverity.Warning, "Syntax", "No verb (V) or copula (Cop) lexemes in lexicon."));

        var known = project.Phonemes.Select(p => p.Symbol).ToHashSet(StringComparer.Ordinal);
        foreach (var lex in project.Lexemes)
        {
            foreach (var ph in lex.UR.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (known.Count > 0 && !known.Contains(ph))
                    issues.Add(new(DiagnosticSeverity.Warning, "Morphology", $"Lexeme '{lex.Lemma}' UR uses unknown phone '{ph}'."));
            }
        }

        foreach (var rule in project.PhonologyRules.Where(r => r.Enabled))
        {
            if (string.IsNullOrWhiteSpace(rule.Target))
                issues.Add(new(DiagnosticSeverity.Warning, "Phonology", $"Rule '{rule.Name}' has empty Target."));
        }

        return issues;
    }
}
