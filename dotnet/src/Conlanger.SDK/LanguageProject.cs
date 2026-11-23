using System.Text.Json.Serialization;

namespace Conlanger.SDK;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WordOrder { SVO, SOV, VSO, VOS, OVS, OSV }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClauseTemplate { Intransitive, Transitive, Copular }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CapitalizationMode { None, SentenceInitial, AllWords }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FlowDirection { LTR, RTL, TTB_LTR, TTB_RTL, Boustrophedon }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiagnosticSeverity { Error, Warning }

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
}

public sealed class AffixEntry
{
    public string Name { get; set; } = "";
    public string Pos { get; set; } = "N";
    public string FeatureName { get; set; } = "num";
    public string FeatureValue { get; set; } = "PL";
    public string Affix { get; set; } = "";
    public bool IsPrefix { get; set; }
    public bool Enabled { get; set; } = true;
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
    public string Name { get; set; } = "Proto-Conlang";
    public List<Phoneme> Phonemes { get; set; } = new();
    public List<string> Consonants { get; set; } = new();
    public List<string> Vowels { get; set; } = new();
    public List<SyllableTemplate> SyllableTemplates { get; set; } = new();
    public PhonotacticsConfig Phonotactics { get; set; } = new();
    public GeneratorOptions GeneratorOptions { get; set; } = new();
    public List<PhonologyRuleSpec> PhonologyRules { get; set; } = new();
    public List<LexemeEntry> Lexemes { get; set; } = new();
    public List<AffixEntry> Affixes { get; set; } = new();
    public WordOrder WordOrder { get; set; } = WordOrder.SVO;
    public ClauseTemplate ClauseTemplate { get; set; } = ClauseTemplate.Transitive;
    public List<OrthRuleEntry> OrthographyRules { get; set; } = new();
    public List<OrthExceptionEntry> OrthographyExceptions { get; set; } = new();
    public CapitalizationMode Capitalization { get; set; } = CapitalizationMode.SentenceInitial;
    public WritingSystemConfig WritingSystem { get; set; } = new();
}
