using System.Text.Json;
using System.Text.Json.Serialization;
using Conlanger.SDK;

namespace Conlanger.Core;

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
        ["ɔɪ"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "back", Roundness = "rounded", Custom = "diphthong" }
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
                    "rt", "rd", "rk", "rp", "rs", "rz", "ɹt", "ɹd", "ɹk", "ɹp", "ɹs", "ɹz", "ks", "ts", "dz", "ps"
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
                new() { Lemma = "see", Pos = "V", UR = "ˈs iː", Spelling = "see", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ˈs ɔː" } },
                new() { Lemma = "go", Pos = "V", UR = "ˈg oʊ", Spelling = "go", Irregulars = new(StringComparer.OrdinalIgnoreCase) { ["Tense=Past"] = "ˈw ɛ n t", ["Person=3,Number=Sing,Tense=Pres"] = "ˈg oʊ z" } },
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
                new() { Name = "3sg /ɪz/", Pos = "V", FeatureName = "Agreement", FeatureValue = "3sg", Affix = "-ɪ z", IsPrefix = false, Condition = "[+sibilant]", Enabled = true },
                new() { Name = "3sg /s/", Pos = "V", FeatureName = "Agreement", FeatureValue = "3sg", Affix = "-s", IsPrefix = false, Condition = "[-voice]", Enabled = true },
                new() { Name = "3sg /z/", Pos = "V", FeatureName = "Agreement", FeatureValue = "3sg", Affix = "-z", IsPrefix = false, Condition = "", Enabled = true }
            },
            WordOrder = WordOrder.SVO,
            ClauseTemplate = ClauseTemplate.Transitive,
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
