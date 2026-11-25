using System.Text.RegularExpressions;
using Conlanger.SDK;

namespace Conlanger.Core;

public sealed record Syllable(IReadOnlyList<string> Onset, IReadOnlyList<string> Nucleus, IReadOnlyList<string> Coda);

public sealed record DerivationStep(string Label, string Value);

public sealed record PhonologyDerivationResult(
    IReadOnlyList<string> SurfacePhones,
    IReadOnlyList<Syllable> Syllables,
    int StressIndex,
    IReadOnlyList<DerivationStep> Steps,
    IReadOnlyList<string> PhonotacticIssues
);

public static class PhonologyEngine
{
    private static readonly HashSet<string> KnownVowels = new(StringComparer.Ordinal)
    {
        "a", "e", "i", "o", "u", "ə", "ɛ", "ɔ", "æ", "ø", "œ", "y", "ɨ", "ɯ", "ʌ", "ɑ", "ɒ", "ɐ", "ɤ", "ɪ", "ʊ",
        "aː", "eː", "iː", "oː", "uː"
    };

    private static readonly HashSet<string> SibilantSymbols = new(StringComparer.Ordinal)
    {
        "s", "z", "ʃ", "ʒ", "tʃ", "dʒ"
    };

    public static bool IsVowel(string phone, LanguageProject project)
    {
        var clean = phone.TrimStart('ˈ');
        var custom = project.Phonemes.FirstOrDefault(p => p.Symbol == clean);
        if (custom is not null) return custom.Type == "V";
        return KnownVowels.Contains(clean) || project.Vowels.Contains(clean);
    }

    public static int ResolveStressSyllableIndex(int syllableCount, StressPattern pattern) => pattern switch
    {
        StressPattern.None => -1,
        _ when syllableCount <= 0 => -1,
        StressPattern.Initial => 0,
        StressPattern.Final => syllableCount - 1,
        StressPattern.Penultimate => syllableCount >= 2 ? syllableCount - 2 : 0,
        _ => -1
    };

    public static HashSet<int> ComputeStressedPhoneIndices(IReadOnlyList<string> phones, LanguageProject project)
    {
        var stressed = new HashSet<int>();
        if (project.StressPattern == StressPattern.None || phones.Count == 0) return stressed;

        var syls = Syllabify(phones, project);
        int targetIdx = ResolveStressSyllableIndex(syls.Count, project.StressPattern);
        if (targetIdx < 0) return stressed;

        int offset = 0;
        for (int i = 0; i < syls.Count; i++)
        {
            var s = syls[i];
            int total = s.Onset.Count + s.Nucleus.Count + s.Coda.Count;
            if (i == targetIdx)
            {
                for (int k = offset; k < offset + total; k++)
                    stressed.Add(k);
                break;
            }
            offset += total;
        }
        return stressed;
    }

    public static IReadOnlyList<Syllable> Syllabify(IReadOnlyList<string> wordPhones, LanguageProject project)
    {
        var syllables = new List<Syllable>();
        if (wordPhones.Count == 0) return syllables;

        var vowelIndices = new List<int>();
        for (int i = 0; i < wordPhones.Count; i++)
        {
            if (IsVowel(wordPhones[i], project))
                vowelIndices.Add(i);
        }

        if (vowelIndices.Count == 0)
        {
            syllables.Add(new Syllable(wordPhones.ToList(), Array.Empty<string>(), Array.Empty<string>()));
            return syllables;
        }

        var boundaries = new int[vowelIndices.Count + 1];
        boundaries[0] = 0;
        boundaries[vowelIndices.Count] = wordPhones.Count;

        for (int k = 0; k < vowelIndices.Count - 1; k++)
        {
            int v1 = vowelIndices[k];
            int v2 = vowelIndices[k + 1];
            int clusterLen = v2 - v1 - 1;
            if (clusterLen <= 0)
            {
                boundaries[k + 1] = v2;
                continue;
            }

            int bestSplit = v2;
            int maxOnset = Math.Max(0, project.Phonotactics.MaxOnsetCluster);
            for (int split = Math.Max(v1 + 1, v2 - maxOnset); split <= v2; split++)
            {
                var onsetSlice = wordPhones.Skip(split).Take(v2 - split).ToList();
                var codaSlice = wordPhones.Skip(v1 + 1).Take(split - (v1 + 1)).ToList();
                if (IsOnsetAllowed(onsetSlice, project) && IsCodaAllowed(codaSlice, project))
                {
                    bestSplit = split;
                    break;
                }
            }

            if (bestSplit == v2 && clusterLen >= 1 && project.Phonotactics.MaxOnsetCluster >= 1)
                bestSplit = v2 - 1;

            boundaries[k + 1] = bestSplit;
        }

        for (int s = 0; s < vowelIndices.Count; s++)
        {
            int start = boundaries[s];
            int nuc = vowelIndices[s];
            int end = boundaries[s + 1];
            syllables.Add(new Syllable(
                wordPhones.Skip(start).Take(nuc - start).ToList(),
                new[] { wordPhones[nuc] },
                wordPhones.Skip(nuc + 1).Take(end - (nuc + 1)).ToList()
            ));
        }

        return syllables;
    }

    public static bool IsOnsetAllowed(IReadOnlyList<string> phones, LanguageProject project)
    {
        if (phones.Count == 0) return true;
        if (phones.Count > project.Phonotactics.MaxOnsetCluster) return false;
        foreach (var sym in phones)
        {
            var ph = project.Phonemes.FirstOrDefault(p => p.Symbol == sym);
            if (ph is not null && !ph.AllowOnset) return false;
        }
        if (phones.Count == 1) return true;
        if (project.Phonotactics.AllowedOnsets.Count == 0) return true;
        return project.Phonotactics.AllowedOnsets.Contains(string.Concat(phones));
    }

    public static bool IsCodaAllowed(IReadOnlyList<string> phones, LanguageProject project)
    {
        if (phones.Count == 0) return true;
        if (phones.Count > project.Phonotactics.MaxCodaCluster) return false;
        foreach (var sym in phones)
        {
            var ph = project.Phonemes.FirstOrDefault(p => p.Symbol == sym);
            if (ph is not null && !ph.AllowCoda) return false;
        }
        if (phones.Count == 1) return true;
        if (project.Phonotactics.AllowedCodas.Count == 0) return true;
        return project.Phonotactics.AllowedCodas.Contains(string.Concat(phones));
    }

    public static IReadOnlyList<string> ValidateWordPhonotactics(IReadOnlyList<string> phones, LanguageProject project)
    {
        var issues = new List<string>();
        var joined = string.Concat(phones);
        foreach (var pat in project.Phonotactics.ForbiddenPatterns)
        {
            if (string.IsNullOrWhiteSpace(pat)) continue;
            try
            {
                if (Regex.IsMatch(joined, pat))
                    issues.Add($"Forbidden pattern /{pat}/ matched in '{joined}'");
            }
            catch
            {
                // Invalid regex caught by ProjectValidator
            }
        }

        var syllables = Syllabify(phones, project);
        for (int i = 0; i < syllables.Count; i++)
        {
            var s = syllables[i];
            if (!IsOnsetAllowed(s.Onset, project))
                issues.Add($"Illegal onset '{string.Concat(s.Onset)}' in syllable {i + 1}");
            if (!IsCodaAllowed(s.Coda, project))
                issues.Add($"Illegal coda '{string.Concat(s.Coda)}' in syllable {i + 1}");
        }
        return issues;
    }

    private static bool MatchesSingleClause(
        string phone,
        string clause,
        LanguageProject project,
        IReadOnlySet<int>? stressedSet,
        int? phoneIndex)
    {
        var body = clause.Trim();
        if (string.IsNullOrEmpty(body)) return true;

        if (body.StartsWith('+') || body.StartsWith('-'))
        {
            bool positive = body[0] == '+';
            var feat = body[1..].ToLowerInvariant();
            if (feat is "stress" or "stressed")
            {
                if (stressedSet is null || !phoneIndex.HasValue) return !positive;
                bool isStressed = stressedSet.Contains(phoneIndex.Value);
                return positive ? isStressed : !isStressed;
            }

            var ph = project.Phonemes.FirstOrDefault(p => p.Symbol == phone)
                ?? ProjectFactory.CreatePhoneme(phone, IsVowel(phone, project) ? "V" : "C");

            if (feat is "voice" or "voicing" or "voiced")
            {
                bool voiced = string.Equals(ph.Features.Voicing, "voiced", StringComparison.OrdinalIgnoreCase);
                return positive ? voiced : string.Equals(ph.Features.Voicing, "voiceless", StringComparison.OrdinalIgnoreCase);
            }
            if (feat is "round" or "rounded")
            {
                bool rounded = string.Equals(ph.Features.Roundness, "rounded", StringComparison.OrdinalIgnoreCase);
                return positive ? rounded : !rounded;
            }
            if (feat == "sibilant")
            {
                bool sib = SibilantSymbols.Contains(phone) || ph.Features.Custom.Contains("sibilant", StringComparison.OrdinalIgnoreCase);
                return positive ? sib : !sib;
            }
            if (feat == "sonorant")
            {
                bool son = ph.Type == "V" || ph.Features.Manner.ToLowerInvariant() is "nasal" or "approximant" or "trill" or "tap" or "lateral" or "vowel";
                return positive ? son : !son;
            }

            var allVals = $"{ph.Features.Manner} {ph.Features.Place} {ph.Features.Height} {ph.Features.Backness} {ph.Features.Custom}".ToLowerInvariant();
            return positive ? allVals.Contains(feat, StringComparison.Ordinal) : !allVals.Contains(feat, StringComparison.Ordinal);
        }

        if (body.Contains('='))
        {
            var ph = project.Phonemes.FirstOrDefault(p => p.Symbol == phone)
                ?? ProjectFactory.CreatePhoneme(phone, IsVowel(phone, project) ? "V" : "C");
            var kv = body.Split('=', 2);
            var k = kv[0].Trim().ToLowerInvariant();
            var v = kv[1].Trim().ToLowerInvariant();
            var actual = k switch
            {
                "voicing" => ph.Features.Voicing,
                "manner" => ph.Features.Manner,
                "place" => ph.Features.Place,
                "height" => ph.Features.Height,
                "backness" => ph.Features.Backness,
                "roundness" => ph.Features.Roundness,
                "custom" => ph.Features.Custom,
                _ => ""
            };
            return actual.Contains(v, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool MatchesCondition(
        string? phone,
        string? condition,
        LanguageProject project,
        IReadOnlySet<int>? stressedSet = null,
        int? phoneIndex = null)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        if (string.IsNullOrEmpty(phone)) return false;

        var trimmed = condition.Trim();
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            trimmed = trimmed[1..^1].Trim();
        if (string.IsNullOrEmpty(trimmed)) return true;

        var clauses = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return clauses.All(cl => MatchesSingleClause(phone, cl, project, stressedSet, phoneIndex));
    }

    private static bool MatchesPhoneOrClass(
        string? actual,
        string? expected,
        LanguageProject project,
        IReadOnlySet<int>? stressedSet = null,
        int? actualIndex = null)
    {
        if (string.IsNullOrWhiteSpace(expected)) return true;
        var exp = expected.Trim();
        if (exp == "#") return actual is null;
        if (actual is null) return false;
        if (exp == "V") return IsVowel(actual, project);
        if (exp == "C") return !IsVowel(actual, project);
        if (exp.StartsWith('[') && exp.EndsWith(']'))
            return MatchesCondition(actual, exp, project, stressedSet, actualIndex);
        return actual == exp;
    }

    public static IReadOnlyList<string> ApplyRule(
        IReadOnlyList<string> phones,
        PhonologyRuleSpec rule,
        LanguageProject project,
        IReadOnlySet<int>? stressedSet = null)
    {
        if (!rule.Enabled) return phones;

        var target = rule.Target.Trim();
        var replacement = rule.Replacement.Trim();
        var isDeletion = replacement is "∅" or "0" or "";
        var isEpenthesis = target is "∅" or "0" or "";

        if (isEpenthesis)
        {
            if (isDeletion) return phones;
            var repPhones = replacement.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var outList = new List<string>();
            for (int i = 0; i <= phones.Count; i++)
            {
                string? prev = i > 0 ? phones[i - 1] : null;
                string? next = i < phones.Count ? phones[i] : null;
                if ((!string.IsNullOrWhiteSpace(rule.PrevPhone) || !string.IsNullOrWhiteSpace(rule.NextPhone)) &&
                    MatchesPhoneOrClass(prev, rule.PrevPhone, project, stressedSet, i - 1) &&
                    MatchesPhoneOrClass(next, rule.NextPhone, project, stressedSet, i))
                {
                    outList.AddRange(repPhones);
                }
                if (i < phones.Count) outList.Add(phones[i]);
            }
            return outList;
        }

        var result = new List<string>(phones.Count);
        var subPhones = isDeletion ? Array.Empty<string>() : replacement.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < phones.Count; i++)
        {
            var cur = phones[i];
            string? prev = i > 0 ? phones[i - 1] : null;
            string? next = i < phones.Count - 1 ? phones[i + 1] : null;

            bool targetHit = MatchesPhoneOrClass(cur, target, project, stressedSet, i);
            bool ctxOk = MatchesPhoneOrClass(prev, rule.PrevPhone, project, stressedSet, i - 1)
                && MatchesPhoneOrClass(next, rule.NextPhone, project, stressedSet, i + 1)
                && MatchesCondition(cur, rule.Condition, project, stressedSet, i);

            if (targetHit && ctxOk)
                result.AddRange(subPhones);
            else
                result.Add(cur);
        }
        return result;
    }

    public static PhonologyDerivationResult Derive(
        IReadOnlyList<string> underlyingPhones,
        LanguageProject project,
        IEnumerable<IPhonoRule>? pluginRules = null)
    {
        IReadOnlyList<string> cur = underlyingPhones.ToList();
        var steps = new List<DerivationStep> { new("UR", string.Join(" ", cur)) };
        var stressedSet = ComputeStressedPhoneIndices(cur, project);

        foreach (var rule in project.PhonologyRules)
        {
            cur = ApplyRule(cur, rule, project, stressedSet);
            stressedSet = ComputeStressedPhoneIndices(cur, project);
            string label = string.IsNullOrWhiteSpace(rule.Name) ? $"{rule.Target}->{rule.Replacement}" : rule.Name;
            if (!rule.Enabled) label += " (disabled)";
            steps.Add(new DerivationStep(label, string.Join(" ", cur)));
        }

        if (pluginRules is not null)
        {
            foreach (var pr in pluginRules.Where(r => r.Enabled))
            {
                cur = pr.Apply(cur, project, stressedSet);
                stressedSet = ComputeStressedPhoneIndices(cur, project);
                steps.Add(new DerivationStep($"[Plugin] {pr.Name}", string.Join(" ", cur)));
            }
        }

        var syllables = Syllabify(cur, project);
        int stressIdx = ResolveStressSyllableIndex(syllables.Count, project.StressPattern);
        var issues = ValidateWordPhonotactics(cur, project);
        if (issues.Count > 0)
            steps.Add(new DerivationStep("Phonotactics", string.Join("; ", issues)));

        return new PhonologyDerivationResult(cur, syllables, stressIdx, steps, issues);
    }
}
