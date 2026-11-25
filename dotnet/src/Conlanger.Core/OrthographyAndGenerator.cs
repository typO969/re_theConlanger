using System.Text;
using System.Text.RegularExpressions;
using Conlanger.SDK;

namespace Conlanger.Core;

public class OrthographyEngine : IOrthographyRenderer
{
    public virtual string Id => "standard-orthography";
    public virtual string Name => "Rule-Driven Orthography (Default)";
    public virtual string Description => "Context-sensitive grapheme rules, exceptions, and capitalization.";

    protected static List<string> MapWordDetailed(IReadOnlyList<string> wordPhones, LanguageProject project, IReadOnlyList<OrthRuleEntry> activeRules, out string wordText)
    {
        var joinedRaw = string.Concat(wordPhones);
        var cleanWord = string.Concat(wordPhones.Select(PhonologyEngine.StripStressMark));

        var exception = project.OrthographyExceptions.FirstOrDefault(e => e.Input == joinedRaw)
            ?? project.OrthographyExceptions.FirstOrDefault(e => PhonologyEngine.StripStressMark(e.Input) == cleanWord || e.Input.Replace("ˈ", "", StringComparison.Ordinal) == cleanWord);

        List<string> graphemeUnits;
        if (exception is not null)
        {
            wordText = exception.Output;
            graphemeUnits = exception.Output.Select(c => c.ToString()).ToList();
        }
        else
        {
            graphemeUnits = new List<string>(wordPhones.Count);
            var cleanPhones = wordPhones.Select(PhonologyEngine.StripStressMark).ToList();
            for (int i = 0; i < cleanPhones.Count; i++)
            {
                var p = cleanPhones[i];
                var prev = i > 0 ? cleanPhones[i - 1] : null;
                var next = i < cleanPhones.Count - 1 ? cleanPhones[i + 1] : null;

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

        return graphemeUnits;
    }

    protected static string ApplyWordCapitalization(string wordText, int wordIdx, CapitalizationMode mode)
    {
        if ((mode == CapitalizationMode.AllWords || (mode == CapitalizationMode.SentenceInitial && wordIdx == 0)) && wordText.Length > 0)
            return char.ToUpperInvariant(wordText[0]) + wordText[1..];
        return wordText;
    }

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
            var graphemeUnits = MapWordDetailed(surfaceWords[wordIdx], project, activeRules, out var wordText);
            wordText = ApplyWordCapitalization(wordText, wordIdx, project.Capitalization);
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

    public virtual OrthographyResult RenderWords(IReadOnlyList<RealizedWordContext> words, LanguageProject project) =>
        Render(words.Select(w => w.SurfacePhones).ToList(), project);
}

public sealed class MorphophonemicOrthography : OrthographyEngine
{
    public override string Id => "morphophonemic-orthography";
    public override string Name => "Morphophonemic / Deep Orthography (English-Style)";
    public override string Description => "Uses lexical historical spellings + morphological spelling rules alongside surface phonological exceptions.";

    private static bool HasFeat(IReadOnlyList<Feat>? feats, string name, string value) =>
        feats is not null && feats.Any(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.Value, value, StringComparison.OrdinalIgnoreCase));

    private static string InflectRegularSpelling(string baseSpelling, RealizedWordContext ctx)
    {
        if (ctx.Features is null || ctx.Features.Count == 0) return baseSpelling;

        bool isPluralN = string.Equals(ctx.Pos, "N", StringComparison.OrdinalIgnoreCase) && HasFeat(ctx.Features, "Number", "Pl");
        bool is3sgPres = (string.Equals(ctx.Pos, "V", StringComparison.OrdinalIgnoreCase) || string.Equals(ctx.Pos, "AUX", StringComparison.OrdinalIgnoreCase))
            && HasFeat(ctx.Features, "Tense", "Pres")
            && HasFeat(ctx.Features, "Person", "3")
            && HasFeat(ctx.Features, "Number", "Sing");
        bool isPastV = (string.Equals(ctx.Pos, "V", StringComparison.OrdinalIgnoreCase) || string.Equals(ctx.Pos, "AUX", StringComparison.OrdinalIgnoreCase))
            && HasFeat(ctx.Features, "Tense", "Past");

        if (isPluralN || is3sgPres)
        {
            if (Regex.IsMatch(baseSpelling, @"(s|x|z|ch|sh)$", RegexOptions.IgnoreCase))
                return $"{baseSpelling}es";
            if (Regex.IsMatch(baseSpelling, @"[^aeiou]y$", RegexOptions.IgnoreCase))
                return $"{baseSpelling[..^1]}ies";
            return $"{baseSpelling}s";
        }

        if (isPastV)
        {
            if (baseSpelling.EndsWith("e", StringComparison.OrdinalIgnoreCase))
                return $"{baseSpelling}d";
            if (Regex.IsMatch(baseSpelling, @"[^aeiou]y$", RegexOptions.IgnoreCase))
                return $"{baseSpelling[..^1]}ied";
            return $"{baseSpelling}ed";
        }

        return baseSpelling;
    }

    public override OrthographyResult RenderWords(IReadOnlyList<RealizedWordContext> words, LanguageProject project)
    {
        if (words.Count == 0) return new OrthographyResult("", Array.Empty<GraphemeToken>());

        var activeRules = project.OrthographyRules
            .Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.Phone))
            .OrderByDescending(r => r.Phone.Length)
            .ToList();

        var renderedWords = new List<string>(words.Count);
        var tokens = new List<GraphemeToken>();

        for (int wordIdx = 0; wordIdx < words.Count; wordIdx++)
        {
            var ctx = words[wordIdx];
            var joinedRaw = string.Concat(ctx.SurfacePhones);
            var cleanSurface = string.Concat(ctx.SurfacePhones.Select(PhonologyEngine.StripStressMark));

            var exception = project.OrthographyExceptions.FirstOrDefault(e => e.Input == joinedRaw)
                ?? project.OrthographyExceptions.FirstOrDefault(e => e.Input.Replace("ˈ", "", StringComparison.Ordinal) == cleanSurface);

            string wordText;
            List<string> graphemeUnits;
            if (exception is not null)
            {
                wordText = exception.Output;
                graphemeUnits = exception.Output.Select(c => c.ToString()).ToList();
            }
            else if (!ctx.IsIrregular && !string.IsNullOrWhiteSpace(ctx.Spelling))
            {
                wordText = InflectRegularSpelling(ctx.Spelling.Trim(), ctx);
                graphemeUnits = wordText.Select(c => c.ToString()).ToList();
            }
            else
            {
                graphemeUnits = MapWordDetailed(ctx.SurfacePhones, project, activeRules, out wordText);
            }

            wordText = ApplyWordCapitalization(wordText, wordIdx, project.Capitalization);
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

    public override OrthographyResult RenderWords(IReadOnlyList<RealizedWordContext> words, LanguageProject project) =>
        Render(words.Select(w => w.SurfacePhones).ToList(), project);
}

public static class WordGenerator
{
    private static Phoneme? PickWeightedPhoneme(IReadOnlyList<Phoneme> pool, Random rng)
    {
        if (pool.Count == 0) return null;
        int total = pool.Sum(p => Math.Max(0, p.Weight));
        if (total <= 0) return pool[rng.Next(pool.Count)];
        int roll = rng.Next(total);
        int acc = 0;
        foreach (var item in pool)
        {
            acc += Math.Max(0, item.Weight);
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
        var realizedWords = morphology.RealizeDetailed(tree, project);

        var surfaceWords = new List<IReadOnlyList<string>>(realizedWords.Count);
        var wordContexts = new List<RealizedWordContext>(realizedWords.Count);
        var allIssues = new List<string>();
        foreach (var rw in realizedWords)
        {
            var derived = PhonologyEngine.Derive(rw.Phones, project, pluginRules);
            surfaceWords.Add(derived.SurfacePhones);
            wordContexts.Add(new RealizedWordContext(derived.SurfacePhones, rw.Lemma, rw.Pos, rw.Features, rw.Spelling, rw.IsIrregular));
            foreach (var issue in derived.PhonotacticIssues)
                allIssues.Add($"{string.Concat(derived.SurfacePhones)}: {issue}");
        }

        var orth = orthography.RenderWords(wordContexts, project);
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

            var phones = MorphologyEngine.SplitPhones(lex.UR);
            if (phones.Count == 0)
            {
                issues.Add(new(DiagnosticSeverity.Warning, "Morphology", $"Lexeme '{lex.Lemma}' has empty UR."));
            }
            else
            {
                foreach (var ph in phones)
                {
                    var cleanPh = PhonologyEngine.StripStressMark(ph);
                    if (seenPhon.Count > 0 && !seenPhon.Contains(cleanPh))
                        issues.Add(new(DiagnosticSeverity.Warning, "Phonology", $"Lexeme '{lex.Lemma}' UR uses unknown phone '{cleanPh}'."));
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
