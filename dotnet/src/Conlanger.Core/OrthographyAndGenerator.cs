using System.Text;
using System.Text.RegularExpressions;
using Conlanger.SDK;

namespace Conlanger.Core;

public class OrthographyEngine : IOrthographyRenderer
{
    public virtual string Id => "standard-orthography";
    public virtual string Name => "Rule-Driven Orthography (Default)";
    public virtual string Description => "Context-sensitive grapheme rules, exceptions, and capitalization.";

    public virtual OrthographyResult Render(IReadOnlyList<IReadOnlyList<string>> surfaceWords, LanguageProject project)
    {
        var activeRules = project.OrthographyRules
            .Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.Phone))
            .OrderByDescending(r => r.Phone.Length)
            .ToList();

        var renderedWords = new List<string>(surfaceWords.Count);
        var tokens = new List<GraphemeToken>();

        for (int wordIdx = 0; wordIdx < surfaceWords.Count; wordIdx++)
        {
            var wordPhones = surfaceWords[wordIdx];
            var joinedWord = string.Concat(wordPhones);

            var exception = project.OrthographyExceptions.FirstOrDefault(e => e.Input == joinedWord);
            List<string> graphemeUnits;
            string wordText;

            if (exception is not null)
            {
                wordText = exception.Output;
                graphemeUnits = exception.Output.Select(c => c.ToString()).ToList();
            }
            else
            {
                graphemeUnits = new List<string>(wordPhones.Count);
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

                    var grapheme = match?.Grapheme ?? p;
                    if (!string.IsNullOrEmpty(grapheme))
                        graphemeUnits.Add(grapheme);
                }
                wordText = string.Concat(graphemeUnits);
            }

            if (project.Capitalization == CapitalizationMode.AllWords ||
                (project.Capitalization == CapitalizationMode.SentenceInitial && wordIdx == 0))
            {
                if (wordText.Length > 0)
                    wordText = char.ToUpperInvariant(wordText[0]) + wordText[1..];
            }

            renderedWords.Add(wordText);

            for (int gIdx = 0; gIdx < graphemeUnits.Count; gIdx++)
            {
                string pos = graphemeUnits.Count == 1 ? "isolated"
                    : gIdx == 0 ? "initial"
                    : gIdx == graphemeUnits.Count - 1 ? "final"
                    : "medial";
                tokens.Add(new GraphemeToken(graphemeUnits[gIdx].ToLowerInvariant(), graphemeUnits[gIdx], wordIdx, pos));
            }
        }

        return new OrthographyResult(string.Join(" ", renderedWords), tokens);
    }
}

public sealed class IpaBroadOrthography : OrthographyEngine
{
    public override string Id => "ipa-broad";
    public override string Name => "IPA Broad Transcription (Phase 4)";
    public override string Description => "Renders surface phonemic output wrapped in /.../ slashes for linguistic inspection.";

    public override OrthographyResult Render(IReadOnlyList<IReadOnlyList<string>> surfaceWords, LanguageProject project)
    {
        var baseResult = base.Render(surfaceWords, project);
        var ipaWords = surfaceWords
            .Select(w => string.Concat(w))
            .Where(w => !string.IsNullOrEmpty(w))
            .ToList();
        string text = ipaWords.Count > 0 ? $"/{string.Join(" ", ipaWords)}/" : "";
        return new OrthographyResult(text, baseResult.Tokens);
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

    public static IReadOnlyList<string> GenerateWords(LanguageProject project, int count, int syllablesPerWord, int seed = 42, PipelineRegistry? registry = null)
    {
        var rng = new Random(seed);
        var vowels = project.Phonemes.Where(p => p.Type == "V").ToList();
        var onsets = project.Phonemes.Where(p => p.Type == "C" && p.AllowOnset).ToList();
        var codas = project.Phonemes.Where(p => p.Type == "C" && p.AllowCoda).ToList();
        var allCons = project.Phonemes.Where(p => p.Type == "C").ToList();
        if (vowels.Count == 0 || allCons.Count == 0) return Array.Empty<string>();

        var reg = registry ?? PipelineRegistry.CreateDefaultWithPlugins();
        var orth = reg.ResolveOrthography(project.Pipeline?.OrthographyRendererId);
        var pluginRules = reg.ResolvePhonoRules(project.Pipeline?.EnabledPhonoPluginRules);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<string>(count);
        int maxAttempts = Math.Max(60, count * 40);

        for (int attempt = 0; attempt < maxAttempts && results.Count < count; attempt++)
        {
            var syllablePieces = new List<List<string>>();
            var flatPhones = new List<string>();

            for (int s = 0; s < Math.Max(1, syllablesPerWord); s++)
            {
                var tmpl = PickWeightedTemplate(project.SyllableTemplates, rng);
                var syl = new List<string>();
                bool optional = false;
                bool seenVowel = false;

                foreach (char rawCh in tmpl.Pattern)
                {
                    if (rawCh == '(') { optional = true; continue; }
                    if (rawCh == ')') { optional = false; continue; }
                    if (optional && rng.NextDouble() < 0.5) continue;

                    char slot = char.ToUpperInvariant(rawCh);
                    if (slot == 'V')
                    {
                        seenVowel = true;
                        var v = PickWeightedPhoneme(vowels, rng);
                        if (v is not null) syl.Add(v.Symbol);
                    }
                    else if (slot == 'C')
                    {
                        var pool = !seenVowel ? (onsets.Count > 0 ? onsets : allCons) : (codas.Count > 0 ? codas : allCons);
                        var c = PickWeightedPhoneme(pool, rng);
                        if (c is not null) syl.Add(c.Symbol);
                    }
                    else
                    {
                        syl.Add(rawCh.ToString());
                    }
                }
                if (syl.Count > 0)
                {
                    syllablePieces.Add(syl);
                    flatPhones.AddRange(syl);
                }
            }

            if (flatPhones.Count == 0) continue;
            var derived = PhonologyEngine.Derive(flatPhones, project, pluginRules);
            if (derived.PhonotacticIssues.Count > 0) continue;

            string word;
            if (project.GeneratorOptions.HyphenateSyllables)
            {
                word = string.Join("-", syllablePieces.Select(sp => orth.Render(new[] { sp }, project).Text)).ToLowerInvariant();
            }
            else
            {
                word = orth.Render(new[] { derived.SurfacePhones }, project).Text.ToLowerInvariant();
            }

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
    IReadOnlyList<string> Issues,
    SyntaxTree Tree
);

public sealed class LangEngine
{
    public PipelineRegistry Registry { get; }

    public LangEngine(PipelineRegistry? registry = null)
    {
        Registry = registry ?? PipelineRegistry.CreateDefaultWithPlugins();
    }

    public ISyntaxGenerator ResolveSyntax(LanguageProject project) =>
        Registry.ResolveSyntax(project.Pipeline?.SyntaxGeneratorId);

    public IMorphologyRealizer ResolveMorphology(LanguageProject project) =>
        Registry.ResolveMorphology(project.Pipeline?.MorphologyRealizerId);

    public IOrthographyRenderer ResolveOrthography(LanguageProject project) =>
        Registry.ResolveOrthography(project.Pipeline?.OrthographyRendererId);

    public IReadOnlyList<IPhonoRule> ResolvePluginRules(LanguageProject project) =>
        Registry.ResolvePhonoRules(project.Pipeline?.EnabledPhonoPluginRules);

    public SentenceGenerationResult GenerateSentence(
        LanguageProject project,
        int seed = 42,
        int approxWords = 4,
        IReadOnlyDictionary<string, string>? constraints = null)
    {
        var rng = new Random(seed);
        var spec = new SentenceSpec(project.ClauseTemplate.ToString(), approxWords, constraints);

        var syntax = ResolveSyntax(project);
        var morphology = ResolveMorphology(project);
        var orthography = ResolveOrthography(project);
        var pluginRules = ResolvePluginRules(project);

        var tree = syntax.Generate(spec, project, rng);
        var urWords = morphology.RealizeWords(tree, project);

        var surfaceWords = new List<IReadOnlyList<string>>(urWords.Count);
        var allIssues = new List<string>();
        foreach (var word in urWords)
        {
            var derived = PhonologyEngine.Derive(word, project, pluginRules);
            surfaceWords.Add(derived.SurfacePhones);
            foreach (var issue in derived.PhonotacticIssues)
                allIssues.Add($"{string.Concat(derived.SurfacePhones)}: {issue}");
        }

        var orth = orthography.Render(surfaceWords, project);
        return new SentenceGenerationResult(orth.Text, orth.Tokens, surfaceWords, allIssues, tree);
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

        var seenPhon = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in project.Phonemes)
        {
            if (!seenPhon.Add(p.Symbol))
                issues.Add(new(DiagnosticSeverity.Warning, "Phonology", $"Duplicate phoneme symbol '{p.Symbol}'."));
        }

        if (project.SyllableTemplates.Count == 0)
            issues.Add(new(DiagnosticSeverity.Warning, "Phonotactics", "No syllable templates defined; WordGenerator falls back to CV."));
        if (project.Lexemes.Count == 0)
            issues.Add(new(DiagnosticSeverity.Error, "Morphology", "Lexicon is empty; sentence generation needs at least one lexeme."));

        var seenLemmas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lex in project.Lexemes)
        {
            if (!seenLemmas.Add(lex.Lemma))
                issues.Add(new(DiagnosticSeverity.Error, "Morphology", $"Duplicate lexeme lemma '{lex.Lemma}'."));

            var phones = lex.UR.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (phones.Length == 0)
            {
                issues.Add(new(DiagnosticSeverity.Warning, "Morphology", $"Lexeme '{lex.Lemma}' has empty UR."));
            }
            else
            {
                foreach (var ph in phones)
                {
                    if (seenPhon.Count > 0 && !seenPhon.Contains(ph))
                        issues.Add(new(DiagnosticSeverity.Warning, "Phonology", $"Lexeme '{lex.Lemma}' UR uses unknown phone '{ph}'."));
                }
                foreach (var pi in PhonologyEngine.ValidateWordPhonotactics(phones, project))
                    issues.Add(new(DiagnosticSeverity.Warning, "Phonotactics", $"'{lex.Lemma}' ({lex.UR}): {pi}"));
            }
        }

        if (!project.Lexemes.Any(l => string.Equals(l.Pos, "N", StringComparison.OrdinalIgnoreCase)))
            issues.Add(new(DiagnosticSeverity.Warning, "Syntax", "No Noun (N) lexemes in lexicon; syntax falls back to defaults."));
        if (project.ClauseTemplate != ClauseTemplate.Copular && !project.Lexemes.Any(l => string.Equals(l.Pos, "V", StringComparison.OrdinalIgnoreCase)))
            issues.Add(new(DiagnosticSeverity.Warning, "Syntax", "No Verb (V) lexemes in lexicon; syntax falls back to defaults."));

        foreach (var pat in project.Phonotactics.ForbiddenPatterns)
        {
            if (string.IsNullOrWhiteSpace(pat)) continue;
            try { _ = new Regex(pat); }
            catch
            {
                issues.Add(new(DiagnosticSeverity.Error, "Phonotactics", $"Invalid forbidden regex pattern '{pat}'."));
            }
        }

        var mappedPhones = project.OrthographyRules.Where(r => r.Enabled).Select(r => r.Phone).ToHashSet(StringComparer.Ordinal);
        foreach (var p in project.Phonemes)
        {
            if (p.Symbol.Any(ch => ch < 0x20 || ch > 0x7E) && !mappedPhones.Contains(p.Symbol))
                issues.Add(new(DiagnosticSeverity.Warning, "Orthography", $"IPA symbol '{p.Symbol}' has no active orthography mapping rule."));
        }

        return issues;
    }
}
