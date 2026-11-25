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
        "ɚ", "ɝː", "aː", "eː", "iː", "oː", "uː", "ɑː", "ɔː", "eɪ", "oʊ", "aɪ", "aʊ", "ɔɪ"
    };

    private static readonly HashSet<char> VowelBaseChars = new("aeiouɑɔɛɪʊyøœæəɨʌɒɐɯɤɚɝ");

    private static readonly HashSet<string> SibilantSymbols = new(StringComparer.Ordinal)
    {
        "s", "z", "ʃ", "ʒ", "tʃ", "dʒ"
    };

    private static readonly Dictionary<string, string> NasalPlaceFallback = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bilabial"] = "m",
        ["labiodental"] = "ɱ",
        ["dental"] = "n",
        ["alveolar"] = "n",
        ["postalveolar"] = "ɲ",
        ["palatal"] = "ɲ",
        ["velar"] = "ŋ"
    };

    public static string StripStressMark(string phone) => phone.TrimStart('ˈ');

    public static bool HasStressMark(string phone) => phone.StartsWith('ˈ');

    private static string CleanDiacritics(string phone) =>
        phone.Replace("ˈ", "", StringComparison.Ordinal).Replace("ʰ", "", StringComparison.Ordinal);

    public static bool IsVowel(string phone, LanguageProject project)
    {
        if (string.IsNullOrEmpty(phone)) return false;
        var clean = StripStressMark(phone);
        var custom = project.Phonemes.FirstOrDefault(p => StripStressMark(p.Symbol) == clean);
        if (custom is not null) return custom.Type == "V";
        if (KnownVowels.Contains(clean) || project.Vowels.Contains(clean)) return true;
        return clean.Length > 0 && VowelBaseChars.Contains(clean[0]);
    }

    public static int ResolveStressSyllableIndex(int syllableCount, StressPattern pattern) =>
        ResolveStressSyllableIndex(syllableCount, pattern, null);

    public static int ResolveStressSyllableIndex(int syllableCount, StressPattern pattern, IReadOnlyList<Syllable>? syllables)
    {
        if (syllableCount <= 0 || pattern == StressPattern.None) return -1;
        if (pattern == StressPattern.Lexical && syllables is not null)
        {
            for (int i = 0; i < syllables.Count; i++)
            {
                var s = syllables[i];
                if (s.Onset.Any(HasStressMark) || s.Nucleus.Any(HasStressMark) || s.Coda.Any(HasStressMark))
                    return i;
            }
            return -1;
        }
        return pattern switch
        {
            StressPattern.Initial => 0,
            StressPattern.Final => syllableCount - 1,
            StressPattern.Penultimate => syllableCount >= 2 ? syllableCount - 2 : 0,
            _ => -1
        };
    }

    public static HashSet<int> ComputeStressedPhoneIndices(IReadOnlyList<string> phones, LanguageProject project)
    {
        var stressed = new HashSet<int>();
        if (phones.Count == 0) return stressed;
        bool hasExplicitMark = phones.Any(HasStressMark);
        if (project.StressPattern == StressPattern.None && !hasExplicitMark) return stressed;

        var syls = Syllabify(phones, project);
        var effectivePattern = hasExplicitMark && project.StressPattern == StressPattern.None
            ? StressPattern.Lexical
            : project.StressPattern;
        int targetIdx = ResolveStressSyllableIndex(syls.Count, effectivePattern, syls);
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
        foreach (var raw in phones)
        {
            var cleanSym = StripStressMark(raw);
            var ph = project.Phonemes.FirstOrDefault(p => StripStressMark(p.Symbol) == cleanSym);
            if (ph is not null && !ph.AllowOnset) return false;
        }
        var cleanCluster = string.Concat(phones.Select(CleanDiacritics));
        if (project.Phonotactics.AllowedOnsets.Contains(cleanCluster)) return true;
        if (phones.Count > project.Phonotactics.MaxOnsetCluster) return false;
        if (phones.Count == 1) return true;
        return project.Phonotactics.AllowedOnsets.Count == 0 || project.Phonotactics.AllowedOnsets.Contains(cleanCluster);
    }

    public static bool IsCodaAllowed(IReadOnlyList<string> phones, LanguageProject project)
    {
        if (phones.Count == 0) return true;
        foreach (var raw in phones)
        {
            var cleanSym = StripStressMark(raw);
            var ph = project.Phonemes.FirstOrDefault(p => StripStressMark(p.Symbol) == cleanSym);
            if (ph is not null && !ph.AllowCoda) return false;
        }
        var cleanCluster = string.Concat(phones.Select(CleanDiacritics));
        if (project.Phonotactics.AllowedCodas.Contains(cleanCluster)) return true;
        if (phones.Count > project.Phonotactics.MaxCodaCluster) return false;
        if (phones.Count == 1) return true;
        return project.Phonotactics.AllowedCodas.Count == 0 || project.Phonotactics.AllowedCodas.Contains(cleanCluster);
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
                issues.Add($"Illegal onset '{string.Concat(s.Onset.Select(CleanDiacritics))}' in syllable {i + 1}");
            if (!IsCodaAllowed(s.Coda, project))
                issues.Add($"Illegal coda '{string.Concat(s.Coda.Select(CleanDiacritics))}' in syllable {i + 1}");
        }
        return issues;
    }

    private static Phoneme ResolvePhoneme(string phone, LanguageProject project)
    {
        var clean = StripStressMark(phone);
        return project.Phonemes.FirstOrDefault(p => StripStressMark(p.Symbol) == clean)
            ?? ProjectFactory.CreatePhoneme(clean, IsVowel(clean, project) ? "V" : "C");
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
        var clean = StripStressMark(phone);

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

            var ph = ResolvePhoneme(clean, project);

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
                bool sib = SibilantSymbols.Contains(clean) || ph.Features.Custom.Contains("sibilant", StringComparison.OrdinalIgnoreCase);
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
            var ph = ResolvePhoneme(clean, project);
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

    internal static bool MatchesCondition(
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
        var cleanActual = StripStressMark(actual);
        if (exp == "V") return IsVowel(cleanActual, project);
        if (exp == "C") return !IsVowel(cleanActual, project);
        if (exp.StartsWith('[') && exp.EndsWith(']'))
            return MatchesCondition(cleanActual, exp, project, stressedSet, actualIndex);
        return exp.StartsWith('ˈ') ? actual == exp : cleanActual == exp;
    }

    private static (HashSet<int> Starts, HashSet<int> Ends) ComputeSyllableBoundarySets(IReadOnlyList<string> phones, LanguageProject project)
    {
        var starts = new HashSet<int>();
        var ends = new HashSet<int>();
        var syls = Syllabify(phones, project);
        int offset = 0;
        foreach (var s in syls)
        {
            int len = s.Onset.Count + s.Nucleus.Count + s.Coda.Count;
            if (len > 0)
            {
                starts.Add(offset);
                ends.Add(offset + len - 1);
            }
            offset += len;
        }
        return (starts, ends);
    }

    private static string ResolveAlphaPlaceReplacement(
        string targetPhone,
        IReadOnlyList<string> phones,
        int pos,
        string? prevTarget,
        string? nextTarget,
        LanguageProject project)
    {
        var prevT = prevTarget?.Trim() ?? "";
        var nextT = nextTarget?.Trim() ?? "";
        string? condPhone = null;
        if (!string.IsNullOrEmpty(nextT) && nextT is not "#" and not "$" and not "." && pos + 1 < phones.Count)
            condPhone = phones[pos + 1];
        else if (!string.IsNullOrEmpty(prevT) && prevT is not "#" and not "$" and not "." && pos > 0)
            condPhone = phones[pos - 1];

        if (string.IsNullOrEmpty(condPhone)) return targetPhone;

        var targetPlace = ResolvePhoneme(condPhone, project).Features.Place.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(targetPlace)) return targetPhone;

        var cleanTarget = StripStressMark(targetPhone);
        var srcFeat = ResolvePhoneme(cleanTarget, project).Features;
        var srcManner = srcFeat.Manner.ToLowerInvariant();
        var srcVoicing = srcFeat.Voicing.ToLowerInvariant();
        bool keepStress = HasStressMark(targetPhone);

        var exactMatch = project.Phonemes.FirstOrDefault(p =>
            p.Type == "C" &&
            string.Equals(p.Features.Place, targetPlace, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrEmpty(srcManner) || string.Equals(p.Features.Manner, srcManner, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(srcVoicing) || string.Equals(p.Features.Voicing, srcVoicing, StringComparison.OrdinalIgnoreCase)));
        if (exactMatch is not null)
            return keepStress && !HasStressMark(exactMatch.Symbol) ? $"ˈ{exactMatch.Symbol}" : exactMatch.Symbol;

        if (srcManner == "nasal" || cleanTarget is "m" or "ɱ" or "n" or "ɲ" or "ŋ")
        {
            var mapped = NasalPlaceFallback.GetValueOrDefault(targetPlace, cleanTarget);
            return keepStress ? $"ˈ{mapped}" : mapped;
        }

        foreach (var kv in ProjectFactory.EnumerateKnownFeatures())
        {
            if (string.Equals(kv.Value.Place, targetPlace, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(srcManner) || string.Equals(kv.Value.Manner, srcManner, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(srcVoicing) || string.Equals(kv.Value.Voicing, srcVoicing, StringComparison.OrdinalIgnoreCase)))
            {
                return keepStress ? $"ˈ{kv.Key}" : kv.Key;
            }
        }

        return targetPhone;
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
        var prevT = rule.PrevPhone?.Trim() ?? "";
        var nextT = rule.NextPhone?.Trim() ?? "";
        bool needsBounds = prevT is "$" or "." || nextT is "$" or ".";
        var sylBounds = needsBounds ? ComputeSyllableBoundarySets(phones, project) : default;

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
                bool prevOk = prevT is "$" or "."
                    ? sylBounds.Starts.Contains(i)
                    : MatchesPhoneOrClass(prev, prevT, project, stressedSet, i - 1);
                bool nextOk = nextT is "$" or "."
                    ? sylBounds.Ends.Contains(i - 1)
                    : MatchesPhoneOrClass(next, nextT, project, stressedSet, i);

                if ((!string.IsNullOrEmpty(prevT) || !string.IsNullOrEmpty(nextT)) && prevOk && nextOk)
                    outList.AddRange(repPhones);
                if (i < phones.Count) outList.Add(phones[i]);
            }
            return outList;
        }

        bool isAlphaPlace = replacement == "[αplace]" || string.Equals(replacement, "[alpha-place]", StringComparison.OrdinalIgnoreCase);
        var result = new List<string>(phones.Count);
        var subPhones = isDeletion ? Array.Empty<string>() : replacement.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < phones.Count; i++)
        {
            var cur = phones[i];
            bool targetHit = MatchesPhoneOrClass(cur, target, project, stressedSet, i);
            bool prevOk = prevT is "$" or "."
                ? sylBounds.Starts.Contains(i)
                : MatchesPhoneOrClass(i > 0 ? phones[i - 1] : null, prevT, project, stressedSet, i - 1);
            bool nextOk = nextT is "$" or "."
                ? sylBounds.Ends.Contains(i)
                : MatchesPhoneOrClass(i < phones.Count - 1 ? phones[i + 1] : null, nextT, project, stressedSet, i + 1);
            bool ctxOk = prevOk && nextOk && MatchesCondition(cur, rule.Condition, project, stressedSet, i);

            if (targetHit && ctxOk)
            {
                if (isAlphaPlace)
                {
                    result.Add(ResolveAlphaPlaceReplacement(cur, phones, i, prevT, nextT, project));
                }
                else if (subPhones.Length > 0 && HasStressMark(cur) && !target.StartsWith('ˈ') && !HasStressMark(subPhones[0]))
                {
                    result.Add($"ˈ{subPhones[0]}");
                    for (int s = 1; s < subPhones.Length; s++) result.Add(subPhones[s]);
                }
                else
                {
                    result.AddRange(subPhones);
                }
            }
            else
            {
                result.Add(cur);
            }
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
        bool hasExplicitMark = cur.Any(HasStressMark);
        var effectivePattern = hasExplicitMark && project.StressPattern == StressPattern.None
            ? StressPattern.Lexical
            : project.StressPattern;
        int stressIdx = ResolveStressSyllableIndex(syllables.Count, effectivePattern, syllables);
        var issues = ValidateWordPhonotactics(cur, project);
        if (issues.Count > 0)
            steps.Add(new DerivationStep("Phonotactics", string.Join("; ", issues)));

        return new PhonologyDerivationResult(cur, syllables, stressIdx, steps, issues);
    }
}
