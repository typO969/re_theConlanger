using System.Text.Json.Serialization;

namespace Conlanger.SDK;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WordOrder { SVO, SOV, VSO, VOS, OVS, OSV }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClauseTemplate { Intransitive, Transitive, Copular }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CapitalizationMode { None, SentenceInitial, AllWords }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StressPattern { None, Initial, Penultimate, Final, Lexical }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FlowDirection { LTR, RTL, TTB_LTR, TTB_RTL, Boustrophedon }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiagnosticSeverity { Error, Warning }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AffixType { Prefix, Suffix, Infix, Circumfix, Template }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlignmentSystem { NomAcc, ErgAbs, Tripartite }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModifierOrder { BeforeNoun, AfterNoun }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AdpositionOrder { Preposition, Postposition }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuestionStrategy { Inversion, Particle }

public sealed record Feat(string Name, string Value);

public sealed record PhonologicalFeatures
{
    public string Voicing { get; set; } = "";
    public string Manner { get; set; } = "";
    public string Place { get; set; } = "";
    public string Height { get; set; } = "";
    public string Backness { get; set; } = "";
    public string Roundness { get; set; } = "";
    public string Custom { get; set; } = "";
}

public sealed class Phoneme
{
    public string Symbol { get; set; } = "";
    public string Type { get; set; } = "C"; // "C" | "V"
    public int Weight { get; set; } = 1;
    public bool AllowOnset { get; set; } = true;
    public bool AllowCoda { get; set; } = true;
    public PhonologicalFeatures Features { get; set; } = new();
}

public sealed class SyllableTemplate
{
    public string Pattern { get; set; } = "CV";
    public int Weight { get; set; } = 1;
}

public sealed class PhonotacticsConfig
{
    public List<string> AllowedOnsets { get; set; } = new();
    public List<string> AllowedCodas { get; set; } = new();
    public int MaxOnsetCluster { get; set; } = 2;
    public int MaxCodaCluster { get; set; } = 2;
    public List<string> ForbiddenPatterns { get; set; } = new();
}

public sealed class GeneratorOptions
{
    public bool HyphenateSyllables { get; set; }
    public bool AllowDuplicates { get; set; }
}

public sealed class PhonologyRuleSpec
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string Replacement { get; set; } = "";
    public string? PrevPhone { get; set; }
    public string? NextPhone { get; set; }
    public string? Condition { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class LexemeEntry
{
    public string Lemma { get; set; } = "";
    public string Pos { get; set; } = "N";
    public string UR { get; set; } = "";
    public string? LexClass { get; set; }
    public string? Spelling { get; set; }
    public Dictionary<string, string>? Irregulars { get; set; }

    [JsonIgnore]
    public string IrregularsSummary
    {
        get => Irregulars is null || Irregulars.Count == 0
            ? ""
            : string.Join("; ", Irregulars.Select(kv => $"{kv.Key} -> {kv.Value}"));
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Irregulars = null;
                return;
            }
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int arrow = part.IndexOf("->", StringComparison.Ordinal);
                int idx = arrow >= 0 ? arrow : part.IndexOf(':');
                if (idx <= 0) continue;
                int sepLen = arrow >= 0 ? 2 : 1;
                var k = part[..idx].Trim();
                var v = part[(idx + sepLen)..].Trim();
                if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                    dict[k] = v;
            }
            Irregulars = dict.Count > 0 ? dict : null;
        }
    }
}

public sealed class AffixEntry
{
    public string Name { get; set; } = "";
    public string Pos { get; set; } = "N";
    public string FeatureName { get; set; } = "Number";
    public string FeatureValue { get; set; } = "Pl";
    public string Affix { get; set; } = "";
    public bool IsPrefix { get; set; }
    public AffixType? AffixType { get; set; }
    public string? Condition { get; set; }
    public bool Enabled { get; set; } = true;

    [JsonIgnore]
    public AffixType EffectiveAffixType
    {
        get => AffixType ?? (IsPrefix ? Conlanger.SDK.AffixType.Prefix : Conlanger.SDK.AffixType.Suffix);
        set
        {
            AffixType = value;
            IsPrefix = value == Conlanger.SDK.AffixType.Prefix;
        }
    }
}

public sealed class OrthRuleEntry
{
    public string Phone { get; set; } = "";
    public string Grapheme { get; set; } = "";
    public string? PrevContext { get; set; }
    public string? NextContext { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class OrthExceptionEntry
{
    public string Input { get; set; } = "";
    public string Output { get; set; } = "";
}

public sealed record AnchorPoint(double X, double Y);

public sealed class GlyphDefinition
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string SvgPath { get; set; } = "";
    public string? InitialPath { get; set; }
    public string? MedialPath { get; set; }
    public string? FinalPath { get; set; }
    public AnchorPoint EnteringAnchor { get; set; } = new(15, 55);
    public AnchorPoint ExitingAnchor { get; set; } = new(85, 55);
    public double AdvanceWidth { get; set; } = 75;
    public double AdvanceHeight { get; set; } = 75;
}

public sealed class FeaturicAssemblyRule
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string TargetGlyphId { get; set; } = "*";
    public string ContextPosition { get; set; } = "any"; // any | initial | medial | final | after | before
    public string? ContextGlyphId { get; set; }
    public string AppendSvgPath { get; set; } = "";
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class WritingSystemConfig
{
    public string Name { get; set; } = "Agnostic Cursive Matrix";
    public FlowDirection FlowDirection { get; set; } = FlowDirection.RTL;
    public bool CursiveBonding { get; set; } = true;
    public bool ContextualVariants { get; set; } = true;
    public double GlyphScale { get; set; } = 0.72;
    public double LetterSpacing { get; set; } = 6;
    public double LineSpacing { get; set; } = 88;
    public double WrapLimit { get; set; } = 680;
    public Dictionary<string, GlyphDefinition> Glyphs { get; set; } = new();
    public List<FeaturicAssemblyRule> FeaturicRules { get; set; } = new();
}

public sealed class SyntaxConfig
{
    [JsonPropertyName("alignment")]
    public AlignmentSystem Alignment { get; set; } = AlignmentSystem.NomAcc;

    [JsonPropertyName("adjectiveOrder")]
    public ModifierOrder AdjectiveOrder { get; set; } = ModifierOrder.BeforeNoun;

    [JsonPropertyName("determinerOrder")]
    public ModifierOrder DeterminerOrder { get; set; } = ModifierOrder.BeforeNoun;

    [JsonPropertyName("adpositionType")]
    public AdpositionOrder AdpositionType { get; set; } = AdpositionOrder.Preposition;

    [JsonPropertyName("questionStrategy")]
    public QuestionStrategy QuestionStrategy { get; set; } = QuestionStrategy.Inversion;

    [JsonPropertyName("proDrop")]
    public bool ProDrop { get; set; }

    [JsonPropertyName("polypersonalAgreement")]
    public bool PolypersonalAgreement { get; set; }

    [JsonPropertyName("allowEmbeddedClauses")]
    public bool AllowEmbeddedClauses { get; set; }

    [JsonPropertyName("allowRelativeClauses")]
    public bool AllowRelativeClauses { get; set; }

    [JsonPropertyName("allowPassive")]
    public bool AllowPassive { get; set; }

    [JsonPropertyName("maxClauseDepth")]
    public int MaxClauseDepth { get; set; } = 1;

    [JsonPropertyName("customRules")]
    public List<string> CustomRules { get; set; } = new();
}

public sealed class PipelineSelection
{
    [JsonPropertyName("syntaxGeneratorId")]
    public string SyntaxGeneratorId { get; set; } = "configurable-syntax";

    [JsonPropertyName("morphologyRealizerId")]
    public string MorphologyRealizerId { get; set; } = "standard-morphology";

    [JsonPropertyName("orthographyRendererId")]
    public string OrthographyRendererId { get; set; } = "standard-orthography";

    [JsonPropertyName("enabledPhonoPluginRules")]
    public List<string> EnabledPhonoPluginRules { get; set; } = new();
}

public sealed record GraphemeToken(
    string Id,
    string Grapheme,
    int WordIndex,
    string PositionInWord // "isolated" | "initial" | "medial" | "final"
);

public sealed record OrthographyResult(string Text, IReadOnlyList<GraphemeToken> Tokens);

public sealed record DiagnosticIssue(DiagnosticSeverity Severity, string Subsystem, string Message);

public sealed class LanguageProject
{
    public int SchemaVersion { get; set; } = 2;
    public string Name { get; set; } = "My Conlang";
    public List<Phoneme> Phonemes { get; set; } = new();
    public List<string> Consonants { get; set; } = new();
    public List<string> Vowels { get; set; } = new();
    public List<SyllableTemplate> SyllableTemplates { get; set; } = new();
    public PhonotacticsConfig Phonotactics { get; set; } = new();
    public GeneratorOptions GeneratorOptions { get; set; } = new();
    public StressPattern StressPattern { get; set; } = StressPattern.None;
    public List<PhonologyRuleSpec> PhonologyRules { get; set; } = new();
    public List<LexemeEntry> Lexemes { get; set; } = new();
    public List<AffixEntry> Affixes { get; set; } = new();
    public WordOrder WordOrder { get; set; } = WordOrder.SVO;
    public ClauseTemplate ClauseTemplate { get; set; } = ClauseTemplate.Transitive;
    public SyntaxConfig SyntaxConfig { get; set; } = new();
    public List<OrthRuleEntry> OrthographyRules { get; set; } = new();
    public List<OrthExceptionEntry> OrthographyExceptions { get; set; } = new();
    public CapitalizationMode Capitalization { get; set; } = CapitalizationMode.SentenceInitial;
    public WritingSystemConfig WritingSystem { get; set; } = new();
    public PipelineSelection Pipeline { get; set; } = new();
}
