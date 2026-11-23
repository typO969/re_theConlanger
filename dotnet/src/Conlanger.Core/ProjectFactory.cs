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
        ["p"] = new() { Voicing = "voiceless", Manner = "plosive", Place = "bilabial" },
        ["b"] = new() { Voicing = "voiced", Manner = "plosive", Place = "bilabial" },
        ["t"] = new() { Voicing = "voiceless", Manner = "plosive", Place = "alveolar" },
        ["d"] = new() { Voicing = "voiced", Manner = "plosive", Place = "alveolar" },
        ["k"] = new() { Voicing = "voiceless", Manner = "plosive", Place = "velar" },
        ["g"] = new() { Voicing = "voiced", Manner = "plosive", Place = "velar" },
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
        ["l"] = new() { Voicing = "voiced", Manner = "lateral", Place = "alveolar" },
        ["r"] = new() { Voicing = "voiced", Manner = "trill", Place = "alveolar" },
        ["j"] = new() { Voicing = "voiced", Manner = "approximant", Place = "palatal" },
        ["w"] = new() { Voicing = "voiced", Manner = "approximant", Place = "labiovelar" },
        ["a"] = new() { Height = "low", Backness = "central", Roundness = "unrounded" },
        ["e"] = new() { Height = "mid", Backness = "front", Roundness = "unrounded" },
        ["i"] = new() { Height = "high", Backness = "front", Roundness = "unrounded" },
        ["o"] = new() { Height = "mid", Backness = "back", Roundness = "rounded" },
        ["u"] = new() { Height = "high", Backness = "back", Roundness = "rounded" },
        ["ə"] = new() { Height = "mid", Backness = "central", Roundness = "unrounded" }
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
            : new PhonologicalFeatures();

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
        var consonants = new[] { "p", "t", "k", "s", "n", "r", "m", "l" };
        var vowels = new[] { "a", "e", "i", "o", "u" };
        var phonemes = consonants.Select(c => CreatePhoneme(c, "C"))
            .Concat(vowels.Select(v => CreatePhoneme(v, "V")))
            .ToList();

        return new LanguageProject
        {
            SchemaVersion = 2,
            Name = "Proto-Conlang",
            Phonemes = phonemes,
            Consonants = consonants.ToList(),
            Vowels = vowels.ToList(),
            SyllableTemplates = new()
            {
                new() { Pattern = "CV", Weight = 3 },
                new() { Pattern = "CVC", Weight = 2 },
                new() { Pattern = "V", Weight = 1 }
            },
            Phonotactics = new PhonotacticsConfig
            {
                AllowedOnsets = consonants.Concat(new[] { "pr", "tr", "kr", "sp", "st", "sk" }).ToList(),
                AllowedCodas = new() { "n", "r", "l", "s", "t" },
                MaxOnsetCluster = 2,
                MaxCodaCluster = 1,
                ForbiddenPatterns = new() { "sss", "nnn" }
            },
            GeneratorOptions = new GeneratorOptions { HyphenateSyllables = false, AllowDuplicates = false },
            PhonologyRules = new()
            {
                new() { Name = "Intervocalic s -> z", Target = "s", Replacement = "z", PrevPhone = "V", NextPhone = "V", Condition = "", Enabled = true },
                new() { Name = "Coda k -> ∅ (lenition)", Target = "k", Replacement = "∅", PrevPhone = "V", NextPhone = "#", Condition = "", Enabled = false }
            },
            Lexemes = new()
            {
                new() { Lemma = "cat", Pos = "N", UR = "p a t" },
                new() { Lemma = "dog", Pos = "N", UR = "k a n" },
                new() { Lemma = "sun", Pos = "N", UR = "s a r" },
                new() { Lemma = "big", Pos = "Adj", UR = "t o r" },
                new() { Lemma = "fast", Pos = "Adj", UR = "k i s" },
                new() { Lemma = "see", Pos = "V", UR = "t e r" },
                new() { Lemma = "chase", Pos = "V", UR = "p a s" },
                new() { Lemma = "sleep", Pos = "V", UR = "m i n" },
                new() { Lemma = "be", Pos = "Cop", UR = "e s" }
            },
            Affixes = new()
            {
                new() { Name = "Plural", Pos = "N", FeatureName = "num", FeatureValue = "PL", Affix = "i", IsPrefix = false, Enabled = true },
                new() { Name = "Past", Pos = "V", FeatureName = "tense", FeatureValue = "PST", Affix = "t a", IsPrefix = false, Enabled = true }
            },
            WordOrder = WordOrder.SVO,
            ClauseTemplate = ClauseTemplate.Transitive,
            OrthographyRules = new()
            {
                new() { Phone = "p", Grapheme = "p", Enabled = true },
                new() { Phone = "t", Grapheme = "t", Enabled = true },
                new() { Phone = "k", Grapheme = "k", Enabled = true },
                new() { Phone = "s", Grapheme = "s", Enabled = true },
                new() { Phone = "z", Grapheme = "z", Enabled = true },
                new() { Phone = "n", Grapheme = "n", Enabled = true },
                new() { Phone = "r", Grapheme = "r", Enabled = true },
                new() { Phone = "m", Grapheme = "m", Enabled = true },
                new() { Phone = "l", Grapheme = "l", Enabled = true },
                new() { Phone = "a", Grapheme = "a", Enabled = true },
                new() { Phone = "e", Grapheme = "e", Enabled = true },
                new() { Phone = "i", Grapheme = "i", Enabled = true },
                new() { Phone = "o", Grapheme = "o", Enabled = true },
                new() { Phone = "u", Grapheme = "u", Enabled = true },
                new() { Phone = "ʃ", Grapheme = "sh", Enabled = true }
            },
            OrthographyExceptions = new()
            {
                new() { Input = "ka", Output = "ca" }
            },
            Capitalization = CapitalizationMode.SentenceInitial,
            WritingSystem = WritingSystemEngine.CreateDefaultConfig()
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
        {
            project.WritingSystem = WritingSystemEngine.CreateDefaultConfig();
        }
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
