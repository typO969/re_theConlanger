using System.Text;
using System.Text.Json;
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

    private static readonly Regex ToneCombiningRegex = new(@"[\u0300\u0301\u0302\u0304\u0308\u030B\u030C\u030F]", RegexOptions.Compiled);
    private static readonly Regex ChaoToneRegex = new(@"[˥˦˧˨˩]+", RegexOptions.Compiled);
    private static readonly Regex SuprasegmentalStripRegex = new(@"[ˈʰ˥˦˧˨˩\u0300\u0301\u0302\u0304\u0308\u030B\u030C\u030F]", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> ChaoToTone = new(StringComparer.Ordinal)
    {
        ["˥"] = "H", ["˥˥"] = "H",
        ["˦"] = "MH", ["˦˦"] = "MH",
        ["˧"] = "M", ["˧˧"] = "M",
        ["˨"] = "ML", ["˨˨"] = "ML",
        ["˩"] = "L", ["˩˩"] = "L",
        ["˧˥"] = "LH", ["˨˦"] = "LH", ["˩˥"] = "LH",
        ["˥˩"] = "HL", ["˥˧"] = "HL", ["˧˩"] = "HL",
        ["˨˩˦"] = "LHL", ["˧˩˧"] = "LHL", ["˨˩˧"] = "LHL"
    };

    private static readonly Dictionary<char, string> DiacriticToTone = new()
    {
        ['\u0301'] = "H",
        ['\u030B'] = "H",
        ['\u0304'] = "M",
        ['\u0300'] = "L",
        ['\u030F'] = "L",
        ['\u030C'] = "LH",
        ['\u0302'] = "HL"
    };

    private static readonly Dictionary<string, string> ToneToChao = new(StringComparer.OrdinalIgnoreCase)
    {
        ["H"] = "˥", ["HIGH"] = "˥",
        ["MH"] = "˦",
        ["M"] = "˧", ["MID"] = "˧",
        ["ML"] = "˨",
        ["L"] = "˩", ["LOW"] = "˩",
        ["LH"] = "˧˥", ["RISING"] = "˧˥",
        ["HL"] = "˥˩", ["FALLING"] = "˥˩",
        ["LHL"] = "˨˩˦", ["DIPPING"] = "˨˩˦"
    };

    public static string StripStressMark(string phone) => phone.TrimStart('ˈ');

    public static string StripSuprasegmentals(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return "";
        var nfd = phone.Normalize(NormalizationForm.FormD);
        if (nfd.StartsWith('ˈ')) nfd = nfd[1..];
        nfd = ToneCombiningRegex.Replace(nfd, "");
        nfd = ChaoToneRegex.Replace(nfd, "");
        return nfd.Normalize(NormalizationForm.FormC);
    }

    public static bool HasStressMark(string phone) => phone.StartsWith('ˈ');

    public static bool HasChaoOrToneMark(string s) =>
        ChaoToneRegex.IsMatch(s) || ToneCombiningRegex.IsMatch(s.Normalize(NormalizationForm.FormD));

    public static string ExtractToneRaw(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return "";
        var m = ChaoToneRegex.Match(symbol);
        if (m.Success) return m.Value;
        var nfd = symbol.Normalize(NormalizationForm.FormD);
        foreach (char ch in nfd)
        {
            if (DiacriticToTone.ContainsKey(ch)) return ch.ToString();
        }
        return "";
    }

    public static string ExtractTone(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return "";
        var m = ChaoToneRegex.Match(symbol);
        if (m.Success)
            return ChaoToTone.TryGetValue(m.Value, out var t) ? t : m.Value;
        var nfd = symbol.Normalize(NormalizationForm.FormD);
        foreach (char ch in nfd)
        {
            if (DiacriticToTone.TryGetValue(ch, out var dt)) return dt;
        }
        return "";
    }

    public static string ApplyToneToPhone(string symbol, string targetTone)
    {
        var stressPrefix = HasStressMark(symbol) ? "ˈ" : "";
        var baseSym = StripSuprasegmentals(symbol);
        var trimmed = targetTone.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == "∅" || trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
            return $"{stressPrefix}{baseSym}";

        string suffix = Regex.IsMatch(trimmed, @"^[˥˦˧˨˩]+$")
            ? trimmed
            : (ToneToChao.TryGetValue(trimmed, out var chao) ? chao : trimmed);
        return $"{stressPrefix}{baseSym}{suffix}";
    }

    private static string CleanDiacritics(string phone) =>
        SuprasegmentalStripRegex.Replace(phone.Normalize(NormalizationForm.FormD), "").Normalize(NormalizationForm.FormC);

    public static bool IsVowel(string phone, LanguageProject project)
    {
        if (string.IsNullOrEmpty(phone)) return false;
        var clean = StripSuprasegmentals(phone);
        var custom = project.Phonemes.FirstOrDefault(p => StripSuprasegmentals(p.Symbol) == clean);
        if (custom is not null) return custom.Type == "V";
        if (KnownVowels.Contains(clean) || project.Vowels.Any(v => StripSuprasegmentals(v) == clean)) return true;
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
            var cleanSym = StripSuprasegmentals(raw);
            var ph = project.Phonemes.FirstOrDefault(p => StripSuprasegmentals(p.Symbol) == cleanSym);
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
            var cleanSym = StripSuprasegmentals(raw);
            var ph = project.Phonemes.FirstOrDefault(p => StripSuprasegmentals(p.Symbol) == cleanSym);
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
        var cleanStress = StripStressMark(phone);
        var cleanBase = StripSuprasegmentals(phone);
        return project.Phonemes.FirstOrDefault(p => StripStressMark(p.Symbol) == cleanStress)
            ?? project.Phonemes.FirstOrDefault(p => StripSuprasegmentals(p.Symbol) == cleanBase)
            ?? ProjectFactory.CreatePhoneme(cleanBase, IsVowel(cleanBase, project) ? "V" : "C");
    }

    private static bool IsToneContextSelector(string expected)
    {
        var trimmed = expected.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']')) return false;
        var inner = trimmed[1..^1].ToLowerInvariant();
        return inner.Contains("tone", StringComparison.Ordinal)
            || inner.Contains("high-tone", StringComparison.Ordinal)
            || inner.Contains("mid-tone", StringComparison.Ordinal)
            || inner.Contains("low-tone", StringComparison.Ordinal);
    }

    private static int FindAdjacentTbuIndex(IReadOnlyList<string> phones, int startIdx, int step, LanguageProject project)
    {
        for (int i = startIdx; i >= 0 && i < phones.Count; i += step)
        {
            if (!string.IsNullOrEmpty(ExtractTone(phones[i])) || IsVowel(phones[i], project))
                return i;
        }
        return -1;
    }

    private static bool MatchesSingleClause(
        string phone,
        string clause,
        LanguageProject project,
        IReadOnlySet<int>? stressedSet,
        int? phoneIndex,
        string? lexClass = null)
    {
        var body = clause.Trim();
        if (string.IsNullOrEmpty(body)) return true;
        var clean = StripSuprasegmentals(phone);

        if (body.StartsWith('+') || body.StartsWith('-'))
        {
            bool positive = body[0] == '+';
            var feat = body[1..].ToLowerInvariant();
            if (feat.StartsWith("class=", StringComparison.Ordinal))
            {
                var targetCls = feat["class=".Length..].Trim();
                var actualCls = (lexClass ?? "").Trim().ToLowerInvariant();
                return positive ? actualCls == targetCls : actualCls != targetCls;
            }
            if (feat is "stress" or "stressed")
            {
                if (stressedSet is null || !phoneIndex.HasValue) return !positive;
                bool isStressed = stressedSet.Contains(phoneIndex.Value);
                return positive ? isStressed : !isStressed;
            }
            if (feat is "high-tone" or "h-tone")
            {
                var t = ExtractTone(phone).ToUpperInvariant();
                return positive ? t == "H" : t != "H";
            }
            if (feat is "mid-tone" or "m-tone")
            {
                var t = ExtractTone(phone).ToUpperInvariant();
                return positive ? t == "M" : t != "M";
            }
            if (feat is "low-tone" or "l-tone")
            {
                var t = ExtractTone(phone).ToUpperInvariant();
                return positive ? t == "L" : t != "L";
            }

            if (string.IsNullOrEmpty(clean)) return false;
            var ph = ResolvePhoneme(phone, project);

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
            var kv = body.Split('=', 2);
            var k = kv[0].Trim().ToLowerInvariant();
            var v = kv[1].Trim().ToLowerInvariant();

            if (k == "class")
                return (lexClass ?? "").Trim().ToLowerInvariant() == v;

            if (k == "tone")
            {
                if (string.IsNullOrEmpty(phone)) return false;
                var tNorm = ExtractTone(phone).ToLowerInvariant();
                var tRaw = ExtractToneRaw(phone).ToLowerInvariant();
                if (string.IsNullOrEmpty(tNorm) && string.IsNullOrEmpty(tRaw))
                    return v is "none" or "∅";
                return tNorm == v || tRaw == v;
            }

            if (string.IsNullOrEmpty(clean)) return false;
            var ph = ResolvePhoneme(phone, project);
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
        int? phoneIndex = null,
        string? lexClass = null)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        var trimmed = condition.Trim();
        bool hasClassClause = trimmed.Contains("class=", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(phone) && !hasClassClause) return false;

        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            trimmed = trimmed[1..^1].Trim();
        if (string.IsNullOrEmpty(trimmed)) return true;

        var clauses = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return clauses.All(cl => MatchesSingleClause(phone ?? "", cl, project, stressedSet, phoneIndex, lexClass));
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
        var cleanStress = StripStressMark(actual);
        var cleanBase = StripSuprasegmentals(actual);
        if (exp == "V") return IsVowel(actual, project);
        if (exp == "C") return !IsVowel(actual, project);
        if (exp.StartsWith('[') && exp.EndsWith(']'))
            return MatchesCondition(actual, exp, project, stressedSet, actualIndex);
        if (exp.StartsWith('ˈ')) return actual == exp;
        if (HasChaoOrToneMark(exp)) return cleanStress == exp;
        return cleanBase == exp || cleanStress == exp;
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

    private static string ResolveAlphaHarmonyReplacement(
        string targetPhone,
        IReadOnlyList<string> phones,
        int pos,
        string prevTarget,
        string nextTarget,
        bool matchBack,
        bool matchRound,
        LanguageProject project)
    {
        bool regressiveOnly = string.IsNullOrEmpty(prevTarget)
            && !string.IsNullOrEmpty(nextTarget)
            && nextTarget is not "#" and not "$" and not ".";

        string? triggerPhone = null;
        if (regressiveOnly)
        {
            for (int j = pos + 1; j < phones.Count; j++)
            {
                if (IsVowel(phones[j], project)) { triggerPhone = phones[j]; break; }
            }
        }
        else
        {
            for (int j = pos - 1; j >= 0; j--)
            {
                if (IsVowel(phones[j], project)) { triggerPhone = phones[j]; break; }
            }
            if (triggerPhone is null)
            {
                for (int j = pos + 1; j < phones.Count; j++)
                {
                    if (IsVowel(phones[j], project)) { triggerPhone = phones[j]; break; }
                }
            }
        }
        if (string.IsNullOrEmpty(triggerPhone)) return targetPhone;

        var triggerFeat = ResolvePhoneme(triggerPhone, project).Features;
        var srcFeat = ResolvePhoneme(targetPhone, project).Features;

        var rawTriggerBack = triggerFeat.Backness.Trim().ToLowerInvariant();
        var triggerBack = rawTriggerBack == "central" ? "back" : rawTriggerBack;
        var triggerRound = triggerFeat.Roundness.Trim().ToLowerInvariant();

        var srcHeight = srcFeat.Height.Trim().ToLowerInvariant();
        var srcBackRaw = srcFeat.Backness.Trim().ToLowerInvariant();
        var srcBack = srcBackRaw == "central" ? "back" : srcBackRaw;
        var srcRound = srcFeat.Roundness.Trim().ToLowerInvariant();

        var desiredBack = matchBack && !string.IsNullOrEmpty(triggerBack) ? triggerBack : srcBack;
        var desiredRound = matchRound && !string.IsNullOrEmpty(triggerRound) ? triggerRound : srcRound;

        bool MatchesHarmony(PhonologicalFeatures f, bool allowLowBackFallback)
        {
            var h = f.Height.ToLowerInvariant();
            var bRaw = f.Backness.ToLowerInvariant();
            var b = bRaw == "central" && allowLowBackFallback ? "back" : bRaw;
            var r = f.Roundness.ToLowerInvariant();
            bool backOk = string.IsNullOrEmpty(desiredBack) || b == desiredBack || (desiredBack == "back" && bRaw == "central" && allowLowBackFallback);
            bool roundOk = string.IsNullOrEmpty(desiredRound) || r == desiredRound;
            if (allowLowBackFallback && srcHeight is "mid" or "low" && desiredRound == "unrounded")
                return h is "mid" or "low" && backOk && roundOk;
            return (string.IsNullOrEmpty(srcHeight) || h == srcHeight) && backOk && roundOk;
        }

        string ReapplyProsody(string sym)
        {
            var cleanNew = StripSuprasegmentals(sym);
            var stress = HasStressMark(targetPhone) ? "ˈ" : "";
            var toneRaw = ExtractToneRaw(targetPhone);
            return $"{stress}{cleanNew}{toneRaw}";
        }

        if (MatchesHarmony(srcFeat, true))
            return targetPhone;

        var invExact = project.Phonemes.FirstOrDefault(p => p.Type == "V" && MatchesHarmony(p.Features, false));
        if (invExact is not null) return ReapplyProsody(invExact.Symbol);

        var invHarmonic = project.Phonemes.FirstOrDefault(p => p.Type == "V" && MatchesHarmony(p.Features, true));
        if (invHarmonic is not null) return ReapplyProsody(invHarmonic.Symbol);

        if (srcHeight is "mid" or "low" && desiredBack == "back" && desiredRound == "unrounded")
        {
            bool hasGammaInInv = project.Phonemes.Any(p => StripSuprasegmentals(p.Symbol) == "ɤ");
            if (!hasGammaInInv) return ReapplyProsody("a");
        }
        if (srcHeight is "mid" or "low" && desiredBack == "front" && desiredRound == "unrounded")
        {
            bool hasEInInv = project.Phonemes.Any(p => StripSuprasegmentals(p.Symbol) == "e");
            if (hasEInInv || project.Phonemes.Count == 0) return ReapplyProsody("e");
        }

        foreach (var kv in ProjectFactory.EnumerateKnownFeatures())
        {
            if (string.Equals(kv.Value.Manner, "vowel", StringComparison.OrdinalIgnoreCase) && MatchesHarmony(kv.Value, false))
                return ReapplyProsody(kv.Key);
        }
        foreach (var kv in ProjectFactory.EnumerateKnownFeatures())
        {
            if (string.Equals(kv.Value.Manner, "vowel", StringComparison.OrdinalIgnoreCase) && MatchesHarmony(kv.Value, true))
                return ReapplyProsody(kv.Key);
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

        var lowerRep = replacement.ToLowerInvariant();
        bool isAlphaPlace = replacement == "[αplace]" || lowerRep == "[alpha-place]";
        bool isAlphaBack = lowerRep.Contains("αback", StringComparison.Ordinal) || lowerRep.Contains("alpha-back", StringComparison.Ordinal);
        bool isAlphaRound = lowerRep.Contains("αround", StringComparison.Ordinal) || lowerRep.Contains("alpha-round", StringComparison.Ordinal);
        var toneMatch = Regex.Match(replacement, @"^\[tone\s*=\s*([^\]]+)\]$", RegexOptions.IgnoreCase);

        bool carryStress = false;
        var result = new List<string>(phones.Count);
        var subPhones = isDeletion ? Array.Empty<string>() : replacement.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < phones.Count; i++)
        {
            var cur = phones[i];
            bool targetHit = MatchesPhoneOrClass(cur, target, project, stressedSet, i);

            string ApplyStress(string seg)
            {
                if (!carryStress) return seg;
                carryStress = false;
                return HasStressMark(seg) ? seg : $"ˈ{seg}";
            }

            bool prevOk;
            if (prevT is "$" or ".")
            {
                prevOk = sylBounds.Starts.Contains(i);
            }
            else if (!string.IsNullOrEmpty(prevT) && IsToneContextSelector(prevT))
            {
                int tbuIdx = FindAdjacentTbuIndex(phones, i - 1, -1, project);
                prevOk = MatchesPhoneOrClass(tbuIdx >= 0 ? phones[tbuIdx] : null, prevT, project, stressedSet, tbuIdx);
            }
            else
            {
                prevOk = MatchesPhoneOrClass(i > 0 ? phones[i - 1] : null, prevT, project, stressedSet, i - 1);
            }

            bool nextOk;
            if (nextT is "$" or ".")
            {
                nextOk = sylBounds.Ends.Contains(i);
            }
            else if (!string.IsNullOrEmpty(nextT) && IsToneContextSelector(nextT))
            {
                int tbuIdx = FindAdjacentTbuIndex(phones, i + 1, 1, project);
                nextOk = MatchesPhoneOrClass(tbuIdx >= 0 ? phones[tbuIdx] : null, nextT, project, stressedSet, tbuIdx);
            }
            else
            {
                nextOk = MatchesPhoneOrClass(i < phones.Count - 1 ? phones[i + 1] : null, nextT, project, stressedSet, i + 1);
            }

            bool ctxOk = prevOk && nextOk && MatchesCondition(cur, rule.Condition, project, stressedSet, i);

            if (targetHit && ctxOk)
            {
                if (isAlphaPlace)
                {
                    result.Add(ApplyStress(ResolveAlphaPlaceReplacement(cur, phones, i, prevT, nextT, project)));
                }
                else if (isAlphaBack || isAlphaRound)
                {
                    result.Add(ApplyStress(ResolveAlphaHarmonyReplacement(cur, phones, i, prevT, nextT, isAlphaBack, isAlphaRound, project)));
                }
                else if (toneMatch.Success)
                {
                    result.Add(ApplyStress(ApplyToneToPhone(cur, toneMatch.Groups[1].Value)));
                }
                else if (subPhones.Length == 0)
                {
                    if (HasStressMark(cur) && !target.StartsWith('ˈ'))
                        carryStress = true;
                }
                else if (subPhones.Length > 0 && HasStressMark(cur) && !target.StartsWith('ˈ') && !HasStressMark(subPhones[0]))
                {
                    carryStress = false;
                    result.Add($"ˈ{subPhones[0]}");
                    for (int s = 1; s < subPhones.Length; s++) result.Add(subPhones[s]);
                }
                else
                {
                    result.Add(ApplyStress(subPhones[0]));
                    for (int s = 1; s < subPhones.Length; s++) result.Add(subPhones[s]);
                }
            }
            else
            {
                result.Add(ApplyStress(cur));
            }
        }
        return result;
    }

    public static IReadOnlyList<string> ApplyRule(
        IReadOnlyList<string> phones,
        DiachronicRuleSpec rule,
        LanguageProject project,
        IReadOnlySet<int>? stressedSet = null) =>
        ApplyRule(phones, new PhonologyRuleSpec
        {
            Name = rule.Name,
            Target = rule.Target,
            Replacement = rule.Replacement,
            PrevPhone = rule.PrevPhone,
            NextPhone = rule.NextPhone,
            Condition = rule.Condition,
            Enabled = rule.Enabled
        }, project, stressedSet);

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

public sealed record DiachronicPresetSpec(string Id, string Name, string Description, IReadOnlyList<DiachronicRuleSpec> Rules);

public sealed record DiachronicStepTrace(string Era, string RuleName, string SurfaceForm);

public sealed record DiachronicCognateDiff(
    string ProtoLemma,
    string ProtoUr,
    string DaughterUr,
    string DaughterSurface,
    string DiffStatus
);

public sealed record DiachronicAffixDiff(string AffixName, string ProtoForm, string DaughterForm);

public sealed record DiachronicSimulationResult(
    string DaughterProjectName,
    IReadOnlyList<DiachronicStepTrace> StepTraces,
    IReadOnlyList<DiachronicCognateDiff> CognateDiffs,
    IReadOnlyList<DiachronicAffixDiff> AffixDiffs,
    IReadOnlyList<string> EmergentPhones,
    IReadOnlyList<string> LostPhones
);

public static class DiachronicEngine
{
    public static IReadOnlyList<DiachronicPresetSpec> GetPresets() => new[]
    {
        new DiachronicPresetSpec(
            "great-vowel-shift",
            "Great Vowel Shift (English)",
            "Historic raising and diphthongization of long middle English vowels (e.g., iː -> aɪ, uː -> aʊ, eː -> iː).",
            new[]
            {
                new DiachronicRuleSpec { Name = "Long i raising/diphthongization", Era = "EMnE", Target = "iː", Replacement = "aɪ", Enabled = true },
                new DiachronicRuleSpec { Name = "Long u raising/diphthongization", Era = "EMnE", Target = "uː", Replacement = "aʊ", Enabled = true },
                new DiachronicRuleSpec { Name = "Long e raising", Era = "EMnE", Target = "eː", Replacement = "iː", Enabled = true },
                new DiachronicRuleSpec { Name = "Long o raising", Era = "EMnE", Target = "oː", Replacement = "uː", Enabled = true }
            }
        ),
        new DiachronicPresetSpec(
            "grimms-law",
            "Grimm's Law (Proto-Indo-European to Germanic)",
            "Consonant shift affecting PIE stops (e.g., voiceless stops to fricatives, voiced stops to voiceless stops).",
            new[]
            {
                new DiachronicRuleSpec { Name = "PIE voiceless stops to fricatives", Era = "Proto-Germanic", Target = "p", Replacement = "f", Enabled = true },
                new DiachronicRuleSpec { Name = "PIE voiceless t to θ", Era = "Proto-Germanic", Target = "t", Replacement = "θ", Enabled = true },
                new DiachronicRuleSpec { Name = "PIE voiced aspirated to voiced", Era = "Proto-Germanic", Target = "bʰ", Replacement = "b", Enabled = true }
            }
        ),
        new DiachronicPresetSpec(
            "romance-lenition",
            "Western Romance Lenition",
            "Intervocalic voicing and spirantization of stops (e.g., t -> d between vowels).",
            new[]
            {
                new DiachronicRuleSpec { Name = "Intervocalic t voicing", Era = "Western Romance", Target = "t", Replacement = "d", PrevPhone = "V", NextPhone = "V", Enabled = true },
                new DiachronicRuleSpec { Name = "Intervocalic p voicing", Era = "Western Romance", Target = "p", Replacement = "b", PrevPhone = "V", NextPhone = "V", Enabled = true },
                new DiachronicRuleSpec { Name = "Intervocalic k voicing", Era = "Western Romance", Target = "k", Replacement = "g", PrevPhone = "V", NextPhone = "V", Enabled = true }
            }
        ),
        new DiachronicPresetSpec(
            "japanese-h-lenition",
            "Japanese Consonant Shift (*p -> ɸ -> h)",
            "Bilabial stop lenition into glottal fricative before non-high vowels.",
            new[]
            {
                new DiachronicRuleSpec { Name = "p to bilabial fricative", Era = "Early Middle Japanese", Target = "p", Replacement = "ɸ", Enabled = true },
                new DiachronicRuleSpec { Name = "ɸ to h", Era = "Early Modern Japanese", Target = "ɸ", Replacement = "h", Enabled = true }
            }
        )
    };

    public static IReadOnlyList<DiachronicStepTrace> TraceProtoForm(
        string protoUr,
        IEnumerable<DiachronicRuleSpec> rules,
        LanguageProject project)
    {
        var traces = new List<DiachronicStepTrace>();
        IReadOnlyList<string> cur = MorphologyEngine.SplitPhones(protoUr);
        traces.Add(new DiachronicStepTrace("Proto-Form", "UR", string.Join(" ", cur)));

        int stepNum = 1;
        foreach (var rule in rules.Where(r => r.Enabled))
        {
            cur = PhonologyEngine.ApplyRule(cur, rule, project);
            var era = string.IsNullOrWhiteSpace(rule.Era) ? $"Era {stepNum}" : rule.Era;
            stepNum++;
            traces.Add(new DiachronicStepTrace(era, rule.Name, string.Join(" ", cur)));
        }
        return traces;
    }

    public static DiachronicSimulationResult SimulateEvolution(
        LanguageProject protoProject,
        IEnumerable<DiachronicRuleSpec> rules,
        string daughterName)
    {
        var ruleList = rules.ToList();
        var sampleLexeme = protoProject.Lexemes.FirstOrDefault(l => !string.IsNullOrEmpty(l.ProtoUR))
            ?? protoProject.Lexemes.FirstOrDefault();
        var sampleUr = sampleLexeme?.ProtoUR ?? sampleLexeme?.UR ?? "ˈp a t e";

        var traces = TraceProtoForm(sampleUr, ruleList, protoProject);

        var cognateDiffs = new List<DiachronicCognateDiff>();
        foreach (var lex in protoProject.Lexemes.Take(15))
        {
            var pUr = !string.IsNullOrEmpty(lex.ProtoUR) ? lex.ProtoUR : lex.UR;
            var pPhones = MorphologyEngine.SplitPhones(pUr);
            IReadOnlyList<string> dPhones = pPhones;
            foreach (var r in ruleList.Where(x => x.Enabled))
            {
                dPhones = PhonologyEngine.ApplyRule(dPhones, r, protoProject);
            }
            var dSurf = string.Join("", dPhones);
            string status = string.Join(" ", pPhones) == string.Join(" ", dPhones) ? "Unchanged" : "Evolved";
            cognateDiffs.Add(new DiachronicCognateDiff(lex.Lemma, pUr, string.Join(" ", dPhones), dSurf, status));
        }

        var affixDiffs = new List<DiachronicAffixDiff>();
        foreach (var afx in protoProject.Affixes)
        {
            var aPhones = MorphologyEngine.SplitPhones(afx.Affix);
            IReadOnlyList<string> dAffPhones = aPhones;
            foreach (var r in ruleList.Where(x => x.Enabled))
            {
                dAffPhones = PhonologyEngine.ApplyRule(dAffPhones, r, protoProject);
            }
            affixDiffs.Add(new DiachronicAffixDiff(afx.Name, afx.Affix, string.Join("", dAffPhones)));
        }

        var originalPhones = new HashSet<string>(protoProject.Phonemes.Select(p => PhonologyEngine.StripSuprasegmentals(p.Symbol)));
        var evolvedPhones = new HashSet<string>();
        foreach (var lex in protoProject.Lexemes)
        {
            var pPhones = MorphologyEngine.SplitPhones(!string.IsNullOrEmpty(lex.ProtoUR) ? lex.ProtoUR : lex.UR);
            IReadOnlyList<string> dPhones = pPhones;
            foreach (var r in ruleList.Where(x => x.Enabled)) dPhones = PhonologyEngine.ApplyRule(dPhones, r, protoProject);
            foreach (var ph in dPhones) evolvedPhones.Add(PhonologyEngine.StripSuprasegmentals(ph));
        }

        var emergent = evolvedPhones.Where(p => !originalPhones.Contains(p)).ToList();
        var lost = protoProject.Phonemes.Select(p => PhonologyEngine.StripSuprasegmentals(p.Symbol)).Where(p => !evolvedPhones.Contains(p)).ToList();

        return new DiachronicSimulationResult(
            string.IsNullOrWhiteSpace(daughterName) ? $"{protoProject.Name} (Daughter)" : daughterName,
            traces,
            cognateDiffs,
            affixDiffs,
            emergent,
            lost
        );
    }

    public static LanguageProject ApplyEvolutionToProject(
        LanguageProject protoProject,
        IEnumerable<DiachronicRuleSpec> rules,
        string daughterName,
        bool preserveHistoricalSpelling)
    {
        var ruleList = rules.ToList();
        var daughter = JsonSerializer.Deserialize<LanguageProject>(ProjectFactory.ToJson(protoProject), ProjectFactory.JsonOptions)!;
        daughter.Name = string.IsNullOrWhiteSpace(daughterName) ? $"{protoProject.Name} Daughter" : daughterName;
        daughter.DiachronicRules = ruleList.Select(r => new DiachronicRuleSpec
        {
            Name = r.Name,
            Era = r.Era,
            Target = r.Target,
            Replacement = r.Replacement,
            PrevPhone = r.PrevPhone,
            NextPhone = r.NextPhone,
            Condition = r.Condition,
            Enabled = r.Enabled
        }).ToList();

        var orth = new OrthographyEngine();
        var stemOrthProj = new LanguageProject
        {
            OrthographyRules = daughter.OrthographyRules,
            OrthographyExceptions = daughter.OrthographyExceptions,
            Capitalization = daughter.Capitalization == CapitalizationMode.AllWords ? CapitalizationMode.AllWords : CapitalizationMode.None
        };

        foreach (var lex in daughter.Lexemes)
        {
            if (string.IsNullOrEmpty(lex.ProtoUR))
                lex.ProtoUR = lex.UR;

            IReadOnlyList<string> dPhones = MorphologyEngine.SplitPhones(lex.ProtoUR);
            foreach (var r in ruleList.Where(x => x.Enabled))
            {
                dPhones = PhonologyEngine.ApplyRule(dPhones, r, daughter);
            }
            lex.UR = string.Join(" ", dPhones);
            if (!preserveHistoricalSpelling && !string.IsNullOrEmpty(lex.Spelling))
            {
                lex.Spelling = orth.Render(new[] { dPhones }, stemOrthProj).Text;
            }
        }

        if (preserveHistoricalSpelling)
        {
            daughter.Pipeline.OrthographyRendererId = "morphophonemic-orthography";
        }

        return daughter;
    }
}
