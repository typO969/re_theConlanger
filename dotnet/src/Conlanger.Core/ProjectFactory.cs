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
        ["b"] = new() { Voicing = "voiced", Manner = "stop", Place = "bilabial" },
        ["t"] = new() { Voicing = "voiceless", Manner = "stop", Place = "alveolar" },
        ["d"] = new() { Voicing = "voiced", Manner = "stop", Place = "alveolar" },
        ["k"] = new() { Voicing = "voiceless", Manner = "stop", Place = "velar" },
        ["g"] = new() { Voicing = "voiced", Manner = "stop", Place = "velar" },
        ["m"] = new() { Voicing = "voiced", Manner = "nasal", Place = "bilabial" },
        ["n"] = new() { Voicing = "voiced", Manner = "nasal", Place = "alveolar" },
        ["ŋ"] = new() { Voicing = "voiced", Manner = "nasal", Place = "velar" },
        ["f"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "labiodental" },
        ["v"] = new() { Voicing = "voiced", Manner = "fricative", Place = "labiodental" },
        ["s"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "alveolar" },
        ["z"] = new() { Voicing = "voiced", Manner = "fricative", Place = "alveolar" },
        ["ʃ"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "postalveolar" },
        ["ʒ"] = new() { Voicing = "voiced", Manner = "fricative", Place = "postalveolar" },
        ["h"] = new() { Voicing = "voiceless", Manner = "fricative", Place = "glottal" },
        ["tʃ"] = new() { Voicing = "voiceless", Manner = "affricate", Place = "postalveolar" },
        ["l"] = new() { Voicing = "voiced", Manner = "approximant", Place = "alveolar" },
        ["r"] = new() { Voicing = "voiced", Manner = "trill", Place = "alveolar" },
        ["j"] = new() { Voicing = "voiced", Manner = "approximant", Place = "palatal" },
        ["w"] = new() { Voicing = "voiced", Manner = "approximant", Place = "labiovelar" },
        ["a"] = new() { Voicing = "voiced", Manner = "vowel", Height = "low", Backness = "central", Roundness = "unrounded" },
        ["e"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "front", Roundness = "unrounded" },
        ["i"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "front", Roundness = "unrounded" },
        ["o"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "back", Roundness = "rounded" },
        ["u"] = new() { Voicing = "voiced", Manner = "vowel", Height = "high", Backness = "back", Roundness = "rounded" },
        ["ə"] = new() { Voicing = "voiced", Manner = "vowel", Height = "mid", Backness = "central", Roundness = "unrounded" }
    };

    public static Phoneme CreatePhoneme(string symbol, string type = "C", int weight = 1, bool allowOnset = true, bool allowCoda = true)
    {
        var features = KnownFeatures.TryGetValue(symbol, out var preset)
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
            Weight = Math.Max(1, weight),
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
