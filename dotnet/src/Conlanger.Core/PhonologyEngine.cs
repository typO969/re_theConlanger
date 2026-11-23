using System.Text.RegularExpressions;
using Conlanger.SDK;

namespace Conlanger.Core;

public sealed record Syllable(IReadOnlyList<string> Onset, IReadOnlyList<string> Nucleus, IReadOnlyList<string> Coda);

public sealed record DerivationStep(string Label, string Value);

public sealed record PhonologyDerivationResult(
    IReadOnlyList<string> SurfacePhones,
    IReadOnlyList<Syllable> Syllables,
    IReadOnlyList<DerivationStep> Steps,
    IReadOnlyList<string> PhonotacticIssues
);

public static class PhonologyEngine
{
    private static readonly HashSet<string> KnownVowels = new(StringComparer.Ordinal)
    {
        "a", "e", "i", "o", "u", "ə", "ɛ", "ɔ", "æ", "ø", "y", "ɨ", "ɯ", "ɑ", "ɪ", "ʊ",
        "aː", "eː", "iː", "oː", "uː"
    };

    public static bool IsVowel(string phone, LanguageProject project)
    {
        var custom = project.Phonemes.FirstOrDefault(p => p.Symbol == phone);
        if (custom is not null) return custom.Type == "V";
        return KnownVowels.Contains(phone) || project.Vowels.Contains(phone);
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
        if (project.Phonotactics.AllowedCodas.Count == 0) return true;
        return project.Phonotactics.AllowedCodas.Contains(string.Concat(phones));
    }

    public static IReadOnlyList<string> ValidateWordPhonotactics(IReadOnlyList<string> phones, LanguageProject project)
    {
        var issues = new List<string>();
        var joined = string.Concat(phones);
        foreach (var pat in project.Phonotactics.ForbiddenPatterns)
        {
            if (!string.IsNullOrEmpty(pat) && joined.Contains(pat, StringComparison.Ordinal))
                issues.Add($"Contains forbidden sequence '{pat}'");
        }

        var syllables = Syllabify(phones, project);
        for (int i = 0; i < syllables.Count; i++)
        {
            var s = syllables[i];
            if (!IsOnsetAllowed(s.Onset, project))
                issues.Add($"Syllable {i + 1} onset '{string.Concat(s.Onset)}' violates phonotactics");
            if (!IsCodaAllowed(s.Coda, project))
                issues.Add($"Syllable {i + 1} coda '{string.Concat(s.Coda)}' violates phonotactics");
        }
        return issues;
    }

    private static bool MatchesPhoneOrClass(string? actual, string? expected, LanguageProject project)
    {
        if (string.IsNullOrEmpty(expected)) return true;
        if (expected == "#") return actual is null;
        if (actual is null) return false;
        if (expected == "V") return IsVowel(actual, project);
        if (expected == "C") return !IsVowel(actual, project);
        return actual == expected;
    }

    private static bool MatchesFeatureCondition(string? phone, string? condition, LanguageProject project)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        if (string.IsNullOrEmpty(phone)) return false;

        var ph = project.Phonemes.FirstOrDefault(p => p.Symbol == phone) ?? ProjectFactory.CreatePhoneme(phone, IsVowel(phone, project) ? "V" : "C");
        var matches = Regex.Matches(condition, @"\[([^\]]+)\]");
        if (matches.Count == 0) return true;

        foreach (Match m in matches)
        {
            var body = m.Groups[1].Value.Trim();
            if (body.StartsWith('+'))
            {
                var feat = body[1..].ToLowerInvariant();
                if (feat == "voice" || feat == "voiced")
                {
                    if (!string.Equals(ph.Features.Voicing, "voiced", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (feat is "round" or "rounded")
                {
                    if (!string.Equals(ph.Features.Roundness, "rounded", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else
                {
                    var allVals = $"{ph.Features.Manner} {ph.Features.Place} {ph.Features.Height} {ph.Features.Backness} {ph.Features.Custom}".ToLowerInvariant();
                    if (!allVals.Contains(feat, StringComparison.Ordinal)) return false;
                }
            }
            else if (body.StartsWith('-'))
            {
                var feat = body[1..].ToLowerInvariant();
                if (feat is "voice" or "voiced")
                {
                    if (string.Equals(ph.Features.Voicing, "voiced", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (feat is "round" or "rounded")
                {
                    if (string.Equals(ph.Features.Roundness, "rounded", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            else if (body.Contains('='))
            {
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
                if (!string.Equals(actual.Trim(), v, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<string> ApplyRule(IReadOnlyList<string> phones, PhonologyRuleSpec rule, LanguageProject project)
    {
        if (!rule.Enabled) return phones;

        var isDeletion = rule.Replacement is "∅" or "0" or "";
        var isEpenthesis = rule.Target is "∅" or "0" or "";

        if (isEpenthesis)
        {
            if (isDeletion) return phones;
            var repPhones = rule.Replacement.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var outList = new List<string>();
            for (int i = 0; i <= phones.Count; i++)
            {
                string? prev = i > 0 ? phones[i - 1] : null;
                string? next = i < phones.Count ? phones[i] : null;
                if (MatchesPhoneOrClass(prev, rule.PrevPhone, project) && MatchesPhoneOrClass(next, rule.NextPhone, project))
                {
                    if (string.IsNullOrEmpty(rule.PrevPhone) && string.IsNullOrEmpty(rule.NextPhone)) continue;
                    outList.AddRange(repPhones);
                }
                if (i < phones.Count) outList.Add(phones[i]);
            }
            return outList;
        }

        var result = new List<string>(phones.Count);
        for (int i = 0; i < phones.Count; i++)
        {
            var cur = phones[i];
            string? prev = i > 0 ? phones[i - 1] : null;
            string? next = i < phones.Count - 1 ? phones[i + 1] : null;

            bool targetHit = cur == rule.Target || (rule.Target == "C" && !IsVowel(cur, project)) || (rule.Target == "V" && IsVowel(cur, project));
            bool ctxOk = MatchesPhoneOrClass(prev, rule.PrevPhone, project)
                && MatchesPhoneOrClass(next, rule.NextPhone, project)
                && MatchesFeatureCondition(cur, rule.Condition, project);

            if (targetHit && ctxOk)
            {
                if (!isDeletion)
                    result.AddRange(rule.Replacement.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                result.Add(cur);
            }
        }
        return result;
    }

    public static PhonologyDerivationResult Derive(IReadOnlyList<string> underlyingPhones, LanguageProject project)
    {
        IReadOnlyList<string> cur = underlyingPhones.ToList();
        var steps = new List<DerivationStep> { new("UR", string.Join(" ", cur)) };

        foreach (var rule in project.PhonologyRules)
        {
            if (!rule.Enabled) continue;
            var next = ApplyRule(cur, rule, project);
            steps.Add(new DerivationStep(string.IsNullOrWhiteSpace(rule.Name) ? $"{rule.Target}->{rule.Replacement}" : rule.Name, string.Join(" ", next)));
            cur = next;
        }

        var syllables = Syllabify(cur, project);
        var issues = ValidateWordPhonotactics(cur, project);
        return new PhonologyDerivationResult(cur, syllables, steps, issues);
    }
}
