using System.Text.Json;
using System.Text.Json.Serialization;
using Conlanger.SDK;

namespace Conlanger.Core;

public sealed record LanguagePresetInfo(string Id, string Name, string Description);

public static class ProjectFactory
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Dictionary<string, PhonologicalFeatures> KnownFeatures = new(StringComparer.Ordinal)
    {
        ["p"] = new() { Voicing = "voiceless", Manner = "stop", Place = "bilabial" },
        ["pʰ"] = new() { Voicing = "voiceless", Manner = "stop", Place = "bilabial", Custom = "aspirated" },
        ["b"] = new() { Voicing = "voiced", Manner = "stop", Place = "bilabial" },
        ["t"] = new() { Voicing = "voiceless", Manner = "stop", Place = "alveolar" },
        ["tʰ"] = new() { Voicing = "voiceless", Manner = "stop", Place = "alveolar", Custom = "aspirated" },
        ["d"] = new() { Voicing = "voiced", Manner = "stop", Place = "alveolar" },
        ["k"] = new() { Voicing = "voiceless", Manner = "stop", Place = "velar" },
        ["kʰ"] = new() { Voicing = "voiceless", Manner = "stop", Place = "velar", Custom = "aspirated" },
        ["q"] = new() { Voicing = "voiceless", Manner = "stop", Place = "uvular" },
        ["g"] = new() { Voicing = "voiced", Manner = "stop", Place = "velar" },
        ["ʔ"] = new() { Voicing = "voiceless", Manner = "stop", Place = "glottal" },
        ["tʃ"] = new() { Voicing = "voiceless", Manner = "affricate", Place = "postalveolar", Custom = "sibilant" },
        ["dʒ"] = new() { Voicing = "voiced", Manner = "affricate", Place = "postalveolar", Custom = "sibilant" },
        ["m"] = new() { Voicing = "voiced", Manner = "nasal", Place = "bilabial" },
        ["ɱ"] = new() { Voicing = "voiced", Manner = "nasal", Place = "labiodental" },
        ["n"] = new() { Voicing = "voiced", Manner = "nasal", Place = "alveolar" },
        ["ɲ"] = new() { Voicing = "voiced", Manner = "nasal", Place = "palatal" },
        ["ŋ"] = new() { Voicing = "voiced", Manner = "nasal", Place = "velar" },
        ["f"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "labiodental" },
        ["v"] = new() { Voicing = "voiced", Manner = "fricative", Place = "labiodental" },
        ["θ"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "dental" },
        ["ð"] = new() { Voicing = "voiced", Manner = "fricative", Place = "dental" },
        ["s"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "alveolar", Custom = "sibilant" },
        ["z"] = new() { Voicing = "voiced", Manner = "fricative", Place = "alveolar", Custom = "sibilant" },
        ["ʃ"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "postalveolar", Custom = "sibilant" },
        ["ʒ"] = new() { Voicing = "voiced", Manner = "fricative", Place = "postalveolar", Custom = "sibilant" },
        ["x"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "velar" },
        ["ɣ"] = new() { Voicing = "voiced", Manner = "fricative", Place = "velar" },
        ["ħ"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "pharyngeal" },
        ["ʕ"] = new() { Voicing = "voiced", Manner = "fricative", Place = "pharyngeal" },
        ["h"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "glottal" },
        ["ɾ"] = new() { Voicing = "voiced", Manner = "tap", Place = "alveolar" },
        ["r"] = new() { Voicing = "voiced", Manner = "trill", Place = "alveolar", Custom = "liquid" },
        ["ɹ"] = new() { Voicing = "voiced", Manner = "approximant", Place = "postalveolar", Custom = "liquid rhotic" },
        ["l"] = new() { Voicing = "voiced", Manner = "approximant", Place = "alveolar", Custom = "lateral liquid" },
        ["ɫ"] = new() { Voicing = "voiced", Manner = "approximant", Place = "velar", Custom = "lateral dark liquid" },
        ["j"] = new() { Voicing = "voiced", Manner = "approximant", Place = "palatal", Custom = "glide" },
        ["w"] = new() { Voicing = "voiced", Manner = "approximant", Place = "bilabial", Custom = "glide" },
        ["a"] = new() { Voicing = "voiced", Manner = "vowel", Height = "low", Backness = "central", Roundness = "unrounded" },
        ["ɑː"] = new() { Voicing = "voiced", Manner = "vowel", Height = "low", Backness = "back", Roundness = "unrounded", Custom = "tense long" },
        ["æ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "low", Backness = "front", Roundness = "unrounded", Custom = "lax" },
        ["e"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "front", Roundness = "unrounded" },
        ["eɪ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "front", Roundness = "unrounded", Custom = "diphthong tense" },
        ["ɛ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "front", Roundness = "unrounded", Custom = "lax" },
        ["i"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "front", Roundness = "unrounded" },
        ["iː"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "front", Roundness = "unrounded", Custom = "tense long" },
        ["ɪ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "front", Roundness = "unrounded", Custom = "lax" },
        ["y"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "front", Roundness = "rounded" },
        ["yː"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "front", Roundness = "rounded", Custom = "tense long" },
        ["ø"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "front", Roundness = "rounded" },
        ["øː"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "front", Roundness = "rounded", Custom = "tense long" },
        ["œ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "front", Roundness = "rounded", Custom = "lax" },
        ["ɯ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "back", Roundness = "unrounded" },
        ["ɤ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "back", Roundness = "unrounded" },
        ["o"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "back", Roundness = "rounded" },
        ["oʊ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "back", Roundness = "rounded", Custom = "diphthong tense" },
        ["ɔː"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "back", Roundness = "rounded", Custom = "tense long" },
        ["u"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "back", Roundness = "rounded" },
        ["uː"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "back", Roundness = "rounded", Custom = "tense long" },
        ["ʊ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "back", Roundness = "rounded", Custom = "lax" },
        ["ʌ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "central", Roundness = "unrounded", Custom = "lax" },
        ["ə"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "central", Roundness = "unrounded", Custom = "reduced" },
        ["ɚ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "central", Roundness = "unrounded", Custom = "rhotic reduced" },
        ["ɝː"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "central", Roundness = "unrounded", Custom = "rhotic stressed" },
        ["aɪ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "low", Backness = "front", Roundness = "unrounded", Custom = "diphthong" },
        ["aʊ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "low", Backness = "back", Roundness = "rounded", Custom = "diphthong" },
        ["ɔɪ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "back", Roundness = "rounded", Custom = "diphthong" },
        ["bʰ"] = new() { Voicing = "voiced", Manner = "stop", Place = "bilabial", Custom = "aspirated" },
        ["dʰ"] = new() { Voicing = "voiced", Manner = "stop", Place = "alveolar", Custom = "aspirated" },
        ["gʰ"] = new() { Voicing = "voiced", Manner = "stop", Place = "velar", Custom = "aspirated" },
        ["ts"] = new() { Voicing = "voiceless", Manner = "affricate", Place = "alveolar", Custom = "sibilant" },
        ["dz"] = new() { Voicing = "voiced", Manner = "affricate", Place = "alveolar", Custom = "sibilant" },
        ["ɸ"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "bilabial" },
        ["β"] = new() { Voicing = "voiced", Manner = "fricative", Place = "bilabial" },
        ["ç"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "palatal" },
        ["eː"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "front", Roundness = "unrounded", Custom = "tense long" },
        ["oː"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "back", Roundness = "rounded", Custom = "tense long" },
        ["ʎ"] = new() { Voicing = "voiced", Manner = "approximant", Place = "palatal", Custom = "lateral liquid" },
        ["pf"] = new() { Voicing = "voiceless", Manner = "affricate", Place = "labiodental" },
        ["tsʰ"] = new() { Voicing = "voiceless", Manner = "affricate", Place = "alveolar", Custom = "aspirated sibilant" },
        ["tʂ"] = new() { Voicing = "voiceless", Manner = "affricate", Place = "retroflex", Custom = "sibilant" },
        ["tʂʰ"] = new() { Voicing = "voiceless", Manner = "affricate", Place = "retroflex", Custom = "aspirated sibilant" },
        ["tɕ"] = new() { Voicing = "voiceless", Manner = "affricate", Place = "alveolopalatal", Custom = "sibilant" },
        ["tɕʰ"] = new() { Voicing = "voiceless", Manner = "affricate", Place = "alveolopalatal", Custom = "aspirated sibilant" },
        ["dʑ"] = new() { Voicing = "voiced", Manner = "affricate", Place = "alveolopalatal", Custom = "sibilant" },
        ["ʂ"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "retroflex", Custom = "sibilant" },
        ["ɕ"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "alveolopalatal", Custom = "sibilant" },
        ["ʑ"] = new() { Voicing = "voiced", Manner = "fricative", Place = "alveolopalatal", Custom = "sibilant" },
        ["ɻ"] = new() { Voicing = "voiced", Manner = "approximant", Place = "retroflex", Custom = "liquid rhotic" },
        ["ʁ"] = new() { Voicing = "voiced", Manner = "fricative", Place = "uvular" },
        ["c"] = new() { Voicing = "voiceless", Manner = "stop", Place = "palatal" },
        ["ɟ"] = new() { Voicing = "voiced", Manner = "stop", Place = "palatal" },
        ["tˤ"] = new() { Voicing = "voiceless", Manner = "stop", Place = "alveolar", Custom = "pharyngealized" },
        ["sˤ"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "alveolar", Custom = "sibilant pharyngealized" },
        ["dʲ"] = new() { Voicing = "voiced", Manner = "stop", Place = "alveolar", Custom = "palatalized" },
        ["tʲ"] = new() { Voicing = "voiceless", Manner = "stop", Place = "alveolar", Custom = "palatalized" },
        ["nʲ"] = new() { Voicing = "voiced", Manner = "nasal", Place = "alveolar", Custom = "palatalized" },
        ["lʲ"] = new() { Voicing = "voiced", Manner = "approximant", Place = "alveolar", Custom = "palatalized lateral" },
        ["ɴ"] = new() { Voicing = "voiced", Manner = "nasal", Place = "uvular" },
        ["ɨ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "central", Roundness = "unrounded" },
        ["z̩"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "central", Roundness = "unrounded", Custom = "apical" }
    };

    internal static PhonologicalFeatures? TryGetKnownFeatures(string symbol)
    {
        var noStress = PhonologyEngine.StripStressMark(symbol);
        if (KnownFeatures.TryGetValue(noStress, out var f)) return f;
        var clean = PhonologyEngine.StripSuprasegmentals(symbol);
        return KnownFeatures.TryGetValue(clean, out f) ? f : null;
    }

    internal static IEnumerable<KeyValuePair<string, PhonologicalFeatures>> EnumerateKnownFeatures() => KnownFeatures;

    public static Phoneme CreatePhoneme(string symbol, string type = "C", int weight = 1, bool allowOnset = true, bool allowCoda = true)
    {
        var noStress = PhonologyEngine.StripStressMark(symbol);
        var clean = PhonologyEngine.StripSuprasegmentals(symbol);
        var hasPreset = KnownFeatures.TryGetValue(noStress, out var preset) || KnownFeatures.TryGetValue(clean, out preset);
        var features = hasPreset && preset is not null
            ? new PhonologicalFeatures
            {
                Voicing = preset.Voicing,
                Manner = preset.Manner,
                Place = preset.Place,
                Height = preset.Height,
                Backness = preset.Backness,
                Roundness = preset.Roundness,
                Custom = preset.Custom
            }
            : (type == "V" ? new PhonologicalFeatures { Voicing = "voiced", Manner = "vowel" } : new PhonologicalFeatures());

        return new Phoneme
        {
            Symbol = symbol,
            Type = type,
            Weight = Math.Max(0, weight),
            AllowOnset = allowOnset,
            AllowCoda = allowCoda,
            Features = features
        };
    }

    public static LanguageProject CreateStarterProject()
    {
        var consonants = new[] { "p", "t", "k", "ʃ", "s", "z" };
        var vowels = new[] { "a", "e", "i", "o", "u" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C"))
            .Concat(vowels.Select(v => CreatePhoneme(v, "V")))
            .ToList();

        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "My Conlang",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 3 },
                new() { Pattern = "CVC", Weight = 2 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = new() { "pr", "tr", "kr", "sp", "st", "sk" },
                AllowedCodas = new() { "m", "n", "s", "t", "k", "r", "l" },
                MaxOnsetCluster = 2,
                MaxCodaCluster = 2,
                ForbiddenPatterns = new()
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = true },
            StressPattern = StressPattern.None,
            PhonologyRules = new()
            {
                new() { Name = "Intervocalic voicing", Target = "s", Replacement = "z", PrevPhone = "a", NextPhone = "a", Condition = "", Enabled = true }
            },
            Lexemes = new()
            {
                new() { Lemma = "person", Pos = "N", UR = "p e r s o n" },
                new() { Lemma = "fish", Pos = "N", UR = "f i ʃ" },
                new() { Lemma = "water", Pos = "N", UR = "k a s a" },
                new() { Lemma = "see", Pos = "V", UR = "s i" },
                new() { Lemma = "seek", Pos = "V", UR = "t i k a" },
                new() { Lemma = "small", Pos = "ADJ", UR = "s m a l" }
            },
            Affixes = new()
            {
                new() { Name = "Plural", Pos = "N", FeatureName = "Number", FeatureValue = "Pl", Affix = "-i", IsPrefix = false, Enabled = true },
                new() { Name = "Past", Pos = "V", FeatureName = "Tense", FeatureValue = "Past", Affix = "-ta", IsPrefix = false, Enabled = true }
            },
            WordOrder = WordOrder.SVO,
            ClauseTemplate = ClauseTemplate.Transitive,
            SyntaxConfig = new SyntaxConfig(),
            OrthographyRules = new()
            {
                new() { Phone = "ʃ", Grapheme = "sh", Enabled = true },
                new() { Phone = "t", Grapheme = "c", NextContext = "i", Enabled = true },
                new() { Phone = "k", Grapheme = "c", NextContext = "e", Enabled = true },
                new() { Phone = "k", Grapheme = "k", Enabled = true }
            },
            OrthographyExceptions = new(),
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig(),
            Pipeline = new PipelineSelection()
        };
    }

    public static LanguageProject CreateEnglishPresetProject()
    {
        var consonants = new[]
        {
            "p", "t", "k", "b", "d", "g",
            "tʃ", "dʒ",
            "f", "v", "θ", "ð", "s", "z", "ʃ", "ʒ", "h",
            "m", "n", "ŋ",
            "l", "ɹ", "w", "j",
            "pʰ", "tʰ", "kʰ", "ɾ", "ɫ", "ɱ", "ɲ"
        };
        var vowels = new[]
        {
            "iː", "ɪ", "eɪ", "ɛ", "æ",
            "ɑː", "ɔː", "oʊ", "ʊ", "uː",
            "ʌ", "ə", "ɚ", "ɝː", "aɪ", "aʊ", "ɔɪ"
        };
        var allophones = new HashSet<string>(StringComparer.Ordinal) { "pʰ", "tʰ", "kʰ", "ɾ", "ɫ", "ɱ", "ɲ" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C", weight: allophones.Contains(c) ? 0 : 2))
            .Concat(vowels.Select(v => CreatePhoneme(v, "V", weight: v == "ə" ? 3 : 2)))
            .ToList();

        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "General American English",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 2 },
                new() { Pattern = "CVC", Weight = 3 },
                new() { Pattern = "(C)CVC(C)", Weight = 2 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = new()
                {
                    "p", "t", "k", "b", "d", "g", "f", "v", "θ", "ð", "s", "z", "ʃ", "h", "m", "n", "l", "ɹ", "w", "j",
                    "tʃ", "dʒ", "pʰ", "tʰ", "kʰ",
                    "pl", "pr", "pɹ", "bl", "br", "bɹ", "kl", "kr", "kɹ", "kw", "gl", "gr", "gɹ",
                    "tr", "tɹ", "dr", "dɹ", "fl", "fr", "fɹ", "θr", "θɹ", "sl", "sm", "sn", "sp", "st", "sk", "sw",
                    "spl", "spr", "spɹ", "str", "stɹ", "skr", "skɹ"
                },
                AllowedCodas = new()
                {
                    "p", "t", "k", "b", "d", "g", "f", "v", "θ", "ð", "s", "z", "ʃ", "ʒ", "m", "n", "ŋ", "l", "ɫ", "ɹ",
                    "tʃ", "dʒ", "ɾ",
                    "st", "sk", "sp", "nt", "nd", "ns", "nz", "mp", "ŋk", "lt", "ld", "ɫt", "ɫd", "lk", "lp",
                    "rt", "rd", "rk", "rp", "rs", "rz", "ɹt", "ɹd", "ɹk", "ɹp", "ɹs", "ɹz", "ks", "ts", "dz", "ps",
                    "gz", "bz", "vz", "mz", "ɹks", "ɹts", "ɹdz", "kt", "gd", "bd", "zd", "pt", "ft"
                },
                MaxOnsetCluster = 3,
                MaxCodaCluster = 4,
                ForbiddenPatterns = new()
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = true },
            StressPattern = StressPattern.Lexical,
            PhonologyRules = new()
            {
                new() { Name = "Aspirate /p/ in stressed onset", Target = "p", Replacement = "pʰ", PrevPhone = "$", NextPhone = "[+stressed]", Condition = "", Enabled = true },
                new() { Name = "Aspirate /t/ in stressed onset", Target = "t", Replacement = "tʰ", PrevPhone = "$", NextPhone = "[+stressed]", Condition = "", Enabled = true },
                new() { Name = "Aspirate /k/ in stressed onset", Target = "k", Replacement = "kʰ", PrevPhone = "$", NextPhone = "[+stressed]", Condition = "", Enabled = true },
                new() { Name = "Intervocalic /t/ Flapping", Target = "t", Replacement = "ɾ", PrevPhone = "V", NextPhone = "[-stressed]", Condition = "", Enabled = true },
                new() { Name = "Nasal Place Assimilation", Target = "n", Replacement = "[αplace]", PrevPhone = null, NextPhone = "[manner=stop]", Condition = "", Enabled = true },
                new() { Name = "Syllable-Final Dark L", Target = "l", Replacement = "ɫ", PrevPhone = null, NextPhone = "$", Condition = "", Enabled = true }
            },
            Lexemes = new()
            {
                new() { Lemma = "the", Pos = "DET", UR = "ð ə", Spelling = "the" },
                new() { Lemma = "a", Pos = "DET", UR = "ə", Spelling = "a" },
                new() { Lemma = "that", Pos = "COMP", UR = "ð æ t", Spelling = "that" },
                new() { Lemma = "who", Pos = "REL", UR = "h uː", Spelling = "who" },
                new() { Lemma = "and", Pos = "CONJ", UR = "æ n d", Spelling = "and" },
                new() { Lemma = "by", Pos = "PREP", UR = "b aɪ", Spelling = "by" },
                new() { Lemma = "I", Pos = "PRON", UR = "ˈaɪ", Spelling = "I", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Case=Acc"] = "ˈm iː" } },
                new() { Lemma = "she", Pos = "PRON", UR = "ˈʃ iː", Spelling = "she", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Case=Acc"] = "ˈh ɝː" } },
                new() { Lemma = "they", Pos = "PRON", UR = "ˈð eɪ", Spelling = "they", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Case=Acc"] = "ˈð ɛ m" } },
                new()
                {
                    Lemma = "be", Pos = "V", UR = "ˈb iː", Spelling = "be",
                    Irregulars = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Person=1,Number=Sing,Tense=Pres"] = "ˈæ m",
                        ["Person=3,Number=Sing,Tense=Pres"] = "ˈɪ z",
                        ["Number=Pl,Tense=Pres"] = "ˈɑː ɹ",
                        ["Number=Sing,Tense=Past"] = "ˈw ʌ z",
                        ["Number=Pl,Tense=Past"] = "ˈw ɝː"
                    }
                },
                new()
                {
                    Lemma = "do", Pos = "AUX", UR = "ˈd uː", Spelling = "do",
                    Irregulars = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Person=3,Number=Sing,Tense=Pres"] = "ˈd ʌ z",
                        ["Tense=Past"] = "ˈd ɪ d"
                    }
                },
                new() { Lemma = "will", Pos = "AUX", UR = "ˈw ɪ l", Spelling = "will" },
                new() { Lemma = "not", Pos = "NEG", UR = "ˈn ɑː t", Spelling = "not" },
                new() { Lemma = "in", Pos = "PREP", UR = "ɪ n", Spelling = "in" },
                new() { Lemma = "near", Pos = "PREP", UR = "ˈn ɪ ɹ", Spelling = "near" },
                new() { Lemma = "quickly", Pos = "ADV", UR = "ˈk w ɪ k l iː", Spelling = "quickly" },
                new() { Lemma = "person", Pos = "N", UR = "ˈp ɝː s ə n", Spelling = "person", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Number=Pl"] = "ˈp iː p ə l" } },
                new() { Lemma = "mouse", Pos = "N", UR = "ˈm aʊ s", Spelling = "mouse", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Number=Pl"] = "ˈm aɪ s" } },
                new() { Lemma = "fish", Pos = "N", UR = "ˈf ɪ ʃ", Spelling = "fish", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Number=Pl"] = "ˈf ɪ ʃ" } },
                new() { Lemma = "cat", Pos = "N", UR = "ˈk æ t", Spelling = "cat" },
                new() { Lemma = "dog", Pos = "N", UR = "ˈd ɔː g", Spelling = "dog" },
                new() { Lemma = "fox", Pos = "N", UR = "ˈf ɑː k s", Spelling = "fox" },
                new() { Lemma = "water", Pos = "N", UR = "ˈw ɔː t ɚ", Spelling = "water", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Number=Pl"] = "ˈw ɔː t ɚ z" } },
                new() { Lemma = "park", Pos = "N", UR = "ˈp ɑː ɹ k", Spelling = "park" },
                new() { Lemma = "knight", Pos = "N", UR = "ˈn aɪ t", Spelling = "knight" },
                new() { Lemma = "walk", Pos = "V", UR = "ˈw ɔː k", Spelling = "walk" },
                new() { Lemma = "chase", Pos = "V", UR = "ˈtʃ eɪ s", Spelling = "chase" },
                new() { Lemma = "see", Pos = "V", UR = "ˈs iː", Spelling = "see", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ˈs ɔː", ["Aspect=Part"] = "ˈs iː n" } },
                new() { Lemma = "go", Pos = "V", UR = "ˈg oʊ", Spelling = "go", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ˈw ɛ n t", ["Person=3,Number=Sing,Tense=Pres"] = "ˈg oʊ z", ["Aspect=Part"] = "ˈg ɔː n" } },
                new() { Lemma = "small", Pos = "ADJ", UR = "ˈs m ɔː l", Spelling = "small" },
                new() { Lemma = "tall", Pos = "ADJ", UR = "ˈt ɔː l", Spelling = "tall" }
            },
            Affixes = new()
            {
                new() { Name = "Plural /ɪz/", Pos = "N", FeatureName = "Number", FeatureValue = "Pl", Affix = "-ɪ z", IsPrefix = false, Condition = "[+sibilant]", Enabled = true },
                new() { Name = "Plural /s/", Pos = "N", FeatureName = "Number", FeatureValue = "Pl", Affix = "-s", IsPrefix = false, Condition = "[-voice]", Enabled = true },
                new() { Name = "Plural /z/", Pos = "N", FeatureName = "Number", FeatureValue = "Pl", Affix = "-z", IsPrefix = false, Condition = "", Enabled = true },
                new() { Name = "Past /ɪd/", Pos = "V", FeatureName = "Tense", FeatureValue = "Past", Affix = "-ɪ d", IsPrefix = false, Condition = "[place=alveolar, manner=stop]", Enabled = true },
                new() { Name = "Past /t/", Pos = "V", FeatureName = "Tense", FeatureValue = "Past", Affix = "-t", IsPrefix = false, Condition = "[-voice]", Enabled = true },
                new() { Name = "Past /d/", Pos = "V", FeatureName = "Tense", FeatureValue = "Past", Affix = "-d", IsPrefix = false, Condition = "", Enabled = true },
                new() { Name = "Participle /ɪd/", Pos = "V", FeatureName = "Aspect", FeatureValue = "Part", Affix = "-ɪ d", IsPrefix = false, Condition = "[place=alveolar, manner=stop]", Enabled = true },
                new() { Name = "Participle /t/", Pos = "V", FeatureName = "Aspect", FeatureValue = "Part", Affix = "-t", IsPrefix = false, Condition = "[-voice]", Enabled = true },
                new() { Name = "Participle /d/", Pos = "V", FeatureName = "Aspect", FeatureValue = "Part", Affix = "-d", IsPrefix = false, Condition = "", Enabled = true },
                new() { Name = "3sg /ɪz/", Pos = "V", FeatureName = "Agreement", FeatureValue = "3sg", Affix = "-ɪ z", IsPrefix = false, Condition = "[+sibilant]", Enabled = true },
                new() { Name = "3sg /s/", Pos = "V", FeatureName = "Agreement", FeatureValue = "3sg", Affix = "-s", IsPrefix = false, Condition = "[-voice]", Enabled = true },
                new() { Name = "3sg /z/", Pos = "V", FeatureName = "Agreement", FeatureValue = "3sg", Affix = "-z", IsPrefix = false, Condition = "", Enabled = true }
            },
            WordOrder = WordOrder.SVO,
            ClauseTemplate = ClauseTemplate.Transitive,
            SyntaxConfig = new SyntaxConfig
            {
                AllowEmbeddedClauses = true,
                AllowRelativeClauses = true,
                AllowPassive = true,
                MaxClauseDepth = 1
            },
            OrthographyRules = new()
            {
                new() { Phone = "tʃ", Grapheme = "ch", Enabled = true },
                new() { Phone = "dʒ", Grapheme = "j", Enabled = true },
                new() { Phone = "ʃ", Grapheme = "sh", Enabled = true },
                new() { Phone = "ʒ", Grapheme = "zh", Enabled = true },
                new() { Phone = "θ", Grapheme = "th", Enabled = true },
                new() { Phone = "ð", Grapheme = "th", Enabled = true },
                new() { Phone = "ŋ", Grapheme = "ng", Enabled = true },
                new() { Phone = "ɱ", Grapheme = "m", Enabled = true },
                new() { Phone = "ɲ", Grapheme = "n", Enabled = true },
                new() { Phone = "pʰ", Grapheme = "p", Enabled = true },
                new() { Phone = "tʰ", Grapheme = "t", Enabled = true },
                new() { Phone = "kʰ", Grapheme = "k", Enabled = true },
                new() { Phone = "ɾ", Grapheme = "t", Enabled = true },
                new() { Phone = "ɫ", Grapheme = "l", Enabled = true },
                new() { Phone = "ɹ", Grapheme = "r", Enabled = true },
                new() { Phone = "j", Grapheme = "y", Enabled = true },
                new() { Phone = "iː", Grapheme = "ee", Enabled = true },
                new() { Phone = "ɪ", Grapheme = "i", Enabled = true },
                new() { Phone = "eɪ", Grapheme = "ay", Enabled = true },
                new() { Phone = "ɛ", Grapheme = "e", Enabled = true },
                new() { Phone = "æ", Grapheme = "a", Enabled = true },
                new() { Phone = "ɑː", Grapheme = "o", Enabled = true },
                new() { Phone = "ɔː", Grapheme = "aw", Enabled = true },
                new() { Phone = "oʊ", Grapheme = "o", Enabled = true },
                new() { Phone = "ʊ", Grapheme = "oo", Enabled = true },
                new() { Phone = "uː", Grapheme = "oo", Enabled = true },
                new() { Phone = "ʌ", Grapheme = "u", Enabled = true },
                new() { Phone = "ə", Grapheme = "a", Enabled = true },
                new() { Phone = "ɚ", Grapheme = "er", Enabled = true },
                new() { Phone = "ɝː", Grapheme = "er", Enabled = true },
                new() { Phone = "aɪ", Grapheme = "i", Enabled = true },
                new() { Phone = "aʊ", Grapheme = "ou", Enabled = true },
                new() { Phone = "ɔɪ", Grapheme = "oy", Enabled = true }
            },
            OrthographyExceptions = new()
            {
                new() { Input = "miː", Output = "me" },
                new() { Input = "hɝː", Output = "her" },
                new() { Input = "hɚ", Output = "her" },
                new() { Input = "ðɛm", Output = "them" },
                new() { Input = "aɪ", Output = "I" },
                new() { Input = "ʃiː", Output = "she" },
                new() { Input = "ðeɪ", Output = "they" },
                new() { Input = "maɪs", Output = "mice" },
                new() { Input = "piːpəl", Output = "people" },
                new() { Input = "pʰiːpəl", Output = "people" },
                new() { Input = "wɛnt", Output = "went" },
                new() { Input = "sɔː", Output = "saw" },
                new() { Input = "siːn", Output = "seen" },
                new() { Input = "gɔːn", Output = "gone" },
                new() { Input = "wʌz", Output = "was" },
                new() { Input = "wɝː", Output = "were" },
                new() { Input = "ɪz", Output = "is" },
                new() { Input = "ɑːɹ", Output = "are" },
                new() { Input = "æm", Output = "am" },
                new() { Input = "dʌz", Output = "does" },
                new() { Input = "dɪd", Output = "did" },
                new() { Input = "goʊz", Output = "goes" },
                new() { Input = "ðə", Output = "the" },
                new() { Input = "ə", Output = "a" },
                new() { Input = "ðæt", Output = "that" },
                new() { Input = "huː", Output = "who" },
                new() { Input = "ænd", Output = "and" },
                new() { Input = "baɪ", Output = "by" },
                new() { Input = "wɔːɾɚz", Output = "waters" }
            },
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig(),
            Pipeline = new PipelineSelection
            {
                SyntaxGeneratorId = "phrase-structure-syntax",
                MorphologyRealizerId = "standard-morphology",
                OrthographyRendererId = "morphophonemic-orthography",
                EnabledPhonoPluginRules = new()
            }
        };
    }

    public static IReadOnlyList<LanguagePresetInfo> GetTop10Presets() => new LanguagePresetInfo[]
    {
        new("english", "General American English", "Germanic / Fusional-Analytic, SVO"),
        new("spanish", "Spanish (Español)", "Romance / Fusional, SVO, rich verbal inflection"),
        new("german", "Standard German (Deutsch)", "Germanic / Agglutinative-Fusional, V2/SOV"),
        new("mandarin", "Mandarin Chinese (普通话)", "Sinitic / Analytic-Tone, SVO, tone sandhi"),
        new("arabic", "Arabic (MSA - العربية)", "Afroasiatic / Root-and-Pattern, VSO/SVO"),
        new("japanese", "Japanese (日本語)", "Japonic / Agglutinative, SOV, postpositions"),
        new("turkish", "Turkish (Türkçe)", "Turkic / Agglutinative, SOV, vowel harmony"),
        new("swahili", "Swahili (Kiswahili)", "Niger-Congo / Noun Class System, SVO"),
        new("russian", "Russian (Русский)", "Slavic / Fusional, SVO, 6 cases & 3 genders"),
        new("indonesian", "Indonesian (Bahasa Indonesia)", "Austronesian / Agglutinative-Isolating, SVO")
    };

    public static LanguageProject CreatePresetProject(string? id) => (id?.ToLowerInvariant()) switch
    {
        "spanish" => CreateSpanishPresetProject(),
        "german" => CreateGermanPresetProject(),
        "mandarin" => CreateMandarinPresetProject(),
        "arabic" => CreateArabicPresetProject(),
        "japanese" => CreateJapanesePresetProject(),
        "turkish" => CreateTurkishPresetProject(),
        "swahili" => CreateSwahiliPresetProject(),
        "russian" => CreateRussianPresetProject(),
        "indonesian" => CreateIndonesianPresetProject(),
        _ => CreateEnglishPresetProject()
    };

    public static LanguageProject CreateSpanishPresetProject()
    {
        var consonants = new[] { "p", "t", "k", "b", "d", "g", "tʃ", "f", "s", "x", "m", "n", "ɲ", "l", "ʎ", "ɾ", "r", "w", "j", "β", "ð", "ɣ" };
        var vowels = new[] { "a", "e", "i", "o", "u" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C")).Concat(vowels.Select(v => CreatePhoneme(v, "V"))).ToList();
        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "Spanish (Español)",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 4 },
                new() { Pattern = "CVC", Weight = 2 },
                new() { Pattern = "CCV", Weight = 1 },
                new() { Pattern = "V", Weight = 1 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = new() { "p", "t", "k", "b", "d", "g", "f", "s", "x", "m", "n", "ɲ", "l", "ɾ", "r", "pr", "br", "tr", "dr", "kr", "gr", "fr", "pl", "bl", "kl", "gl", "fl" },
                AllowedCodas = new() { "n", "s", "l", "r", "d", "θ", "x", "j", "w" },
                MaxOnsetCluster = 2,
                MaxCodaCluster = 1,
                ForbiddenPatterns = new()
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = true },
            StressPattern = StressPattern.Penultimate,
            PhonologyRules = new()
            {
                new() { Name = "Intervocalic /b/ spirantization", Target = "b", Replacement = "β", PrevPhone = "V", NextPhone = "V", Condition = "", Enabled = true },
                new() { Name = "Intervocalic /d/ spirantization", Target = "d", Replacement = "ð", PrevPhone = "V", NextPhone = "V", Condition = "", Enabled = true },
                new() { Name = "Intervocalic /g/ spirantization", Target = "g", Replacement = "ɣ", PrevPhone = "V", NextPhone = "V", Condition = "", Enabled = true },
                new() { Name = "Nasal place assimilation", Target = "n", Replacement = "[αplace]", PrevPhone = null, NextPhone = "[manner=stop]", Condition = "", Enabled = true }
            },
            Lexemes = new()
            {
                new() { Lemma = "el", Pos = "DET", UR = "e l", Spelling = "el", SemanticTags = ["det:def"], LexClass = "Masc" },
                new() { Lemma = "la", Pos = "DET", UR = "l a", Spelling = "la", SemanticTags = ["det:def"], LexClass = "Fem" },
                new() { Lemma = "un", Pos = "DET", UR = "u n", Spelling = "un", SemanticTags = ["det:indef"], LexClass = "Masc" },
                new() { Lemma = "una", Pos = "DET", UR = "u n a", Spelling = "una", SemanticTags = ["det:indef"], LexClass = "Fem" },
                new() { Lemma = "perro", Pos = "N", UR = "ˈp e r o", Spelling = "perro", SemanticTags = ["animate", "animal"], LexClass = "Masc", ProtoUR = "ˈp e r o" },
                new() { Lemma = "gato", Pos = "N", UR = "ˈg a t o", Spelling = "gato", SemanticTags = ["animate", "animal"], LexClass = "Masc", ProtoUR = "ˈg a t o" },
                new() { Lemma = "hombre", Pos = "N", UR = "ˈo m b ɾ e", Spelling = "hombre", SemanticTags = ["animate", "human"], LexClass = "Masc", ProtoUR = "ˈh o m i n e m" },
                new() { Lemma = "mujer", Pos = "N", UR = "m u ˈx e ɾ", Spelling = "mujer", SemanticTags = ["animate", "human"], LexClass = "Fem", ProtoUR = "m u ˈl j e r" },
                new() { Lemma = "agua", Pos = "N", UR = "ˈa ɣ w a", Spelling = "agua", SemanticTags = ["liquid", "mass"], ProtoUR = "ˈa k w a" },
                new() { Lemma = "casa", Pos = "N", UR = "ˈk a s a", Spelling = "casa", SemanticTags = ["place", "concrete"], LexClass = "Fem" },
                new() { Lemma = "parque", Pos = "N", UR = "ˈp a ɾ k e", Spelling = "parque", SemanticTags = ["place", "concrete"], LexClass = "Masc" },
                new() { Lemma = "ver", Pos = "V", UR = "ˈb e ɾ", Spelling = "ver", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ˈb j o", ["Agreement=3sg"] = "ˈb e" }, ProtoUR = "ˈw i d eː r e" },
                new() { Lemma = "correr", Pos = "V", UR = "k o ˈr e ɾ", Spelling = "correr", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "k o ˈr j o", ["Agreement=3sg"] = "ˈk o r e" }, ProtoUR = "k u ˈr e r e" },
                new() { Lemma = "caminar", Pos = "V", UR = "k a m i ˈn a ɾ", Spelling = "caminar", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "k a m i ˈn o", ["Agreement=3sg"] = "k a ˈm i n a" } },
                new() { Lemma = "pequeño", Pos = "ADJ", UR = "p e ˈk e ɲ o", Spelling = "pequeño" },
                new() { Lemma = "grande", Pos = "ADJ", UR = "ˈg ɾ a n d e", Spelling = "grande" },
                new() { Lemma = "en", Pos = "PREP", UR = "e n", Spelling = "en" },
                new() { Lemma = "con", Pos = "PREP", UR = "k o n", Spelling = "con" },
                new() { Lemma = "y", Pos = "CONJ", UR = "i", Spelling = "y" },
                new() { Lemma = "que", Pos = "COMP", UR = "k e", Spelling = "que" },
                new() { Lemma = "yo", Pos = "PRON", UR = "ˈdʒ o", Spelling = "yo" },
                new() { Lemma = "ella", Pos = "PRON", UR = "ˈe ʎ a", Spelling = "ella" },
                new() { Lemma = "ellos", Pos = "PRON", UR = "ˈe ʎ o s", Spelling = "ellos" }
            },
            Affixes = new()
            {
                new() { Name = "Plural /s/", Pos = "N", FeatureName = "Number", FeatureValue = "Pl", Affix = "-s", IsPrefix = false, Condition = "[manner=vowel]", Enabled = true },
                new() { Name = "Plural /es/", Pos = "N", FeatureName = "Number", FeatureValue = "Pl", Affix = "-e s", IsPrefix = false, Condition = "[-sonorant]", Enabled = true },
                new() { Name = "Fem Adj", Pos = "ADJ", FeatureName = "Class", FeatureValue = "Fem", Affix = "-a", IsPrefix = false, Enabled = true },
                new() { Name = "Masc Adj", Pos = "ADJ", FeatureName = "Class", FeatureValue = "Masc", Affix = "-o", IsPrefix = false, Enabled = true },
                new() { Name = "3sg Pres", Pos = "V", FeatureName = "Agreement", FeatureValue = "3sg", Affix = "-e", IsPrefix = false, Enabled = true },
                new() { Name = "Past Preterite", Pos = "V", FeatureName = "Tense", FeatureValue = "Past", Affix = "-o", IsPrefix = false, Enabled = true }
            },
            WordOrder = WordOrder.SVO,
            ClauseTemplate = ClauseTemplate.Transitive,
            SyntaxConfig = new SyntaxConfig
            {
                Alignment = AlignmentSystem.NomAcc,
                AdjectiveOrder = ModifierOrder.AfterNoun,
                DeterminerOrder = ModifierOrder.BeforeNoun,
                AdpositionType = AdpositionOrder.Preposition,
                ProDrop = true,
                AllowPassive = true,
                AllowEmbeddedClauses = true,
                AllowRelativeClauses = true,
                MaxClauseDepth = 1
            },
            OrthographyRules = new()
            {
                new() { Phone = "tʃ", Grapheme = "ch", Enabled = true },
                new() { Phone = "ɲ", Grapheme = "ñ", Enabled = true },
                new() { Phone = "ʎ", Grapheme = "ll", Enabled = true },
                new() { Phone = "r", Grapheme = "rr", Enabled = true },
                new() { Phone = "x", Grapheme = "j", Enabled = true },
                new() { Phone = "β", Grapheme = "b", Enabled = true },
                new() { Phone = "ð", Grapheme = "d", Enabled = true },
                new() { Phone = "ɣ", Grapheme = "g", Enabled = true },
                new() { Phone = "ɾ", Grapheme = "r", Enabled = true }
            },
            OrthographyExceptions = new()
            {
                new() { Input = "el", Output = "el" },
                new() { Input = "un", Output = "un" },
                new() { Input = "la", Output = "la" },
                new() { Input = "una", Output = "una" },
                new() { Input = "aɣwa", Output = "agua" },
                new() { Input = "pero", Output = "perro" },
                new() { Input = "gato", Output = "gato" }
            },
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig(),
            Pipeline = new PipelineSelection
            {
                SyntaxGeneratorId = "phrase-structure-syntax",
                MorphologyRealizerId = "standard-morphology",
                OrthographyRendererId = "morphophonemic-orthography",
                EnabledPhonoPluginRules = new()
            }
        };
    }

    public static LanguageProject CreateGermanPresetProject()
    {
        var consonants = new[] { "p", "t", "k", "b", "d", "g", "pf", "ts", "tʃ", "f", "v", "s", "z", "ʃ", "ç", "x", "h", "m", "n", "ŋ", "l", "ʁ", "j" };
        var vowels = new[] { "a", "eː", "iː", "oː", "uː", "ɛ", "ɪ", "ɔ", "ʊ", "yː", "øː", "aɪ", "aʊ", "ɔɪ", "ə", "ɐ" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C")).Concat(vowels.Select(v => CreatePhoneme(v, "V"))).ToList();
        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "Standard German (Deutsch)",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 2 },
                new() { Pattern = "CVC", Weight = 3 },
                new() { Pattern = "CCVC", Weight = 2 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = new() { "p", "t", "k", "b", "d", "g", "f", "v", "s", "z", "ʃ", "ts", "pf", "m", "n", "l", "ʁ", "j", "sp", "st", "ʃp", "ʃt", "kʁ", "gʁ", "bʁ", "dʁ", "tʁ", "pʁ", "fl", "kl", "pl", "bl", "gl" },
                AllowedCodas = new() { "p", "t", "k", "f", "s", "ʃ", "ç", "x", "m", "n", "ŋ", "l", "ʁ", "st", "nt", "nd", "lt", "ld", "ʁt", "ts", "kst" },
                MaxOnsetCluster = 3,
                MaxCodaCluster = 3,
                ForbiddenPatterns = new()
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = true },
            StressPattern = StressPattern.Initial,
            PhonologyRules = new()
            {
                new() { Name = "Final Devoicing /b/ -> [p]", Target = "b", Replacement = "p", PrevPhone = null, NextPhone = "#", Condition = "", Enabled = true },
                new() { Name = "Final Devoicing /d/ -> [t]", Target = "d", Replacement = "t", PrevPhone = null, NextPhone = "#", Condition = "", Enabled = true },
                new() { Name = "Final Devoicing /g/ -> [k]", Target = "g", Replacement = "k", PrevPhone = null, NextPhone = "#", Condition = "", Enabled = true },
                new() { Name = "Final Devoicing /v/ -> [f]", Target = "v", Replacement = "f", PrevPhone = null, NextPhone = "#", Condition = "", Enabled = true },
                new() { Name = "Final Devoicing /z/ -> [s]", Target = "z", Replacement = "s", PrevPhone = null, NextPhone = "#", Condition = "", Enabled = true },
                new() { Name = "Ich/Ach-Laut allophony", Target = "x", Replacement = "ç", PrevPhone = "[height=high]", NextPhone = null, Condition = "", Enabled = true }
            },
            Lexemes = new()
            {
                new() { Lemma = "der", Pos = "DET", UR = "d eː ɐ", Spelling = "der", SemanticTags = ["det:def"], LexClass = "Masc" },
                new() { Lemma = "die", Pos = "DET", UR = "d iː", Spelling = "die", SemanticTags = ["det:def"], LexClass = "Fem" },
                new() { Lemma = "das", Pos = "DET", UR = "d a s", Spelling = "das", SemanticTags = ["det:def"], LexClass = "Neut" },
                new() { Lemma = "ein", Pos = "DET", UR = "aɪ n", Spelling = "ein", SemanticTags = ["det:indef"] },
                new() { Lemma = "Hund", Pos = "N", UR = "ˈh ʊ n t", Spelling = "Hund", SemanticTags = ["animate", "animal"], LexClass = "Masc", ProtoUR = "ˈh u n d" },
                new() { Lemma = "Katze", Pos = "N", UR = "ˈk a ts ə", Spelling = "Katze", SemanticTags = ["animate", "animal"], LexClass = "Fem", ProtoUR = "ˈk a t s a" },
                new() { Lemma = "Mensch", Pos = "N", UR = "ˈm ɛ n ʃ", Spelling = "Mensch", SemanticTags = ["animate", "human"], LexClass = "Masc" },
                new() { Lemma = "Wasser", Pos = "N", UR = "ˈv a s ɐ", Spelling = "Wasser", SemanticTags = ["liquid", "mass"], LexClass = "Neut", ProtoUR = "ˈw a t s e r" },
                new() { Lemma = "Haus", Pos = "N", UR = "ˈh aʊ s", Spelling = "Haus", SemanticTags = ["place", "concrete"], LexClass = "Neut" },
                new() { Lemma = "Park", Pos = "N", UR = "ˈp a ʁ k", Spelling = "Park", SemanticTags = ["place", "concrete"], LexClass = "Masc" },
                new() { Lemma = "Buch", Pos = "N", UR = "ˈb uː x", Spelling = "Buch", SemanticTags = ["concrete"], LexClass = "Neut" },
                new() { Lemma = "sehen", Pos = "V", UR = "ˈz eː ə n", Spelling = "sehen", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ˈz aː", ["Agreement=3sg"] = "ˈz iː t" } },
                new() { Lemma = "gehen", Pos = "V", UR = "ˈg eː ə n", Spelling = "gehen", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ˈg i ŋ", ["Agreement=3sg"] = "ˈg eː t" } },
                new() { Lemma = "laufen", Pos = "V", UR = "ˈl aʊ f ə n", Spelling = "laufen", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ˈl iː f", ["Agreement=3sg"] = "ˈl ɔɪ f t" } },
                new() { Lemma = "trinken", Pos = "V", UR = "ˈt ʁ ɪ ŋ k ə n", Spelling = "trinken", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ˈt ʁ a ŋ k", ["Agreement=3sg"] = "ˈt ʁ ɪ ŋ k t" } },
                new() { Lemma = "klein", Pos = "ADJ", UR = "k l aɪ n", Spelling = "klein" },
                new() { Lemma = "groß", Pos = "ADJ", UR = "g ʁ oː s", Spelling = "groß" },
                new() { Lemma = "schnell", Pos = "ADJ", UR = "ʃ n ɛ l", Spelling = "schnell" },
                new() { Lemma = "in", Pos = "PREP", UR = "ɪ n", Spelling = "in" },
                new() { Lemma = "mit", Pos = "PREP", UR = "m ɪ t", Spelling = "mit" },
                new() { Lemma = "und", Pos = "CONJ", UR = "ʊ n t", Spelling = "und" },
                new() { Lemma = "dass", Pos = "COMP", UR = "d a s", Spelling = "dass" },
                new() { Lemma = "ich", Pos = "PRON", UR = "ˈɪ ç", Spelling = "ich" },
                new() { Lemma = "er", Pos = "PRON", UR = "ˈeː ɐ", Spelling = "er" },
                new() { Lemma = "sie", Pos = "PRON", UR = "ˈz iː", Spelling = "sie" },
                new() { Lemma = "wir", Pos = "PRON", UR = "ˈv iː ɐ", Spelling = "wir" }
            },
            Affixes = new()
            {
                new() { Name = "Plural -e", Pos = "N", FeatureName = "Number", FeatureValue = "Pl", Affix = "-ə", IsPrefix = false, Enabled = true },
                new() { Name = "3sg Pres", Pos = "V", FeatureName = "Agreement", FeatureValue = "3sg", Affix = "-t", IsPrefix = false, Enabled = true },
                new() { Name = "Past -te", Pos = "V", FeatureName = "Tense", FeatureValue = "Past", Affix = "-t ə", IsPrefix = false, Enabled = true }
            },
            WordOrder = WordOrder.SVO,
            ClauseTemplate = ClauseTemplate.Transitive,
            SyntaxConfig = new SyntaxConfig
            {
                Alignment = AlignmentSystem.NomAcc,
                AdjectiveOrder = ModifierOrder.BeforeNoun,
                DeterminerOrder = ModifierOrder.BeforeNoun,
                AdpositionType = AdpositionOrder.Preposition,
                AllowPassive = true,
                AllowEmbeddedClauses = true,
                AllowRelativeClauses = true,
                MaxClauseDepth = 1
            },
            OrthographyRules = new()
            {
                new() { Phone = "ʃ", Grapheme = "sch", Enabled = true },
                new() { Phone = "ç", Grapheme = "ch", Enabled = true },
                new() { Phone = "x", Grapheme = "ch", Enabled = true },
                new() { Phone = "ts", Grapheme = "z", Enabled = true },
                new() { Phone = "pf", Grapheme = "pf", Enabled = true },
                new() { Phone = "aɪ", Grapheme = "ei", Enabled = true },
                new() { Phone = "aʊ", Grapheme = "au", Enabled = true },
                new() { Phone = "ɔɪ", Grapheme = "eu", Enabled = true },
                new() { Phone = "v", Grapheme = "w", Enabled = true },
                new() { Phone = "f", Grapheme = "v", Enabled = true },
                new() { Phone = "iː", Grapheme = "ie", Enabled = true },
                new() { Phone = "yː", Grapheme = "ü", Enabled = true },
                new() { Phone = "øː", Grapheme = "ö", Enabled = true }
            },
            OrthographyExceptions = new()
            {
                new() { Input = "ʊnt", Output = "und" },
                new() { Input = "ɪn", Output = "in" },
                new() { Input = "mɪt", Output = "mit" },
                new() { Input = "hʊnt", Output = "Hund" },
                new() { Input = "das", Output = "das" }
            },
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig(),
            Pipeline = new PipelineSelection
            {
                SyntaxGeneratorId = "phrase-structure-syntax",
                MorphologyRealizerId = "standard-morphology",
                OrthographyRendererId = "morphophonemic-orthography",
                EnabledPhonoPluginRules = new()
            }
        };
    }

    public static LanguageProject CreateMandarinPresetProject()
    {
        var consonants = new[] { "p", "t", "k", "pʰ", "tʰ", "kʰ", "ts", "tsʰ", "tʂ", "tʂʰ", "tɕ", "tɕʰ", "f", "s", "ʂ", "ɕ", "x", "m", "n", "ŋ", "l", "ɻ", "w", "j" };
        var vowels = new[] { "a", "o", "ɤ", "e", "i", "u", "y", "aɪ", "eɪ", "aʊ", "oʊ" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C")).Concat(vowels.Select(v => CreatePhoneme(v, "V"))).ToList();
        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "Mandarin Chinese (普通话)",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 4 },
                new() { Pattern = "CVC", Weight = 2 },
                new() { Pattern = "V", Weight = 1 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = new() { "p", "t", "k", "pʰ", "tʰ", "kʰ", "ts", "tsʰ", "tʂ", "tʂʰ", "tɕ", "tɕʰ", "f", "s", "ʂ", "ɕ", "x", "m", "n", "l", "ɻ", "w", "j" },
                AllowedCodas = new() { "n", "ŋ", "ɻ" },
                MaxOnsetCluster = 1,
                MaxCodaCluster = 1,
                ForbiddenPatterns = new()
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = true },
            StressPattern = StressPattern.None,
            PhonologyRules = new()
            {
                new() { Name = "Third-Tone Sandhi", Target = "[tone=LHL]", Replacement = "[tone=LH]", PrevPhone = null, NextPhone = "[tone=LHL]", Condition = "", Enabled = true }
            },
            Lexemes = new()
            {
                new() { Lemma = "zhè", Pos = "DET", UR = "tʂ ɤ˥˩", Spelling = "zhè", SemanticTags = ["det:def"] },
                new() { Lemma = "nà", Pos = "DET", UR = "n a˥˩", Spelling = "nà", SemanticTags = ["det:def"] },
                new() { Lemma = "yī", Pos = "DET", UR = "i˥", Spelling = "yī", SemanticTags = ["det:indef"] },
                new() { Lemma = "rén", Pos = "N", UR = "ɻ ɤ n˧˥", Spelling = "rén", SemanticTags = ["animate", "human"] },
                new() { Lemma = "gǒu", Pos = "N", UR = "k oʊ˨˩˦", Spelling = "gǒu", SemanticTags = ["animate", "animal"] },
                new() { Lemma = "māo", Pos = "N", UR = "m aʊ˥", Spelling = "māo", SemanticTags = ["animate", "animal"] },
                new() { Lemma = "shuǐ", Pos = "N", UR = "ʂ w eɪ˨˩˦", Spelling = "shuǐ", SemanticTags = ["liquid", "mass"] },
                new() { Lemma = "fángzi", Pos = "N", UR = "f ɑ ŋ ˧˥ ts z̩", Spelling = "fángzi", SemanticTags = ["place", "concrete"] },
                new() { Lemma = "shū", Pos = "N", UR = "ʂ u˥", Spelling = "shū", SemanticTags = ["concrete"] },
                new() { Lemma = "kàn", Pos = "V", UR = "kʰ a n˥˩", Spelling = "kàn" },
                new() { Lemma = "chī", Pos = "V", UR = "tʂʰ ɨ˥", Spelling = "chī" },
                new() { Lemma = "hē", Pos = "V", UR = "x ɤ˥", Spelling = "hē" },
                new() { Lemma = "zǒu", Pos = "V", UR = "ts oʊ˨˩˦", Spelling = "zǒu" },
                new() { Lemma = "xiǎo", Pos = "ADJ", UR = "ɕ j aʊ˨˩˦", Spelling = "xiǎo" },
                new() { Lemma = "dà", Pos = "ADJ", UR = "t a˥˩", Spelling = "dà" },
                new() { Lemma = "zài", Pos = "PREP", UR = "ts aɪ˥˩", Spelling = "zài" },
                new() { Lemma = "hé", Pos = "CONJ", UR = "x ɤ˧˥", Spelling = "hé" },
                new() { Lemma = "wǒ", Pos = "PRON", UR = "w o˨˩˦", Spelling = "wǒ" },
                new() { Lemma = "nǐ", Pos = "PRON", UR = "n i˨˩˦", Spelling = "nǐ" },
                new() { Lemma = "tā", Pos = "PRON", UR = "tʰ a˥", Spelling = "tā" }
            },
            Affixes = new()
            {
                new() { Name = "Plural -men", Pos = "N", FeatureName = "Number", FeatureValue = "Pl", Affix = "-m ə n", IsPrefix = false, Enabled = true },
                new() { Name = "Aspect -le", Pos = "V", FeatureName = "Tense", FeatureValue = "Past", Affix = "-l ə", IsPrefix = false, Enabled = true }
            },
            WordOrder = WordOrder.SVO,
            ClauseTemplate = ClauseTemplate.Transitive,
            SyntaxConfig = new SyntaxConfig
            {
                Alignment = AlignmentSystem.NomAcc,
                AdjectiveOrder = ModifierOrder.BeforeNoun,
                DeterminerOrder = ModifierOrder.BeforeNoun,
                AdpositionType = AdpositionOrder.Preposition,
                ProDrop = false,
                AllowPassive = false,
                MaxClauseDepth = 1
            },
            OrthographyRules = new()
            {
                new() { Phone = "pʰ", Grapheme = "p", Enabled = true },
                new() { Phone = "p", Grapheme = "b", Enabled = true },
                new() { Phone = "tʰ", Grapheme = "t", Enabled = true },
                new() { Phone = "t", Grapheme = "d", Enabled = true },
                new() { Phone = "kʰ", Grapheme = "k", Enabled = true },
                new() { Phone = "k", Grapheme = "g", Enabled = true },
                new() { Phone = "tɕʰ", Grapheme = "q", Enabled = true },
                new() { Phone = "tɕ", Grapheme = "j", Enabled = true },
                new() { Phone = "ɕ", Grapheme = "x", Enabled = true },
                new() { Phone = "tʂʰ", Grapheme = "ch", Enabled = true },
                new() { Phone = "tʂ", Grapheme = "zh", Enabled = true },
                new() { Phone = "ʂ", Grapheme = "sh", Enabled = true },
                new() { Phone = "ɻ", Grapheme = "r", Enabled = true },
                new() { Phone = "tsʰ", Grapheme = "c", Enabled = true },
                new() { Phone = "ts", Grapheme = "z", Enabled = true }
            },
            OrthographyExceptions = new()
            {
                new() { Input = "wo", Output = "wǒ" },
                new() { Input = "ni", Output = "nǐ" },
                new() { Input = "ta", Output = "tā" }
            },
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig(),
            Pipeline = new PipelineSelection
            {
                SyntaxGeneratorId = "phrase-structure-syntax",
                MorphologyRealizerId = "standard-morphology",
                OrthographyRendererId = "morphophonemic-orthography",
                EnabledPhonoPluginRules = new()
            }
        };
    }

    public static LanguageProject CreateArabicPresetProject()
    {
        var consonants = new[] { "b", "t", "d", "k", "q", "ʔ", "dʒ", "f", "θ", "ð", "s", "z", "ʃ", "x", "ɣ", "ħ", "ʕ", "h", "m", "n", "r", "l", "w", "j" };
        var vowels = new[] { "a", "i", "u", "aː", "iː", "uː" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C")).Concat(vowels.Select(v => CreatePhoneme(v, "V"))).ToList();
        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "Arabic (MSA - العربية)",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 4 },
                new() { Pattern = "CVC", Weight = 3 },
                new() { Pattern = "CVː", Weight = 2 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = new() { "b", "t", "d", "k", "q", "ʔ", "dʒ", "f", "θ", "ð", "s", "z", "ʃ", "x", "ɣ", "ħ", "ʕ", "h", "m", "n", "r", "l", "w", "j" },
                AllowedCodas = new() { "b", "t", "d", "k", "q", "ʔ", "dʒ", "f", "θ", "ð", "s", "z", "ʃ", "x", "ɣ", "ħ", "ʕ", "h", "m", "n", "r", "l" },
                MaxOnsetCluster = 1,
                MaxCodaCluster = 2,
                ForbiddenPatterns = new()
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = true },
            StressPattern = StressPattern.Penultimate,
            PhonologyRules = new()
            {
                new() { Name = "Coronal assimilation of article /l/", Target = "l", Replacement = "[αplace]", PrevPhone = "ʔ a", NextPhone = "[manner=fricative]", Condition = "", Enabled = true }
            },
            Lexemes = new()
            {
                new() { Lemma = "al", Pos = "DET", UR = "ʔ a l", Spelling = "al-", SemanticTags = ["det:def"] },
                new() { Lemma = "rajul", Pos = "N", UR = "ˈr a dʒ u l", Spelling = "rajul", SemanticTags = ["animate", "human", "masc"], LexClass = "Masc", ProtoUR = "r a g u l" },
                new() { Lemma = "imraah", Pos = "N", UR = "ˈi m r a ʔ a h", Spelling = "imra'ah", SemanticTags = ["animate", "human", "fem"], LexClass = "Fem" },
                new() { Lemma = "kalb", Pos = "N", UR = "ˈk a l b", Spelling = "kalb", SemanticTags = ["animate", "animal"], LexClass = "Masc", ProtoUR = "k a l b" },
                new() { Lemma = "qitt", Pos = "N", UR = "ˈq i tˤ", Spelling = "qitt", SemanticTags = ["animate", "animal"], LexClass = "Masc" },
                new() { Lemma = "maa", Pos = "N", UR = "ˈm aː ʔ", Spelling = "maa'", SemanticTags = ["liquid", "mass"] },
                new() { Lemma = "bayt", Pos = "N", UR = "ˈb a j t", Spelling = "bayt", SemanticTags = ["place", "concrete"], LexClass = "Masc", ProtoUR = "b a y t" },
                new() { Lemma = "kitaab", Pos = "N", UR = "k i ˈt aː b", Spelling = "kitaab", SemanticTags = ["concrete"], LexClass = "Masc" },
                new() { Lemma = "kataba", Pos = "V", UR = "ˈk a t a b a", Spelling = "kataba", ProtoUR = "k a t a b a" },
                new() { Lemma = "qaraa", Pos = "V", UR = "ˈq a r a ʔ a", Spelling = "qara'a" },
                new() { Lemma = "raaa", Pos = "V", UR = "ˈr a ʔ aː", Spelling = "ra'aa" },
                new() { Lemma = "shariba", Pos = "V", UR = "ˈʃ a r i b a", Spelling = "shariba" },
                new() { Lemma = "kabiir", Pos = "ADJ", UR = "k a ˈb iː r", Spelling = "kabiir" },
                new() { Lemma = "saghiir", Pos = "ADJ", UR = "sˤ a ˈɣ iː r", Spelling = "saghiir" },
                new() { Lemma = "fii", Pos = "PREP", UR = "f iː", Spelling = "fii" },
                new() { Lemma = "maa_prep", Pos = "PREP", UR = "ˈm a ʕ a", Spelling = "ma'a" },
                new() { Lemma = "wa", Pos = "CONJ", UR = "w a", Spelling = "wa" },
                new() { Lemma = "huwa", Pos = "PRON", UR = "ˈh u w a", Spelling = "huwa" },
                new() { Lemma = "hiya", Pos = "PRON", UR = "ˈh i j a", Spelling = "hiya" },
                new() { Lemma = "hum", Pos = "PRON", UR = "h u m", Spelling = "hum" }
            },
            Affixes = new()
            {
                new() { Name = "Dual -aan", Pos = "N", FeatureName = "Number", FeatureValue = "Dual", Affix = "-aː n", IsPrefix = false, Enabled = true },
                new() { Name = "Plural -uun", Pos = "N", FeatureName = "Number", FeatureValue = "Pl", Affix = "-uː n", IsPrefix = false, Enabled = true },
                new() { Name = "Fem Suffix -at", Pos = "N", FeatureName = "Class", FeatureValue = "Fem", Affix = "-a t", IsPrefix = false, Enabled = true },
                new() { Name = "3sg Fem Verb", Pos = "V", FeatureName = "Agreement", FeatureValue = "3sg", Affix = "-a t", IsPrefix = false, Enabled = true }
            },
            WordOrder = WordOrder.VSO,
            ClauseTemplate = ClauseTemplate.Transitive,
            SyntaxConfig = new SyntaxConfig
            {
                Alignment = AlignmentSystem.NomAcc,
                AdjectiveOrder = ModifierOrder.AfterNoun,
                DeterminerOrder = ModifierOrder.BeforeNoun,
                AdpositionType = AdpositionOrder.Preposition,
                ProDrop = true,
                AllowPassive = true,
                MaxClauseDepth = 1
            },
            OrthographyRules = new()
            {
                new() { Phone = "aː", Grapheme = "aa", Enabled = true },
                new() { Phone = "iː", Grapheme = "ii", Enabled = true },
                new() { Phone = "uː", Grapheme = "uu", Enabled = true },
                new() { Phone = "ʃ", Grapheme = "sh", Enabled = true },
                new() { Phone = "x", Grapheme = "kh", Enabled = true },
                new() { Phone = "ɣ", Grapheme = "gh", Enabled = true },
                new() { Phone = "ħ", Grapheme = "h", Enabled = true },
                new() { Phone = "ʕ", Grapheme = "'", Enabled = true },
                new() { Phone = "ʔ", Grapheme = "'", Enabled = true },
                new() { Phone = "dʒ", Grapheme = "j", Enabled = true },
                new() { Phone = "θ", Grapheme = "th", Enabled = true },
                new() { Phone = "ð", Grapheme = "dh", Enabled = true }
            },
            OrthographyExceptions = new()
            {
                new() { Input = "ʔal", Output = "al-" },
                new() { Input = "wa", Output = "wa" }
            },
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig(),
            Pipeline = new PipelineSelection
            {
                SyntaxGeneratorId = "phrase-structure-syntax",
                MorphologyRealizerId = "standard-morphology",
                OrthographyRendererId = "morphophonemic-orthography",
                EnabledPhonoPluginRules = new()
            }
        };
    }

    public static LanguageProject CreateJapanesePresetProject()
    {
        var consonants = new[] { "p", "t", "k", "b", "d", "g", "ts", "tɕ", "dʑ", "s", "ɕ", "z", "ʑ", "h", "ç", "ɸ", "m", "n", "ɲ", "ŋ", "ɾ", "w", "j" };
        var vowels = new[] { "a", "i", "u", "e", "o", "aː", "iː", "uː", "eː", "oː" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C")).Concat(vowels.Select(v => CreatePhoneme(v, "V"))).ToList();
        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "Japanese (日本語)",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 4 },
                new() { Pattern = "V", Weight = 1 },
                new() { Pattern = "CVC", Weight = 1 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = new() { "p", "t", "k", "b", "d", "g", "s", "z", "h", "m", "n", "ɾ", "w", "j", "tɕ", "dʑ", "ɕ", "ts" },
                AllowedCodas = new() { "n", "ŋ" },
                MaxOnsetCluster = 1,
                MaxCodaCluster = 1,
                ForbiddenPatterns = new()
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = true },
            StressPattern = StressPattern.None,
            PhonologyRules = new()
            {
                new() { Name = "Palatalization /t/ before /i/", Target = "t", Replacement = "tɕ", PrevPhone = null, NextPhone = "i", Condition = "", Enabled = true },
                new() { Name = "Palatalization /s/ before /i/", Target = "s", Replacement = "ɕ", PrevPhone = null, NextPhone = "i", Condition = "", Enabled = true },
                new() { Name = "Palatalization /h/ before /i/", Target = "h", Replacement = "ç", PrevPhone = null, NextPhone = "i", Condition = "", Enabled = true },
                new() { Name = "Affrication /t/ before /u/", Target = "t", Replacement = "ts", PrevPhone = null, NextPhone = "u", Condition = "", Enabled = true },
                new() { Name = "Bilabial /h/ before /u/", Target = "h", Replacement = "ɸ", PrevPhone = null, NextPhone = "u", Condition = "", Enabled = true }
            },
            Lexemes = new()
            {
                new() { Lemma = "kono", Pos = "DET", UR = "k o n o", Spelling = "kono", SemanticTags = ["det:def"] },
                new() { Lemma = "sono", Pos = "DET", UR = "s o n o", Spelling = "sono", SemanticTags = ["det:def"] },
                new() { Lemma = "hito", Pos = "N", UR = "ç i t o", Spelling = "hito", SemanticTags = ["animate", "human"], ProtoUR = "p i t o" },
                new() { Lemma = "inu", Pos = "N", UR = "i n u", Spelling = "inu", SemanticTags = ["animate", "animal"], ProtoUR = "i n u" },
                new() { Lemma = "neko", Pos = "N", UR = "n e k o", Spelling = "neko", SemanticTags = ["animate", "animal"], ProtoUR = "n e k o" },
                new() { Lemma = "mizu", Pos = "N", UR = "m i z u", Spelling = "mizu", SemanticTags = ["liquid", "mass"], ProtoUR = "m i d u" },
                new() { Lemma = "ie", Pos = "N", UR = "i e", Spelling = "ie", SemanticTags = ["place", "concrete"], ProtoUR = "i p e" },
                new() { Lemma = "hon", Pos = "N", UR = "h o ɴ", Spelling = "hon", SemanticTags = ["concrete"] },
                new() { Lemma = "miru", Pos = "V", UR = "m i ɾ u", Spelling = "miru", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "m i t a" } },
                new() { Lemma = "taberu", Pos = "V", UR = "t a b e ɾ u", Spelling = "taberu", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "t a b e t a" } },
                new() { Lemma = "nomu", Pos = "V", UR = "n o m u", Spelling = "nomu", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "n o n d a" } },
                new() { Lemma = "iku", Pos = "V", UR = "i k u", Spelling = "iku", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "i t t a" } },
                new() { Lemma = "chiisai", Pos = "ADJ", UR = "tɕ iː s a i", Spelling = "chiisai" },
                new() { Lemma = "ookii", Pos = "ADJ", UR = "oː k iː", Spelling = "ookii" },
                new() { Lemma = "hayai", Pos = "ADJ", UR = "h a j a i", Spelling = "hayai" },
                new() { Lemma = "de", Pos = "PREP", UR = "d e", Spelling = "de" },
                new() { Lemma = "ni", Pos = "PREP", UR = "n i", Spelling = "ni" },
                new() { Lemma = "to", Pos = "CONJ", UR = "t o", Spelling = "to" },
                new() { Lemma = "ka", Pos = "Q", UR = "k a", Spelling = "ka" },
                new() { Lemma = "watashi", Pos = "PRON", UR = "w a t a ɕ i", Spelling = "watashi" },
                new() { Lemma = "kare", Pos = "PRON", UR = "k a ɾ e", Spelling = "kare" },
                new() { Lemma = "kanojo", Pos = "PRON", UR = "k a n o dʑ o", Spelling = "kanojo" }
            },
            Affixes = new()
            {
                new() { Name = "Past -ta", Pos = "V", FeatureName = "Tense", FeatureValue = "Past", Affix = "-t a", IsPrefix = false, Enabled = true },
                new() { Name = "Neg -nai", Pos = "V", FeatureName = "Polarity", FeatureValue = "Neg", Affix = "-n a i", IsPrefix = false, Enabled = true },
                new() { Name = "Polite -masu", Pos = "V", FeatureName = "Aspect", FeatureValue = "Part", Affix = "-m a s u", IsPrefix = false, Enabled = true }
            },
            WordOrder = WordOrder.SOV,
            ClauseTemplate = ClauseTemplate.Transitive,
            SyntaxConfig = new SyntaxConfig
            {
                Alignment = AlignmentSystem.NomAcc,
                AdjectiveOrder = ModifierOrder.BeforeNoun,
                DeterminerOrder = ModifierOrder.BeforeNoun,
                AdpositionType = AdpositionOrder.Postposition,
                QuestionStrategy = QuestionStrategy.Particle,
                ProDrop = true,
                AllowPassive = false,
                MaxClauseDepth = 1
            },
            OrthographyRules = new()
            {
                new() { Phone = "tɕ", Grapheme = "ch", Enabled = true },
                new() { Phone = "ɕ", Grapheme = "sh", Enabled = true },
                new() { Phone = "dʑ", Grapheme = "j", Enabled = true },
                new() { Phone = "ts", Grapheme = "ts", Enabled = true },
                new() { Phone = "ɸ", Grapheme = "f", Enabled = true },
                new() { Phone = "ç", Grapheme = "h", Enabled = true },
                new() { Phone = "ɾ", Grapheme = "r", Enabled = true },
                new() { Phone = "aː", Grapheme = "aa", Enabled = true },
                new() { Phone = "oː", Grapheme = "oo", Enabled = true },
                new() { Phone = "uː", Grapheme = "uu", Enabled = true },
                new() { Phone = "eː", Grapheme = "ee", Enabled = true },
                new() { Phone = "iː", Grapheme = "ii", Enabled = true }
            },
            OrthographyExceptions = new()
            {
                new() { Input = "kononi", Output = "kono ni" },
                new() { Input = "inude", Output = "inu de" }
            },
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig(),
            Pipeline = new PipelineSelection
            {
                SyntaxGeneratorId = "phrase-structure-syntax",
                MorphologyRealizerId = "standard-morphology",
                OrthographyRendererId = "morphophonemic-orthography",
                EnabledPhonoPluginRules = new()
            }
        };
    }

    public static LanguageProject CreateTurkishPresetProject()
    {
        var consonants = new[] { "p", "b", "t", "d", "k", "g", "tʃ", "dʒ", "f", "v", "s", "z", "ʃ", "ʒ", "h", "m", "n", "l", "ɫ", "ɾ", "j" };
        var vowels = new[] { "a", "e", "ɯ", "i", "o", "ø", "u", "y" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C")).Concat(vowels.Select(v => CreatePhoneme(v, "V"))).ToList();
        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "Turkish (Türkçe)",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 3 },
                new() { Pattern = "CVC", Weight = 3 },
                new() { Pattern = "VC", Weight = 1 },
                new() { Pattern = "V", Weight = 1 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = new() { "p", "b", "t", "d", "k", "g", "tʃ", "dʒ", "f", "v", "s", "z", "ʃ", "h", "m", "n", "l", "ɾ", "j" },
                AllowedCodas = new() { "p", "t", "k", "tʃ", "f", "s", "ʃ", "h", "m", "n", "l", "ɾ", "j", "st", "lt", "rt", "nt" },
                MaxOnsetCluster = 1,
                MaxCodaCluster = 2,
                ForbiddenPatterns = new()
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = true },
            StressPattern = StressPattern.Final,
            PhonologyRules = new()
            {
                new() { Name = "2-way Backness Harmony", Target = "e", Replacement = "[αback]", PrevPhone = "C", NextPhone = null, Condition = "", Enabled = true },
                new() { Name = "4-way Backness+Rounding Harmony", Target = "i", Replacement = "[αback, αround]", PrevPhone = "C", NextPhone = null, Condition = "", Enabled = true }
            },
            Lexemes = new()
            {
                new() { Lemma = "bir", Pos = "DET", UR = "b i ɾ", Spelling = "bir", SemanticTags = ["det:indef"] },
                new() { Lemma = "bu", Pos = "DET", UR = "b u", Spelling = "bu", SemanticTags = ["det:def"] },
                new() { Lemma = "ev", Pos = "N", UR = "e v", Spelling = "ev", SemanticTags = ["place", "concrete"] },
                new() { Lemma = "adam", Pos = "N", UR = "a d a m", Spelling = "adam", SemanticTags = ["animate", "human"] },
                new() { Lemma = "kadın", Pos = "N", UR = "k a d ɯ n", Spelling = "kadın", SemanticTags = ["animate", "human"] },
                new() { Lemma = "köpek", Pos = "N", UR = "c ø p e c", Spelling = "köpek", SemanticTags = ["animate", "animal"] },
                new() { Lemma = "kedi", Pos = "N", UR = "c e d i", Spelling = "kedi", SemanticTags = ["animate", "animal"] },
                new() { Lemma = "su", Pos = "N", UR = "s u", Spelling = "su", SemanticTags = ["liquid", "mass"] },
                new() { Lemma = "kitap", Pos = "N", UR = "c i t a p", Spelling = "kitap", SemanticTags = ["concrete"] },
                new() { Lemma = "görmek", Pos = "V", UR = "ɟ ø ɾ m e c", Spelling = "görmek", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ɟ ø ɾ d y" } },
                new() { Lemma = "gitmek", Pos = "V", UR = "ɟ i t m e c", Spelling = "gitmek", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ɟ i t t i" } },
                new() { Lemma = "yemek", Pos = "V", UR = "j e m e c", Spelling = "yemek", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "j e d i" } },
                new() { Lemma = "içmek", Pos = "V", UR = "i tʃ m e c", Spelling = "içmek", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "i tʃ t i" } },
                new() { Lemma = "küçük", Pos = "ADJ", UR = "c y tʃ y c", Spelling = "küçük" },
                new() { Lemma = "büyük", Pos = "ADJ", UR = "b y j y c", Spelling = "büyük" },
                new() { Lemma = "güzel", Pos = "ADJ", UR = "ɟ y z e l", Spelling = "güzel" },
                new() { Lemma = "içinde", Pos = "PREP", UR = "i tʃ i n d e", Spelling = "içinde" },
                new() { Lemma = "ve", Pos = "CONJ", UR = "v e", Spelling = "ve" },
                new() { Lemma = "ben", Pos = "PRON", UR = "b e n", Spelling = "ben" },
                new() { Lemma = "o", Pos = "PRON", UR = "o", Spelling = "o" },
                new() { Lemma = "onlar", Pos = "PRON", UR = "o n l a ɾ", Spelling = "onlar" }
            },
            Affixes = new()
            {
                new() { Name = "Plural -ler/-lar", Pos = "N", FeatureName = "Number", FeatureValue = "Pl", Affix = "-l e r", IsPrefix = false, Enabled = true },
                new() { Name = "Past -di/-dı", Pos = "V", FeatureName = "Tense", FeatureValue = "Past", Affix = "-d i", IsPrefix = false, Enabled = true },
                new() { Name = "Accusative -i", Pos = "N", FeatureName = "Case", FeatureValue = "Acc", Affix = "-i", IsPrefix = false, Enabled = true },
                new() { Name = "Dative -e", Pos = "N", FeatureName = "Case", FeatureValue = "Dat", Affix = "-e", IsPrefix = false, Enabled = true },
                new() { Name = "Locative -de", Pos = "N", FeatureName = "Case", FeatureValue = "Loc", Affix = "-d e", IsPrefix = false, Enabled = true }
            },
            WordOrder = WordOrder.SOV,
            ClauseTemplate = ClauseTemplate.Transitive,
            SyntaxConfig = new SyntaxConfig
            {
                Alignment = AlignmentSystem.NomAcc,
                AdjectiveOrder = ModifierOrder.BeforeNoun,
                DeterminerOrder = ModifierOrder.BeforeNoun,
                AdpositionType = AdpositionOrder.Postposition,
                ProDrop = true,
                AllowPassive = true,
                MaxClauseDepth = 1
            },
            OrthographyRules = new()
            {
                new() { Phone = "tʃ", Grapheme = "ç", Enabled = true },
                new() { Phone = "dʒ", Grapheme = "c", Enabled = true },
                new() { Phone = "ʃ", Grapheme = "ş", Enabled = true },
                new() { Phone = "ɯ", Grapheme = "ı", Enabled = true },
                new() { Phone = "ø", Grapheme = "ö", Enabled = true },
                new() { Phone = "y", Grapheme = "ü", Enabled = true }
            },
            OrthographyExceptions = new()
            {
                new() { Input = "bir", Output = "bir" },
                new() { Input = "bu", Output = "bu" },
                new() { Input = "su", Output = "su" }
            },
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig(),
            Pipeline = new PipelineSelection
            {
                SyntaxGeneratorId = "phrase-structure-syntax",
                MorphologyRealizerId = "standard-morphology",
                OrthographyRendererId = "morphophonemic-orthography",
                EnabledPhonoPluginRules = new()
            }
        };
    }

    public static LanguageProject CreateSwahiliPresetProject()
    {
        var consonants = new[] { "p", "b", "t", "d", "k", "g", "tʃ", "dʒ", "f", "v", "θ", "ð", "s", "z", "ʃ", "h", "m", "n", "ɲ", "ŋ", "l", "r", "w", "j" };
        var vowels = new[] { "a", "e", "i", "o", "u" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C")).Concat(vowels.Select(v => CreatePhoneme(v, "V"))).ToList();
        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "Swahili (Kiswahili)",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 4 },
                new() { Pattern = "V", Weight = 1 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = new() { "p", "b", "t", "d", "k", "g", "tʃ", "dʒ", "f", "v", "s", "z", "ʃ", "h", "m", "n", "ɲ", "l", "r", "w", "j", "mb", "nd", "ng", "mv", "nz" },
                AllowedCodas = new(),
                MaxOnsetCluster = 2,
                MaxCodaCluster = 0,
                ForbiddenPatterns = new()
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = true },
            StressPattern = StressPattern.Penultimate,
            PhonologyRules = new(),
            Lexemes = new()
            {
                new() { Lemma = "mtu", Pos = "N", UR = "m t u", Spelling = "mtu", SemanticTags = ["animate", "human"], LexClass = "M" },
                new() { Lemma = "watu", Pos = "N", UR = "w a t u", Spelling = "watu", SemanticTags = ["animate", "human"], LexClass = "Wa" },
                new() { Lemma = "mtoto", Pos = "N", UR = "m t o t o", Spelling = "mtoto", SemanticTags = ["animate", "human"], LexClass = "M" },
                new() { Lemma = "mbwa", Pos = "N", UR = "m b w a", Spelling = "mbwa", SemanticTags = ["animate", "animal"], LexClass = "N" },
                new() { Lemma = "paka", Pos = "N", UR = "p a k a", Spelling = "paka", SemanticTags = ["animate", "animal"], LexClass = "N" },
                new() { Lemma = "maji", Pos = "N", UR = "m a dʒ i", Spelling = "maji", SemanticTags = ["liquid", "mass"], LexClass = "Ma" },
                new() { Lemma = "nyumba", Pos = "N", UR = "ɲ u m b a", Spelling = "nyumba", SemanticTags = ["place", "concrete"], LexClass = "N" },
                new() { Lemma = "kitabu", Pos = "N", UR = "k i t a b u", Spelling = "kitabu", SemanticTags = ["concrete"], LexClass = "Ki" },
                new() { Lemma = "vitabu", Pos = "N", UR = "v i t a b u", Spelling = "vitabu", SemanticTags = ["concrete"], LexClass = "Vi" },
                new() { Lemma = "ona", Pos = "V", UR = "o n a", Spelling = "ona" },
                new() { Lemma = "kula", Pos = "V", UR = "k u l a", Spelling = "kula" },
                new() { Lemma = "enda", Pos = "V", UR = "e n d a", Spelling = "enda" },
                new() { Lemma = "fanya", Pos = "V", UR = "f a ɲ a", Spelling = "fanya" },
                new() { Lemma = "dogo", Pos = "ADJ", UR = "d o g o", Spelling = "dogo" },
                new() { Lemma = "kubwa", Pos = "ADJ", UR = "k u b w a", Spelling = "kubwa" },
                new() { Lemma = "zuri", Pos = "ADJ", UR = "z u r i", Spelling = "zuri" },
                new() { Lemma = "katika", Pos = "PREP", UR = "k a t i k a", Spelling = "katika" },
                new() { Lemma = "na", Pos = "CONJ", UR = "n a", Spelling = "na" },
                new() { Lemma = "mimi", Pos = "PRON", UR = "m i m i", Spelling = "mimi" },
                new() { Lemma = "yeye", Pos = "PRON", UR = "j e j e", Spelling = "yeye" },
                new() { Lemma = "wao", Pos = "PRON", UR = "w a o", Spelling = "wao" }
            },
            Affixes = new()
            {
                new() { Name = "Pres na-", Pos = "V", FeatureName = "Tense", FeatureValue = "Pres", Affix = "n a -", IsPrefix = true, Enabled = true },
                new() { Name = "Past li-", Pos = "V", FeatureName = "Tense", FeatureValue = "Past", Affix = "l i -", IsPrefix = true, Enabled = true },
                new() { Name = "Fut ta-", Pos = "V", FeatureName = "Tense", FeatureValue = "Fut", Affix = "t a -", IsPrefix = true, Enabled = true },
                new() { Name = "Class M Adj", Pos = "ADJ", FeatureName = "Class", FeatureValue = "M", Affix = "m -", IsPrefix = true, Enabled = true },
                new() { Name = "Class Wa Adj", Pos = "ADJ", FeatureName = "Class", FeatureValue = "Wa", Affix = "w a -", IsPrefix = true, Enabled = true },
                new() { Name = "Class Ki Adj", Pos = "ADJ", FeatureName = "Class", FeatureValue = "Ki", Affix = "k i -", IsPrefix = true, Enabled = true },
                new() { Name = "Class Vi Adj", Pos = "ADJ", FeatureName = "Class", FeatureValue = "Vi", Affix = "v i -", IsPrefix = true, Enabled = true }
            },
            WordOrder = WordOrder.SVO,
            ClauseTemplate = ClauseTemplate.Transitive,
            SyntaxConfig = new SyntaxConfig
            {
                Alignment = AlignmentSystem.NomAcc,
                AdjectiveOrder = ModifierOrder.AfterNoun,
                DeterminerOrder = ModifierOrder.AfterNoun,
                AdpositionType = AdpositionOrder.Preposition,
                ProDrop = false,
                MaxClauseDepth = 1
            },
            OrthographyRules = new()
            {
                new() { Phone = "tʃ", Grapheme = "ch", Enabled = true },
                new() { Phone = "dʒ", Grapheme = "j", Enabled = true },
                new() { Phone = "ʃ", Grapheme = "sh", Enabled = true },
                new() { Phone = "ɲ", Grapheme = "ny", Enabled = true },
                new() { Phone = "ŋ", Grapheme = "ng'", Enabled = true }
            },
            OrthographyExceptions = new()
            {
                new() { Input = "na", Output = "na" },
                new() { Input = "katika", Output = "katika" }
            },
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig(),
            Pipeline = new PipelineSelection
            {
                SyntaxGeneratorId = "phrase-structure-syntax",
                MorphologyRealizerId = "standard-morphology",
                OrthographyRendererId = "morphophonemic-orthography",
                EnabledPhonoPluginRules = new()
            }
        };
    }

    public static LanguageProject CreateRussianPresetProject()
    {
        var consonants = new[] { "p", "b", "t", "d", "k", "g", "f", "v", "s", "z", "ʃ", "ʒ", "ts", "tʃ", "x", "m", "n", "l", "r", "j" };
        var vowels = new[] { "a", "e", "i", "o", "u", "ɨ" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C")).Concat(vowels.Select(v => CreatePhoneme(v, "V"))).ToList();
        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "Russian (Русский)",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 3 },
                new() { Pattern = "CVC", Weight = 3 },
                new() { Pattern = "CCVC", Weight = 2 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = new() { "p", "b", "t", "d", "k", "g", "f", "v", "s", "z", "ʃ", "ʒ", "ts", "tʃ", "x", "m", "n", "l", "r", "pr", "tr", "kr", "br", "dr", "gr", "sp", "st", "sk", "str" },
                AllowedCodas = new() { "p", "t", "k", "f", "s", "ʃ", "x", "m", "n", "l", "r", "st", "nt", "kt" },
                MaxOnsetCluster = 3,
                MaxCodaCluster = 2,
                ForbiddenPatterns = new()
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = true },
            StressPattern = StressPattern.Lexical,
            PhonologyRules = new()
            {
                new() { Name = "Final Devoicing /b/ -> [p]", Target = "b", Replacement = "p", PrevPhone = null, NextPhone = "#", Condition = "", Enabled = true },
                new() { Name = "Final Devoicing /d/ -> [t]", Target = "d", Replacement = "t", PrevPhone = null, NextPhone = "#", Condition = "", Enabled = true },
                new() { Name = "Final Devoicing /g/ -> [k]", Target = "g", Replacement = "k", PrevPhone = null, NextPhone = "#", Condition = "", Enabled = true },
                new() { Name = "Final Devoicing /v/ -> [f]", Target = "v", Replacement = "f", PrevPhone = null, NextPhone = "#", Condition = "", Enabled = true },
                new() { Name = "Final Devoicing /z/ -> [s]", Target = "z", Replacement = "s", PrevPhone = null, NextPhone = "#", Condition = "", Enabled = true },
                new() { Name = "Final Devoicing /ʒ/ -> [ʃ]", Target = "ʒ", Replacement = "ʃ", PrevPhone = null, NextPhone = "#", Condition = "", Enabled = true },
                new() { Name = "Vowel Reduction (unstressed o -> a)", Target = "o", Replacement = "a", PrevPhone = null, NextPhone = null, Condition = "[-stressed]", Enabled = true }
            },
            Lexemes = new()
            {
                new() { Lemma = "etot", Pos = "DET", UR = "ˈe t o t", Spelling = "etot", SemanticTags = ["det:def"], LexClass = "Masc" },
                new() { Lemma = "eta", Pos = "DET", UR = "ˈe t a", Spelling = "eta", SemanticTags = ["det:def"], LexClass = "Fem" },
                new() { Lemma = "chelovek", Pos = "N", UR = "tʃ e l o ˈv e k", Spelling = "chelovek", SemanticTags = ["animate", "human"], LexClass = "Masc" },
                new() { Lemma = "sobaka", Pos = "N", UR = "s o ˈb a k a", Spelling = "sobaka", SemanticTags = ["animate", "animal"], LexClass = "Fem" },
                new() { Lemma = "koshka", Pos = "N", UR = "ˈk o ʃ k a", Spelling = "koshka", SemanticTags = ["animate", "animal"], LexClass = "Fem" },
                new() { Lemma = "voda", Pos = "N", UR = "v o ˈd a", Spelling = "voda", SemanticTags = ["liquid", "mass"], LexClass = "Fem", ProtoUR = "w o d a" },
                new() { Lemma = "dom", Pos = "N", UR = "d o m", Spelling = "dom", SemanticTags = ["place", "concrete"], LexClass = "Masc" },
                new() { Lemma = "kniga", Pos = "N", UR = "ˈk n i g a", Spelling = "kniga", SemanticTags = ["concrete"], LexClass = "Fem" },
                new() { Lemma = "videt", Pos = "V", UR = "ˈv i dʲ e tʲ", Spelling = "videt'", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ˈv i dʲ e l", ["Agreement=3sg"] = "ˈv i dʲ i t" } },
                new() { Lemma = "chitat", Pos = "V", UR = "tʃ i ˈt a tʲ", Spelling = "chitat'", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "tʃ i ˈt a l", ["Agreement=3sg"] = "tʃ i ˈt a j e t" } },
                new() { Lemma = "idti", Pos = "V", UR = "i ˈt t i", Spelling = "idti", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ʃ o l", ["Agreement=3sg"] = "i ˈdʲ o t" } },
                new() { Lemma = "pit", Pos = "V", UR = "p i tʲ", Spelling = "pit'", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "p i l", ["Agreement=3sg"] = "p j o t" } },
                new() { Lemma = "malenkiy", Pos = "ADJ", UR = "ˈm a lʲ e nʲ k i j", Spelling = "malen'kiy" },
                new() { Lemma = "bolshoy", Pos = "ADJ", UR = "b o lʲ ˈʃ o j", Spelling = "bol'shoy" },
                new() { Lemma = "novyy", Pos = "ADJ", UR = "ˈn o v ɨ j", Spelling = "novyy" },
                new() { Lemma = "v", Pos = "PREP", UR = "v", Spelling = "v" },
                new() { Lemma = "s", Pos = "PREP", UR = "s", Spelling = "s" },
                new() { Lemma = "i", Pos = "CONJ", UR = "i", Spelling = "i" },
                new() { Lemma = "chto", Pos = "COMP", UR = "tʃ t o", Spelling = "chto" },
                new() { Lemma = "ya", Pos = "PRON", UR = "j a", Spelling = "ya" },
                new() { Lemma = "on", Pos = "PRON", UR = "o n", Spelling = "on" },
                new() { Lemma = "ona", Pos = "PRON", UR = "o ˈn a", Spelling = "ona" },
                new() { Lemma = "oni", Pos = "PRON", UR = "o ˈn i", Spelling = "oni" }
            },
            Affixes = new()
            {
                new() { Name = "Plural -y", Pos = "N", FeatureName = "Number", FeatureValue = "Pl", Affix = "-ɨ", IsPrefix = false, Enabled = true },
                new() { Name = "Past -l", Pos = "V", FeatureName = "Tense", FeatureValue = "Past", Affix = "-l", IsPrefix = false, Enabled = true },
                new() { Name = "3sg Pres", Pos = "V", FeatureName = "Agreement", FeatureValue = "3sg", Affix = "-e t", IsPrefix = false, Enabled = true }
            },
            WordOrder = WordOrder.SVO,
            ClauseTemplate = ClauseTemplate.Transitive,
            SyntaxConfig = new SyntaxConfig
            {
                Alignment = AlignmentSystem.NomAcc,
                AdjectiveOrder = ModifierOrder.BeforeNoun,
                DeterminerOrder = ModifierOrder.BeforeNoun,
                AdpositionType = AdpositionOrder.Preposition,
                ProDrop = true,
                AllowPassive = true,
                AllowEmbeddedClauses = true,
                MaxClauseDepth = 1
            },
            OrthographyRules = new()
            {
                new() { Phone = "ʃ", Grapheme = "sh", Enabled = true },
                new() { Phone = "ʒ", Grapheme = "zh", Enabled = true },
                new() { Phone = "tʃ", Grapheme = "ch", Enabled = true },
                new() { Phone = "ts", Grapheme = "ts", Enabled = true },
                new() { Phone = "x", Grapheme = "kh", Enabled = true },
                new() { Phone = "ɨ", Grapheme = "y", Enabled = true },
                new() { Phone = "j", Grapheme = "y", Enabled = true }
            },
            OrthographyExceptions = new()
            {
                new() { Input = "v", Output = "v" },
                new() { Input = "s", Output = "s" },
                new() { Input = "i", Output = "i" }
            },
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig(),
            Pipeline = new PipelineSelection
            {
                SyntaxGeneratorId = "phrase-structure-syntax",
                MorphologyRealizerId = "standard-morphology",
                OrthographyRendererId = "morphophonemic-orthography",
                EnabledPhonoPluginRules = new()
            }
        };
    }

    public static LanguageProject CreateIndonesianPresetProject()
    {
        var consonants = new[] { "p", "b", "t", "d", "k", "g", "tʃ", "dʒ", "f", "s", "z", "ʃ", "x", "h", "m", "n", "ɲ", "ŋ", "l", "r", "w", "j" };
        var vowels = new[] { "a", "e", "ə", "i", "o", "u" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C")).Concat(vowels.Select(v => CreatePhoneme(v, "V"))).ToList();
        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "Indonesian (Bahasa Indonesia)",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 4 },
                new() { Pattern = "CVC", Weight = 3 },
                new() { Pattern = "V", Weight = 1 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = new() { "p", "b", "t", "d", "k", "g", "tʃ", "dʒ", "f", "s", "z", "ʃ", "x", "h", "m", "n", "ɲ", "ŋ", "l", "r", "w", "j", "tr", "dr", "kr", "pr", "br", "bl", "kl", "pl" },
                AllowedCodas = new() { "p", "t", "k", "s", "h", "m", "n", "ŋ", "l", "r" },
                MaxOnsetCluster = 2,
                MaxCodaCluster = 1,
                ForbiddenPatterns = new()
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = true },
            StressPattern = StressPattern.Penultimate,
            PhonologyRules = new()
            {
                new() { Name = "Nasal prefix mutation before /p/", Target = "p", Replacement = "m", PrevPhone = "m ə", NextPhone = "V", Condition = "", Enabled = true },
                new() { Name = "Nasal prefix mutation before /t/", Target = "t", Replacement = "n", PrevPhone = "m ə", NextPhone = "V", Condition = "", Enabled = true },
                new() { Name = "Nasal prefix mutation before /k/", Target = "k", Replacement = "ŋ", PrevPhone = "m ə", NextPhone = "V", Condition = "", Enabled = true }
            },
            Lexemes = new()
            {
                new() { Lemma = "itu", Pos = "DET", UR = "i t u", Spelling = "itu", SemanticTags = ["det:def"] },
                new() { Lemma = "ini", Pos = "DET", UR = "i n i", Spelling = "ini", SemanticTags = ["det:def"] },
                new() { Lemma = "sebuah", Pos = "DET", UR = "s ə b u a h", Spelling = "sebuah", SemanticTags = ["det:indef"] },
                new() { Lemma = "orang", Pos = "N", UR = "o r a ŋ", Spelling = "orang", SemanticTags = ["animate", "human"] },
                new() { Lemma = "anak", Pos = "N", UR = "a n a k", Spelling = "anak", SemanticTags = ["animate", "human"] },
                new() { Lemma = "anjing", Pos = "N", UR = "a n dʒ i ŋ", Spelling = "anjing", SemanticTags = ["animate", "animal"] },
                new() { Lemma = "kucing", Pos = "N", UR = "k u tʃ i ŋ", Spelling = "kucing", SemanticTags = ["animate", "animal"] },
                new() { Lemma = "air", Pos = "N", UR = "a i r", Spelling = "air", SemanticTags = ["liquid", "mass"], ProtoUR = "w a y ə r" },
                new() { Lemma = "rumah", Pos = "N", UR = "r u m a h", Spelling = "rumah", SemanticTags = ["place", "concrete"], ProtoUR = "r u m a q" },
                new() { Lemma = "buku", Pos = "N", UR = "b u k u", Spelling = "buku", SemanticTags = ["concrete"] },
                new() { Lemma = "melihat", Pos = "V", UR = "m ə l i h a t", Spelling = "melihat" },
                new() { Lemma = "makan", Pos = "V", UR = "m a k a n", Spelling = "makan" },
                new() { Lemma = "minum", Pos = "V", UR = "m i n u m", Spelling = "minum" },
                new() { Lemma = "berjalan", Pos = "V", UR = "b ə r dʒ a l a n", Spelling = "berjalan" },
                new() { Lemma = "kecil", Pos = "ADJ", UR = "k ə tʃ i l", Spelling = "kecil" },
                new() { Lemma = "besar", Pos = "ADJ", UR = "b ə s a r", Spelling = "besar" },
                new() { Lemma = "cepat", Pos = "ADJ", UR = "tʃ ə p a t", Spelling = "cepat" },
                new() { Lemma = "di", Pos = "PREP", UR = "d i", Spelling = "di" },
                new() { Lemma = "dengan", Pos = "PREP", UR = "d ə ŋ a n", Spelling = "dengan" },
                new() { Lemma = "dan", Pos = "CONJ", UR = "d a n", Spelling = "dan" },
                new() { Lemma = "bahwa", Pos = "COMP", UR = "b a h w a", Spelling = "bahwa" },
                new() { Lemma = "saya", Pos = "PRON", UR = "s a j a", Spelling = "saya" },
                new() { Lemma = "dia", Pos = "PRON", UR = "d i a", Spelling = "dia" },
                new() { Lemma = "mereka", Pos = "PRON", UR = "m ə r e k a", Spelling = "mereka" }
            },
            Affixes = new()
            {
                new() { Name = "Active meN-", Pos = "V", FeatureName = "Voice", FeatureValue = "Active", Affix = "m ə n -", IsPrefix = true, Enabled = true },
                new() { Name = "Passive di-", Pos = "V", FeatureName = "Voice", FeatureValue = "Passive", Affix = "d i -", IsPrefix = true, Enabled = true },
                new() { Name = "Causative -kan", Pos = "V", FeatureName = "Aspect", FeatureValue = "Part", Affix = "-k a n", IsPrefix = false, Enabled = true }
            },
            WordOrder = WordOrder.SVO,
            ClauseTemplate = ClauseTemplate.Transitive,
            SyntaxConfig = new SyntaxConfig
            {
                Alignment = AlignmentSystem.NomAcc,
                AdjectiveOrder = ModifierOrder.AfterNoun,
                DeterminerOrder = ModifierOrder.AfterNoun,
                AdpositionType = AdpositionOrder.Preposition,
                ProDrop = false,
                AllowPassive = true,
                AllowEmbeddedClauses = true,
                MaxClauseDepth = 1
            },
            OrthographyRules = new()
            {
                new() { Phone = "tʃ", Grapheme = "c", Enabled = true },
                new() { Phone = "dʒ", Grapheme = "j", Enabled = true },
                new() { Phone = "ɲ", Grapheme = "ny", Enabled = true },
                new() { Phone = "ŋ", Grapheme = "ng", Enabled = true },
                new() { Phone = "ʃ", Grapheme = "sy", Enabled = true },
                new() { Phone = "x", Grapheme = "kh", Enabled = true }
            },
            OrthographyExceptions = new()
            {
                new() { Input = "di", Output = "di" },
                new() { Input = "dan", Output = "dan" },
                new() { Input = "ini", Output = "ini" },
                new() { Input = "itu", Output = "itu" }
            },
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig(),
            Pipeline = new PipelineSelection
            {
                SyntaxGeneratorId = "phrase-structure-syntax",
                MorphologyRealizerId = "standard-morphology",
                OrthographyRendererId = "morphophonemic-orthography",
                EnabledPhonoPluginRules = new()
            }
        };
    }

    public static void SyncLegacyLists(LanguageProject project)
    {
        if (project.Phonemes.Count == 0 && (project.Consonants.Count > 0 || project.Vowels.Count > 0))
        {
            project.Phonemes = project.Consonants.Select(c => CreatePhoneme(c, "C"))
                .Concat(project.Vowels.Select(v => CreatePhoneme(v, "V")))
                .ToList();
        }
        else if (project.Phonemes.Count > 0)
        {
            project.Consonants = project.Phonemes.Where(p => p.Type == "C").Select(p => p.Symbol).ToList();
            project.Vowels = project.Phonemes.Where(p => p.Type == "V").Select(p => p.Symbol).ToList();
        }

        if (project.WritingSystem.Glyphs.Count == 0)
            project.WritingSystem = WritingSystemEngine.CreateDefaultConfig();

        project.Pipeline ??= new PipelineSelection();
        project.Phonotactics ??= new PhonotacticsConfig();
        project.GeneratorOptions ??= new GeneratorOptions();
        project.SyntaxConfig ??= new SyntaxConfig();
        project.SyntaxConfig.CustomRules ??= new();
        project.DiachronicRules ??= new();
        project.SyntaxConfig.MaxClauseDepth = Math.Clamp(project.SyntaxConfig.MaxClauseDepth, 0, 3);
        project.SchemaVersion = 2;
    }

    public static string ToJson(LanguageProject project)
    {
        SyncLegacyLists(project);
        return JsonSerializer.Serialize(project, JsonOptions);
    }

    public static LanguageProject FromJson(string json)
    {
        var loaded = JsonSerializer.Deserialize<LanguageProject>(json, JsonOptions) ?? CreateStarterProject();
        SyncLegacyLists(loaded);
        return loaded;
    }
}
