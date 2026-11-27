using System.Text;
using System.Text.Json;
using Conlanger.SDK;

namespace Conlanger.Core;

public sealed record ShapedGlyph(
    int Index,
    GraphemeToken Token,
    double X,
    double Y,
    string SvgPath,
    IReadOnlyList<string> ExtraPaths,
    AnchorPoint EnteringWorld,
    AnchorPoint ExitingWorld,
    string VariantUsed,
    IReadOnlyList<string> AppliedFeatures
);

public sealed record ShapingTraceEntry(int Step, string TokenId, string Variant, string AnchorMode, string Position);

public sealed record ShapingResult(
    IReadOnlyList<ShapedGlyph> Glyphs,
    IReadOnlyList<ShapingTraceEntry> Trace,
    double Width,
    double Height
);

public static class WritingSystemEngine
{
    private static GlyphDefinition MakeGlyph(
        string id,
        string label,
        string svgPath,
        double advanceWidth = 72,
        double advanceHeight = 76,
        AnchorPoint? enter = null,
        AnchorPoint? exit = null,
        string? initialPath = null,
        string? medialPath = null,
        string? finalPath = null) => new()
    {
        Id = id,
        Label = label,
        SvgPath = svgPath,
        InitialPath = initialPath,
        MedialPath = medialPath,
        FinalPath = finalPath,
        EnteringAnchor = enter ?? new AnchorPoint(15, 55),
        ExitingAnchor = exit ?? new AnchorPoint(85, 55),
        AdvanceWidth = advanceWidth,
        AdvanceHeight = advanceHeight
    };

    private static Dictionary<string, GlyphDefinition> CreateDefaultGlyphs() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = MakeGlyph("a", "a", "M 15 55 C 15 30, 55 25, 65 45 L 65 70 C 65 75, 80 75, 85 55", 75),
        ["b"] = MakeGlyph("b", "b", "M 15 55 L 30 16 L 30 72 C 68 72, 72 34, 30 44 M 64 58 L 85 55"),
        ["c"] = MakeGlyph("c", "c", "M 15 55 C 35 30, 70 28, 72 38 M 36 45 C 25 68, 65 72, 85 55", 70),
        ["d"] = MakeGlyph("d", "d", "M 15 55 C 22 34, 64 34, 64 56 C 64 74, 24 74, 30 50 M 64 16 L 64 72 L 85 55"),
        ["e"] = MakeGlyph("e", "e", "M 15 55 L 70 50 C 75 30, 35 25, 30 50 C 25 75, 70 75, 85 55", 75),
        ["f"] = MakeGlyph("f", "f", "M 15 55 C 35 35, 50 15, 65 18 L 45 85 M 28 45 L 85 55", 72, 80),
        ["g"] = MakeGlyph("g", "g", "M 15 55 C 22 32, 62 32, 62 52 C 62 70, 26 70, 26 52 M 62 38 L 62 82 C 62 92, 32 92, 28 80 L 85 55", 72, 82),
        ["h"] = MakeGlyph("h", "h", "M 15 55 L 28 16 L 28 72 M 28 48 C 44 28, 66 32, 66 68 L 85 55"),
        ["i"] = MakeGlyph("i", "i", "M 15 55 C 35 55, 45 30, 50 25 C 50 45, 55 70, 85 55 M 48 12 A 4 4 0 1 0 56 12 A 4 4 0 1 0 48 12", 65),
        ["j"] = MakeGlyph("j", "j", "M 15 55 L 54 34 L 54 80 C 54 92, 28 92, 24 80 M 50 14 A 4 4 0 1 0 58 14 A 4 4 0 1 0 50 14 M 54 56 L 85 55", 66, 82),
        ["k"] = MakeGlyph("k", "k", "M 15 55 L 35 15 L 35 72 M 66 30 L 35 52 L 85 55", 75),
        ["l"] = MakeGlyph("l", "l", "M 15 55 C 35 45, 55 12, 48 12 C 40 12, 42 70, 85 55", 68),
        ["m"] = MakeGlyph("m", "m", "M 15 55 L 25 32 L 35 65 L 50 32 L 62 65 L 85 55", 78),
        ["n"] = MakeGlyph("n", "n", "M 15 55 L 32 30 L 34 68 M 34 45 C 48 25, 65 28, 66 65 L 85 55", 75),
        ["o"] = MakeGlyph("o", "o", "M 15 55 C 25 35, 65 30, 65 52 C 65 72, 30 72, 30 52 C 30 42, 60 55, 85 55", 75),
        ["p"] = MakeGlyph("p", "p", "M 15 55 L 35 28 L 35 88 M 35 38 C 68 25, 72 60, 35 58 L 85 55", 75, 80),
        ["q"] = MakeGlyph("q", "q", "M 15 55 C 24 32, 64 32, 64 52 C 64 70, 26 70, 26 52 M 64 36 L 64 88 L 85 55", 72, 82),
        ["r"] = MakeGlyph("r", "r", "M 15 55 L 35 30 L 35 68 M 35 44 C 48 28, 65 28, 70 40 L 85 55", 70),
        ["s"] = MakeGlyph("s", "s", "M 15 55 L 42 30 C 65 38, 25 62, 55 70 L 85 55", 70),
        ["sh"] = MakeGlyph("sh", "sh (ʃ)", "M 15 55 C 30 22, 48 22, 52 55 C 56 80, 72 80, 85 55 M 32 18 L 68 18", 78),
        ["t"] = MakeGlyph("t", "t", "M 15 55 L 45 15 L 48 68 C 52 74, 72 70, 85 55 M 28 32 L 66 32", 70),
        ["u"] = MakeGlyph("u", "u", "M 15 55 L 32 30 L 35 65 C 40 72, 62 72, 65 30 L 68 65 L 85 55", 75),
        ["v"] = MakeGlyph("v", "v", "M 15 55 L 30 32 L 50 70 L 68 32 L 85 55", 70),
        ["w"] = MakeGlyph("w", "w", "M 15 55 L 24 32 L 36 70 L 50 40 L 64 70 L 74 32 L 85 55", 78),
        ["x"] = MakeGlyph("x", "x", "M 15 55 L 32 30 L 68 70 M 68 30 L 32 70 L 85 55", 72),
        ["y"] = MakeGlyph("y", "y", "M 15 55 L 30 32 L 48 62 M 68 32 L 42 88 L 85 55", 72, 82),
        ["z"] = MakeGlyph("z", "z", "M 15 55 L 32 32 L 66 32 L 32 68 L 85 55", 75),
        ["'"] = MakeGlyph("'", "'", "M 15 55 M 48 16 C 54 24, 52 34, 44 38 M 60 55 L 85 55", 42),
        ["-"] = MakeGlyph("-", "-", "M 15 55 L 32 55 M 36 52 L 64 52 M 68 55 L 85 55", 48)
    };

    private static Dictionary<string, GlyphDefinition> CreateLatinPrintGlyphs()
    {
        var g = CreateDefaultGlyphs();
        g["a"] = MakeGlyph("a", "a", "M 32 38 C 46 28, 66 32, 66 52 L 66 72 M 66 50 C 30 48, 28 74, 66 70");
        g["b"] = MakeGlyph("b", "b", "M 30 16 L 30 72 M 30 44 C 72 34, 72 74, 30 70");
        g["c"] = MakeGlyph("c", "c", "M 68 38 C 30 26, 28 76, 68 64");
        g["d"] = MakeGlyph("d", "d", "M 66 16 L 66 72 M 66 44 C 24 34, 24 74, 66 70");
        g["e"] = MakeGlyph("e", "e", "M 30 52 L 68 52 C 70 30, 30 28, 30 52 C 30 74, 62 74, 68 64");
        g["f"] = MakeGlyph("f", "f", "M 64 20 C 48 14, 42 22, 42 74 M 28 42 L 62 42");
        g["g"] = MakeGlyph("g", "g", "M 66 36 C 28 28, 28 70, 66 62 M 66 34 L 66 80 C 66 94, 32 94, 28 80", 72, 84);
        g["h"] = MakeGlyph("h", "h", "M 30 16 L 30 72 M 30 48 C 42 32, 68 32, 68 72");
        g["i"] = MakeGlyph("i", "i", "M 50 34 L 50 72 M 46 16 A 4 4 0 1 0 54 16 A 4 4 0 1 0 46 16", 52);
        g["j"] = MakeGlyph("j", "j", "M 54 34 L 54 80 C 54 94, 30 92, 26 80 M 50 16 A 4 4 0 1 0 58 16 A 4 4 0 1 0 50 16", 56, 84);
        g["k"] = MakeGlyph("k", "k", "M 32 16 L 32 72 M 68 34 L 32 54 L 68 72");
        g["l"] = MakeGlyph("l", "l", "M 48 16 L 48 68 C 48 74, 58 74, 62 70", 52);
        g["m"] = MakeGlyph("m", "m", "M 22 72 L 22 36 C 34 28, 48 34, 48 72 M 48 44 C 58 28, 74 34, 74 72", 78);
        g["n"] = MakeGlyph("n", "n", "M 30 34 L 30 72 M 30 46 C 44 28, 68 32, 68 72");
        g["o"] = MakeGlyph("o", "o", "M 50 32 C 74 32, 74 72, 50 72 C 26 72, 26 32, 50 32");
        g["p"] = MakeGlyph("p", "p", "M 32 34 L 32 90 M 32 42 C 72 30, 72 72, 32 68", 72, 84);
        g["q"] = MakeGlyph("q", "q", "M 66 36 C 28 28, 28 72, 66 66 M 66 34 L 66 90", 72, 84);
        g["r"] = MakeGlyph("r", "r", "M 34 34 L 34 72 M 34 46 C 44 30, 64 30, 68 38", 64);
        g["s"] = MakeGlyph("s", "s", "M 64 38 C 36 26, 26 48, 50 52 C 74 56, 62 78, 30 66", 68);
        g["t"] = MakeGlyph("t", "t", "M 46 18 L 46 68 C 46 74, 60 74, 66 68 M 30 34 L 64 34", 64);
        g["u"] = MakeGlyph("u", "u", "M 30 34 L 30 62 C 30 76, 66 76, 66 62 L 66 34");
        g["v"] = MakeGlyph("v", "v", "M 28 34 L 50 72 L 72 34", 68);
        g["w"] = MakeGlyph("w", "w", "M 20 34 L 34 72 L 50 42 L 66 72 L 80 34", 78);
        g["x"] = MakeGlyph("x", "x", "M 28 34 L 70 72 M 70 34 L 28 72", 68);
        g["y"] = MakeGlyph("y", "y", "M 28 34 L 50 66 M 70 34 L 38 90", 72, 84);
        g["z"] = MakeGlyph("z", "z", "M 28 34 L 70 34 L 28 72 L 70 72", 68);
        g["'"] = MakeGlyph("'", "'", "M 48 16 C 54 24, 52 34, 44 38", 34);
        g["-"] = MakeGlyph("-", "-", "M 30 52 L 70 52", 46);
        g["ch"] = MakeGlyph("ch", "ch", "M 44 38 C 18 28, 18 74, 44 66 M 56 18 L 56 72 M 56 48 C 68 34, 84 36, 84 72", 82);
        g["sh"] = MakeGlyph("sh", "sh", "M 44 38 C 20 30, 18 48, 34 52 C 50 56, 42 74, 20 66 M 58 18 L 58 72 M 58 48 C 70 34, 86 36, 86 72", 84);
        g["th"] = MakeGlyph("th", "th", "M 32 20 L 32 70 M 20 36 L 44 36 M 56 18 L 56 72 M 56 48 C 68 34, 84 36, 84 72", 82);
        g["ng"] = MakeGlyph("ng", "ng", "M 20 72 L 20 36 C 32 28, 46 34, 46 72 M 80 38 C 54 30, 54 68, 80 62 M 80 34 L 80 82 C 80 92, 54 92, 52 82", 84);
        g["ee"] = MakeGlyph("ee", "ee", "M 18 52 L 44 52 C 46 34, 18 32, 18 52 C 18 72, 40 72, 44 64 M 56 52 L 82 52 C 84 34, 56 32, 56 52 C 56 72, 78 72, 82 64", 84);
        g["oo"] = MakeGlyph("oo", "oo", "M 32 34 C 48 34, 48 70, 32 70 C 16 70, 16 34, 32 34 M 68 34 C 84 34, 84 70, 68 70 C 52 70, 52 34, 68 34", 84);
        return g;
    }

    public static WritingSystemConfig CreateDefaultConfig() => new()
    {
        Name = "Agnostic Cursive Matrix",
        FlowDirection = FlowDirection.RTL,
        CursiveBonding = true,
        ContextualVariants = true,
        GlyphScale = 0.72,
        LetterSpacing = 6,
        LineSpacing = 88,
        WrapLimit = 680,
        Glyphs = CreateDefaultGlyphs(),
        FeaturicRules = new()
        {
            new()
            {
                Id = "final-flourish",
                Name = "Word-Final Flourish Tick",
                TargetGlyphId = "*",
                ContextPosition = "final",
                AppendSvgPath = "M 80 48 Q 94 35 90 66",
                Enabled = true
            }
        }
    };

    private static WritingSystemConfig CreateSpanishWritingSystem()
    {
        var g = CreateDefaultGlyphs();
        g["b"] = MakeGlyph("b", "b", "M 15 55 L 30 16 L 30 70 C 66 70, 68 36, 30 44 L 85 55");
        g["d"] = MakeGlyph("d", "d", "M 15 55 C 24 34, 62 34, 62 56 C 62 72, 24 72, 30 50 M 62 16 L 62 70 L 85 55");
        g["ñ"] = MakeGlyph("ñ", "ñ", "M 15 55 L 32 30 L 34 68 M 34 45 C 48 25, 65 28, 66 65 L 85 55 M 28 18 Q 42 10, 50 18 Q 58 26, 72 18", 75);
        g["á"] = MakeGlyph("á", "á", g["a"].SvgPath + " M 42 20 L 62 10", 75);
        g["é"] = MakeGlyph("é", "é", g["e"].SvgPath + " M 42 20 L 62 10", 75);
        g["í"] = MakeGlyph("í", "í", "M 15 55 C 35 55, 45 30, 50 25 C 50 45, 55 70, 85 55 M 44 18 L 62 8", 65);
        g["ó"] = MakeGlyph("ó", "ó", g["o"].SvgPath + " M 42 20 L 62 10", 75);
        g["ú"] = MakeGlyph("ú", "ú", g["u"].SvgPath + " M 42 20 L 62 10", 75);
        g["ü"] = MakeGlyph("ü", "ü", g["u"].SvgPath + " M 36 16 A 3 3 0 1 0 42 16 A 3 3 0 1 0 36 16 M 56 16 A 3 3 0 1 0 62 16 A 3 3 0 1 0 56 16", 75);
        g["ll"] = MakeGlyph("ll", "ll", "M 15 55 C 28 45, 40 14, 34 14 C 28 14, 30 68, 52 55 C 62 45, 72 14, 66 14 C 60 14, 62 68, 85 55", 78);
        g["rr"] = MakeGlyph("rr", "rr", "M 15 55 L 26 32 L 26 68 M 26 44 C 36 30, 48 30, 50 55 L 58 32 L 58 68 M 58 44 C 68 30, 78 30, 85 55", 78);
        g["ch"] = MakeGlyph("ch", "ch", "M 15 55 C 30 34, 48 34, 46 46 C 30 48, 28 70, 52 58 L 58 18 L 58 70 M 58 48 C 68 34, 78 36, 85 55", 80);

        return new WritingSystemConfig
        {
            Name = "Romance Calligraphic Cursive (Español)",
            FlowDirection = FlowDirection.LTR,
            CursiveBonding = true,
            ContextualVariants = true,
            GlyphScale = 0.68,
            LetterSpacing = 3,
            LineSpacing = 86,
            WrapLimit = 680,
            Glyphs = g,
            FeaturicRules = new()
            {
                new() { Id = "spanish-final-sweep", Name = "Word-Final Romance Flourish", TargetGlyphId = "*", ContextPosition = "final", AppendSvgPath = "M 80 50 Q 94 36 90 68", Enabled = true }
            }
        };
    }

    private static WritingSystemConfig CreateGermanWritingSystem()
    {
        var g = CreateLatinPrintGlyphs();
        g["a"] = MakeGlyph("a", "a", "M 32 38 L 50 28 L 66 38 L 66 72 M 66 48 L 32 54 L 32 72 L 66 68");
        g["e"] = MakeGlyph("e", "e", "M 30 52 L 68 48 L 62 32 L 34 32 L 30 52 L 34 72 L 66 66");
        g["h"] = MakeGlyph("h", "h", "M 30 16 L 30 72 M 30 46 L 52 32 L 68 42 L 64 74");
        g["k"] = MakeGlyph("k", "k", "M 32 16 L 32 72 M 68 32 L 32 52 L 68 72 M 26 20 L 40 16");
        g["s"] = MakeGlyph("s", "s", "M 64 36 L 38 30 L 32 46 L 64 56 L 58 72 L 30 66", 68);
        g["ä"] = MakeGlyph("ä", "ä", g["a"].SvgPath + " M 36 16 L 42 20 L 36 24 L 30 20 M 60 16 L 66 20 L 60 24 L 54 20");
        g["ö"] = MakeGlyph("ö", "ö", g["o"].SvgPath + " M 36 16 L 42 20 L 36 24 L 30 20 M 60 16 L 66 20 L 60 24 L 54 20");
        g["ü"] = MakeGlyph("ü", "ü", g["u"].SvgPath + " M 36 16 L 42 20 L 36 24 L 30 20 M 60 16 L 66 20 L 60 24 L 54 20");
        g["ß"] = MakeGlyph("ß", "ß", "M 30 82 L 30 26 C 34 14, 68 14, 64 34 L 46 44 C 74 48, 74 74, 42 70", 72, 82);
        g["sch"] = MakeGlyph("sch", "sch", "M 34 36 L 16 32 L 14 48 L 34 56 L 26 70 M 58 38 L 40 34 L 40 68 L 58 66 M 68 18 L 68 72 M 68 46 L 86 36 L 86 72", 88);
        g["pf"] = MakeGlyph("pf", "pf", "M 20 34 L 20 88 M 20 40 L 46 34 L 46 64 L 20 64 M 78 20 L 62 18 L 62 78 M 50 42 L 78 42", 84);

        return new WritingSystemConfig
        {
            Name = "Geometric Fraktur-Latin (Deutsch)",
            FlowDirection = FlowDirection.LTR,
            CursiveBonding = false,
            ContextualVariants = true,
            GlyphScale = 0.68,
            LetterSpacing = 4,
            LineSpacing = 86,
            WrapLimit = 680,
            Glyphs = g,
            FeaturicRules = new()
            {
                new() { Id = "fraktur-initial-crest", Name = "Initial Blackletter Crest", TargetGlyphId = "*", ContextPosition = "initial", AppendSvgPath = "M 14 26 L 26 16 L 34 22", Enabled = true }
            }
        };
    }

    private static WritingSystemConfig CreateMandarinWritingSystem()
    {
        var g = new Dictionary<string, GlyphDefinition>(StringComparer.OrdinalIgnoreCase);
        GlyphDefinition Vg(string id, string path) =>
            MakeGlyph(id, id, path, 76, 80, new AnchorPoint(50, 10), new AnchorPoint(50, 90));

        (string Id, string Path)[] cons =
        [
            ("b", "M 24 26 L 76 26 L 72 74 L 28 74 L 24 26 M 50 18 L 50 82"),
            ("p", "M 24 24 L 76 24 M 36 24 L 36 78 M 64 24 L 64 78 M 22 52 L 78 52"),
            ("m", "M 22 24 L 78 24 M 28 24 L 28 78 M 50 24 L 50 78 M 72 24 L 72 78"),
            ("f", "M 24 28 L 76 28 M 50 18 L 50 82 M 26 54 L 74 54"),
            ("d", "M 24 26 L 76 26 M 50 18 L 50 80 L 38 72"),
            ("t", "M 20 38 L 80 38 M 50 16 L 50 82 M 28 60 L 72 78"),
            ("n", "M 28 24 L 28 78 M 28 46 L 72 46 L 72 78"),
            ("l", "M 34 20 L 34 76 M 66 20 L 66 80 L 54 70"),
            ("g", "M 26 24 L 74 24 M 50 24 L 32 76 M 44 50 L 74 50 L 74 78"),
            ("k", "M 28 22 L 28 80 M 28 48 L 72 26 M 46 42 L 76 78"),
            ("h", "M 22 28 L 78 28 M 36 28 L 28 78 M 64 28 L 74 78 M 28 56 L 72 56"),
            ("j", "M 26 28 L 74 28 M 50 18 L 50 78 C 50 86, 32 84, 28 74"),
            ("q", "M 24 26 L 76 26 L 70 64 M 50 18 L 50 82 M 26 52 L 74 52"),
            ("x", "M 26 28 L 74 76 M 74 28 L 26 76 M 24 52 L 76 52"),
            ("zh", "M 24 24 L 76 24 L 76 52 L 24 52 L 24 78 L 76 78 M 50 16 L 50 84"),
            ("ch", "M 22 32 L 78 32 M 36 20 L 36 74 M 64 20 L 64 74 M 24 68 L 76 68"),
            ("sh", "M 24 22 L 24 78 M 50 22 L 50 78 M 76 22 L 76 78 M 22 76 L 78 76"),
            ("r", "M 28 24 L 72 24 L 72 76 L 28 76 L 28 24 M 28 50 L 72 50"),
            ("z", "M 26 26 L 74 26 L 26 74 L 74 74"),
            ("c", "M 26 28 L 74 28 L 70 74 L 26 74"),
            ("s", "M 32 24 L 72 24 L 50 52 L 74 78 L 28 78"),
            ("w", "M 22 28 L 40 76 L 50 44 L 60 76 L 78 28"),
            ("y", "M 26 24 L 50 52 M 74 24 L 50 52 L 50 82")
        ];
        foreach (var (id, path) in cons) g[id] = Vg(id, path);

        var tones = new Dictionary<int, string>
        {
            [0] = "",
            [1] = "M 30 14 L 70 14",
            [2] = "M 34 18 L 68 10",
            [3] = "M 32 10 L 50 20 L 68 10",
            [4] = "M 32 10 L 68 18"
        };

        void AddTonedVowels(string basePath, params string[] chars)
        {
            for (int t = 0; t < chars.Length; t++)
                g[chars[t]] = Vg(chars[t], string.IsNullOrEmpty(tones[t]) ? basePath : $"{basePath} {tones[t]}");
        }

        AddTonedVowels("M 28 32 L 72 32 M 50 24 L 50 80 M 24 58 L 76 58", "a", "ā", "á", "ǎ", "à");
        AddTonedVowels("M 30 30 L 70 30 L 70 74 L 30 74 L 30 30", "o", "ō", "ó", "ǒ", "ò");
        AddTonedVowels("M 26 32 L 74 32 M 26 52 L 70 52 M 26 74 L 74 74 M 36 32 L 36 74", "e", "ē", "é", "ě", "è");
        AddTonedVowels("M 50 24 L 50 80 M 30 78 L 70 78", "i", "ī", "í", "ǐ", "ì");
        AddTonedVowels("M 28 32 L 28 74 L 72 74 L 72 32", "u", "ū", "ú", "ǔ", "ù");
        g["ü"] = Vg("ü", "M 28 32 L 28 74 L 72 74 L 72 32 M 38 20 A 3 3 0 1 0 44 20 A 3 3 0 1 0 38 20 M 56 20 A 3 3 0 1 0 62 20 A 3 3 0 1 0 56 20");
        g["ɤ"] = Vg("ɤ", "M 26 32 L 74 32 M 26 52 L 70 52 M 26 74 L 74 74");
        g["-"] = Vg("-", "M 50 28 L 50 72");

        return new WritingSystemConfig
        {
            Name = "Vertical Sinographic Grid (普通话)",
            FlowDirection = FlowDirection.TTB_RTL,
            CursiveBonding = false,
            ContextualVariants = true,
            GlyphScale = 0.68,
            LetterSpacing = 8,
            LineSpacing = 86,
            WrapLimit = 380,
            Glyphs = g,
            FeaturicRules = new()
            {
                new() { Id = "sinographic-hook", Name = "Brush Hook Terminal", TargetGlyphId = "*", ContextPosition = "final", AppendSvgPath = "M 72 74 L 84 64", Enabled = true }
            }
        };
    }

    private static WritingSystemConfig CreateArabicWritingSystem()
    {
        var g = new Dictionary<string, GlyphDefinition>(StringComparer.OrdinalIgnoreCase);
        void Ar(string id, string label, string iso, string? init = null, string? med = null, string? fin = null) =>
            g[id] = MakeGlyph(id, label, iso, 70, 80, new AnchorPoint(15, 58), new AnchorPoint(85, 58), init, med, fin);

        Ar("a", "ا (a)", "M 50 14 L 50 72", "M 50 14 L 50 72", "M 50 14 L 50 58 L 85 58", "M 50 14 L 50 58 L 85 58");
        Ar("aa", "آ (aa)", "M 50 16 L 50 74 M 36 10 Q 50 4 64 10", null, "M 50 16 L 50 58 L 85 58", "M 50 16 L 50 58 L 85 58");
        Ar("b", "ب (b)",
            "M 22 44 C 22 72, 78 72, 78 44 M 48 80 A 3 3 0 1 0 54 80 A 3 3 0 1 0 48 80",
            "M 15 58 L 58 58 L 62 36 M 56 76 A 3 3 0 1 0 62 76 A 3 3 0 1 0 56 76",
            "M 15 58 L 50 58 L 52 38 L 54 58 L 85 58 M 48 76 A 3 3 0 1 0 54 76 A 3 3 0 1 0 48 76",
            "M 20 44 C 22 72, 72 72, 74 58 L 85 58 M 48 80 A 3 3 0 1 0 54 80 A 3 3 0 1 0 48 80");
        Ar("t", "ت (t)",
            "M 22 44 C 22 72, 78 72, 78 44 M 40 34 A 3 3 0 1 0 46 34 A 3 3 0 1 0 40 34 M 54 34 A 3 3 0 1 0 60 34 A 3 3 0 1 0 54 34",
            "M 15 58 L 58 58 L 62 36 M 52 24 A 3 3 0 1 0 58 24 A 3 3 0 1 0 52 24 M 64 24 A 3 3 0 1 0 70 24 A 3 3 0 1 0 64 24",
            "M 15 58 L 50 58 L 52 38 L 54 58 L 85 58 M 44 26 A 3 3 0 1 0 50 26 A 3 3 0 1 0 44 26 M 56 26 A 3 3 0 1 0 62 26 A 3 3 0 1 0 56 26",
            "M 20 44 C 22 72, 72 72, 74 58 L 85 58 M 40 34 A 3 3 0 1 0 46 34 A 3 3 0 1 0 40 34 M 54 34 A 3 3 0 1 0 60 34 A 3 3 0 1 0 54 34");
        Ar("th", "ث (th)", "M 22 44 C 22 72, 78 72, 78 44 M 40 36 A 3 3 0 1 0 46 36 A 3 3 0 1 0 40 36 M 54 36 A 3 3 0 1 0 60 36 A 3 3 0 1 0 54 36 M 47 26 A 3 3 0 1 0 53 26 A 3 3 0 1 0 47 26");
        Ar("j", "ج (j)",
            "M 34 32 L 72 32 C 30 48, 28 84, 72 78 M 48 56 A 3 3 0 1 0 54 56 A 3 3 0 1 0 48 56",
            "M 15 58 L 48 58 L 68 38 L 40 38 M 48 72 A 3 3 0 1 0 54 72 A 3 3 0 1 0 48 72",
            "M 15 58 L 85 58 M 42 42 L 66 42 L 46 58 M 48 72 A 3 3 0 1 0 54 72 A 3 3 0 1 0 48 72");
        Ar("h", "ح/ه (h)", "M 36 32 L 72 32 C 30 48, 28 84, 72 78", "M 15 58 L 52 58 L 68 38 L 40 38", "M 15 58 L 85 58 M 42 42 L 66 42 L 46 58");
        Ar("kh", "خ (kh)", "M 36 36 L 72 36 C 30 50, 28 84, 72 78 M 50 20 A 3 3 0 1 0 56 20 A 3 3 0 1 0 50 20", "M 15 58 L 52 58 L 68 38 L 40 38 M 50 22 A 3 3 0 1 0 56 22 A 3 3 0 1 0 50 22");
        Ar("d", "د (d)", "M 42 32 L 66 56 L 34 68", null, "M 44 36 L 66 58 L 85 58", "M 44 36 L 66 58 L 85 58");
        Ar("dh", "ذ (dh)", "M 42 34 L 66 56 L 34 68 M 48 20 A 3 3 0 1 0 54 20 A 3 3 0 1 0 48 20");
        Ar("r", "ر (r)", "M 62 42 C 62 66, 44 82, 26 84", null, "M 85 58 L 64 58 C 56 76, 40 84, 24 84", "M 85 58 L 64 58 C 56 76, 40 84, 24 84");
        Ar("z", "ز (z)", "M 62 42 C 62 66, 44 82, 26 84 M 56 24 A 3 3 0 1 0 62 24 A 3 3 0 1 0 56 24");
        Ar("s", "س (s)",
            "M 78 42 L 72 56 L 62 42 L 56 56 L 46 42 C 42 74, 20 74, 20 50",
            "M 15 58 L 46 58 L 54 40 L 62 58 L 70 40 L 78 58",
            "M 15 58 L 42 58 L 50 40 L 58 58 L 66 40 L 74 58 L 85 58");
        Ar("sh", "ش (sh)", "M 78 42 L 72 56 L 62 42 L 56 56 L 46 42 C 42 74, 20 74, 20 50 M 54 24 A 3 3 0 1 0 60 24 A 3 3 0 1 0 54 24 M 68 24 A 3 3 0 1 0 74 24 A 3 3 0 1 0 68 24 M 61 14 A 3 3 0 1 0 67 14 A 3 3 0 1 0 61 14");
        Ar("gh", "غ (gh)", "M 66 36 C 46 30, 44 48, 66 52 C 30 62, 32 88, 72 82 M 52 20 A 3 3 0 1 0 58 20 A 3 3 0 1 0 52 20");
        Ar("f", "ف (f)", "M 68 38 C 52 28, 48 52, 68 50 L 72 64 C 50 72, 24 68, 22 48 M 56 20 A 3 3 0 1 0 62 20 A 3 3 0 1 0 56 20", "M 15 58 L 68 58 C 72 34, 52 34, 54 58 M 56 22 A 3 3 0 1 0 62 22 A 3 3 0 1 0 56 22");
        Ar("q", "ق (q)", "M 68 38 C 52 28, 48 52, 68 50 L 70 68 C 50 88, 22 82, 22 52 M 48 20 A 3 3 0 1 0 54 20 A 3 3 0 1 0 48 20 M 62 20 A 3 3 0 1 0 68 20 A 3 3 0 1 0 62 20");
        Ar("k", "ك (k)", "M 68 20 L 68 68 L 24 68 L 24 54 M 44 38 L 54 46 L 44 52", "M 15 58 L 58 58 L 46 38 L 68 22");
        Ar("l", "ل (l)", "M 64 16 L 64 68 C 60 88, 24 88, 24 58", "M 15 58 L 62 58 L 62 16", "M 15 58 L 54 58 L 54 16 L 54 58 L 85 58");
        Ar("m", "م (m)", "M 54 46 C 72 34, 74 62, 54 58 L 32 60 L 32 88", "M 15 58 L 52 58 C 68 42, 68 62, 52 58", "M 15 58 L 52 58 C 66 42, 66 62, 52 58 L 85 58");
        Ar("n", "ن (n)", "M 72 44 C 72 82, 26 82, 26 44 M 48 32 A 3 3 0 1 0 54 32 A 3 3 0 1 0 48 32", "M 15 58 L 58 58 L 62 36 M 56 22 A 3 3 0 1 0 62 22 A 3 3 0 1 0 56 22", "M 15 58 L 50 58 L 52 38 L 54 58 L 85 58 M 48 24 A 3 3 0 1 0 54 24 A 3 3 0 1 0 48 24");
        Ar("w", "و (w)", "M 60 40 C 44 34, 42 56, 62 54 C 62 74, 40 84, 24 80", null, "M 85 58 L 62 58 C 46 42, 46 58, 62 58 C 58 76, 38 84, 24 80");
        Ar("y", "ي (y)", "M 66 40 C 46 38, 46 54, 68 58 C 68 82, 24 84, 24 56 M 42 84 A 3 3 0 1 0 48 84 A 3 3 0 1 0 42 84 M 56 84 A 3 3 0 1 0 62 84 A 3 3 0 1 0 56 84");
        Ar("i", "ي/i", g["y"].SvgPath, "M 15 58 L 58 58 L 62 36 M 52 76 A 3 3 0 1 0 58 76 A 3 3 0 1 0 52 76 M 64 76 A 3 3 0 1 0 70 76 A 3 3 0 1 0 64 76");
        Ar("ii", "ي (ii)", g["i"].SvgPath, g["i"].InitialPath);
        Ar("u", "و/u", g["w"].SvgPath, null, g["w"].MedialPath);
        Ar("uu", "و (uu)", g["w"].SvgPath, null, g["w"].MedialPath);
        Ar("e", "e", "M 15 58 L 85 58 M 42 32 L 62 24");
        Ar("o", "o", "M 15 58 L 85 58 M 50 28 C 60 28, 60 40, 50 40 L 42 46");
        Ar("'", "ء/ع (')", "M 58 34 C 38 30, 38 52, 62 54 C 34 62, 34 86, 68 80");
        Ar("-", "-", "M 15 58 L 85 58", null, null, "M 25 58 L 85 58");

        return new WritingSystemConfig
        {
            Name = "Arabic RTL Cursive Abjad (العربية)",
            FlowDirection = FlowDirection.RTL,
            CursiveBonding = true,
            ContextualVariants = true,
            GlyphScale = 0.72,
            LetterSpacing = 2,
            LineSpacing = 90,
            WrapLimit = 680,
            Glyphs = g,
            FeaturicRules = new()
            {
                new() { Id = "arabic-final-tail", Name = "Final Calligraphic Sweep", TargetGlyphId = "*", ContextPosition = "final", AppendSvgPath = "M 24 58 Q 10 74 30 82", Enabled = true }
            }
        };
    }

    private static WritingSystemConfig CreateJapaneseWritingSystem()
    {
        var g = new Dictionary<string, GlyphDefinition>(StringComparer.OrdinalIgnoreCase);
        void Kana(string id, string label, string path) =>
            g[id] = MakeGlyph(id, label, path, 74, 80, new AnchorPoint(50, 10), new AnchorPoint(50, 90));

        const string dakuten = "M 74 18 L 82 28 M 84 14 L 92 24";
        const string handakuten = "M 78 20 A 5 5 0 1 0 88 20 A 5 5 0 1 0 78 20";

        Kana("a", "あ (a)", "M 24 30 L 74 28 M 50 16 L 46 78 M 68 42 C 68 78, 22 78, 26 56 C 30 40, 74 44, 72 74");
        Kana("i", "い (i)", "M 32 24 C 28 54, 34 72, 46 60 M 64 28 C 72 44, 74 62, 68 72");
        Kana("u", "う (u)", "M 42 18 L 58 26 M 28 42 C 74 30, 74 68, 40 82");
        Kana("e", "え (e)", "M 38 18 L 58 24 M 26 40 L 66 36 L 28 76 L 52 54 L 74 74");
        Kana("o", "お (o)", "M 24 32 L 62 30 M 44 18 L 44 76 C 32 76, 22 66, 36 56 C 54 44, 76 58, 64 78 M 70 22 L 80 30");
        Kana("k", "か (k)", "M 24 38 C 56 32, 60 66, 44 78 M 46 20 L 34 76 M 68 32 L 80 54");
        Kana("g", "が (g)", $"{g["k"].SvgPath} {dakuten}");
        Kana("s", "さ (s)", "M 24 34 L 74 28 M 44 18 L 58 56 M 30 62 C 38 78, 60 78, 70 72");
        Kana("z", "ざ (z)", $"{g["s"].SvgPath} {dakuten}");
        Kana("sh", "し (sh)", "M 38 20 L 38 66 C 38 82, 68 80, 78 60");
        Kana("j", "じ (j)", $"{g["sh"].SvgPath} {dakuten}");
        Kana("t", "た (t)", "M 22 32 L 58 28 M 42 18 L 30 78 M 52 46 L 74 44 M 50 68 C 58 76, 72 76, 78 72");
        Kana("d", "だ (d)", $"{g["t"].SvgPath} {dakuten}");
        Kana("ch", "ち (ch)", "M 24 32 L 72 28 M 46 18 L 38 52 C 74 42, 74 76, 40 80");
        Kana("ts", "つ (ts)", "M 22 38 C 78 24, 80 68, 36 78");
        Kana("n", "な (n)", "M 22 30 L 54 28 M 38 18 L 28 62 M 66 28 L 78 36 M 60 44 L 60 74 C 48 80, 34 74, 42 64 C 52 56, 72 68, 76 74");
        Kana("h", "は (h)", "M 28 22 C 24 48, 26 68, 32 76 M 46 34 L 76 32 M 62 20 L 62 72 C 50 78, 40 72, 46 62 C 56 54, 72 64, 78 72");
        Kana("b", "ば (b)", $"{g["h"].SvgPath} {dakuten}");
        Kana("p", "ぱ (p)", $"{g["h"].SvgPath} {handakuten}");
        Kana("f", "ふ (f)", "M 44 18 L 56 26 M 38 42 C 54 46, 58 68, 44 76 M 22 58 L 32 72 M 68 54 L 80 70");
        Kana("m", "ま (m)", "M 26 30 L 72 28 M 28 48 L 70 46 M 50 18 L 50 74 C 36 80, 26 70, 36 62 C 48 56, 72 68, 76 74");
        Kana("y", "や (y)", "M 24 46 C 72 24, 78 58, 52 62 M 56 20 L 48 34 M 34 26 L 48 80");
        Kana("r", "ら (r)", "M 40 18 L 62 26 M 34 36 L 32 58 C 72 44, 74 76, 38 80");
        Kana("w", "わ (w)", "M 36 18 L 36 80 M 22 36 L 48 32 L 22 58 C 74 30, 78 68, 48 78");
        Kana("ɴ", "ん (ɴ)", "M 48 20 L 24 76 M 38 56 C 52 44, 56 76, 76 68");
        Kana("aa", "あー", $"{g["a"].SvgPath} M 50 82 L 50 92");
        Kana("ii", "いー", $"{g["i"].SvgPath} M 50 82 L 50 92");
        Kana("uu", "うー", $"{g["u"].SvgPath} M 50 84 L 50 94");
        Kana("ee", "えー", $"{g["e"].SvgPath} M 50 82 L 50 92");
        Kana("oo", "おー", $"{g["o"].SvgPath} M 50 82 L 50 92");
        Kana("-", "ー", "M 50 20 L 50 80");

        return new WritingSystemConfig
        {
            Name = "Vertical Kana-Style Tategaki (日本語)",
            FlowDirection = FlowDirection.TTB_RTL,
            CursiveBonding = false,
            ContextualVariants = true,
            GlyphScale = 0.68,
            LetterSpacing = 8,
            LineSpacing = 86,
            WrapLimit = 380,
            Glyphs = g,
            FeaturicRules = new()
            {
                new() { Id = "japanese-brush-sweep", Name = "Vertical Brush Sweep", TargetGlyphId = "*", ContextPosition = "final", AppendSvgPath = "M 44 82 Q 62 92 78 76", Enabled = true }
            }
        };
    }

    private static WritingSystemConfig CreateTurkishWritingSystem()
    {
        var g = CreateLatinPrintGlyphs();
        g["ı"] = MakeGlyph("ı", "ı (dotless)", "M 50 34 L 50 72", 50);
        g["ç"] = MakeGlyph("ç", "ç", "M 68 38 C 30 26, 28 74, 68 64 M 50 70 L 46 82 C 56 86, 56 92, 44 90", 72, 86);
        g["ş"] = MakeGlyph("ş", "ş", "M 64 38 C 36 26, 26 48, 50 52 C 74 56, 62 76, 30 66 M 48 72 L 44 84 C 54 88, 54 94, 42 92", 68, 86);
        g["ğ"] = MakeGlyph("ğ", "ğ", "M 66 36 C 28 28, 28 70, 66 62 M 66 34 L 66 80 C 66 94, 32 94, 28 80 M 34 18 Q 50 28 66 18", 72, 86);
        g["ö"] = MakeGlyph("ö", "ö", g["o"].SvgPath + " M 36 18 A 3 3 0 1 0 42 18 A 3 3 0 1 0 36 18 M 58 18 A 3 3 0 1 0 64 18 A 3 3 0 1 0 58 18");
        g["ü"] = MakeGlyph("ü", "ü", g["u"].SvgPath + " M 36 18 A 3 3 0 1 0 42 18 A 3 3 0 1 0 36 18 M 58 18 A 3 3 0 1 0 64 18 A 3 3 0 1 0 58 18");
        return new WritingSystemConfig
        {
            Name = "Harmonic Extended Latin (Türkçe)",
            FlowDirection = FlowDirection.LTR,
            CursiveBonding = false,
            ContextualVariants = true,
            GlyphScale = 0.68,
            LetterSpacing = 4,
            LineSpacing = 86,
            WrapLimit = 680,
            Glyphs = g
        };
    }

    private static WritingSystemConfig CreateSwahiliWritingSystem()
    {
        var g = CreateLatinPrintGlyphs();
        g["ny"] = MakeGlyph("ny", "ny", "M 18 36 L 18 72 M 18 46 C 30 32, 46 34, 46 72 M 58 36 L 72 64 M 86 36 L 62 88", 84, 84);
        g["ng'"] = MakeGlyph("ng'", "ng'", "M 16 72 L 16 36 C 28 30, 42 36, 42 72 M 72 38 C 48 32, 48 68, 72 62 M 72 34 L 72 82 C 72 90, 48 90, 46 80 M 84 18 C 88 26, 84 34, 78 38", 86);
        g["dh"] = MakeGlyph("dh", "dh", "M 46 16 L 46 72 M 46 44 C 16 34, 16 74, 46 70 M 58 16 L 58 72 M 58 48 C 70 34, 86 36, 86 72", 84);
        g["mb"] = MakeGlyph("mb", "mb", "M 14 72 L 14 36 C 24 30, 34 36, 34 72 M 34 44 C 42 32, 52 36, 52 72 M 62 18 L 62 72 M 62 46 C 88 38, 88 72, 62 70", 86);
        g["nd"] = MakeGlyph("nd", "nd", "M 18 36 L 18 72 M 18 46 C 32 32, 46 34, 46 72 M 82 18 L 82 72 M 82 46 C 54 36, 54 72, 82 70", 84);
        return new WritingSystemConfig
        {
            Name = "Coastal Bantu Script (Kiswahili)",
            FlowDirection = FlowDirection.LTR,
            CursiveBonding = false,
            ContextualVariants = true,
            GlyphScale = 0.68,
            LetterSpacing = 4,
            LineSpacing = 86,
            WrapLimit = 680,
            Glyphs = g
        };
    }

    private static WritingSystemConfig CreateRussianWritingSystem()
    {
        var g = CreateLatinPrintGlyphs();
        g["a"] = MakeGlyph("a", "А (a)", "M 24 72 L 50 22 L 76 72 M 34 54 L 66 54");
        g["b"] = MakeGlyph("b", "Б (b)", "M 28 24 L 72 24 M 28 24 L 28 72 L 66 72 C 74 52, 66 44, 28 46");
        g["v"] = MakeGlyph("v", "В (v)", "M 28 22 L 28 72 M 28 22 C 70 22, 70 46, 28 46 C 74 46, 74 72, 28 72");
        g["g"] = MakeGlyph("g", "Г (g)", "M 32 72 L 32 24 L 72 24");
        g["d"] = MakeGlyph("d", "Д (d)", "M 26 64 L 38 24 L 64 24 L 72 64 M 18 64 L 82 64 M 22 64 L 22 78 M 78 64 L 78 78");
        g["e"] = MakeGlyph("e", "Е (e)", "M 68 24 L 30 24 L 30 72 L 68 72 M 30 48 L 60 48");
        g["zh"] = MakeGlyph("zh", "Ж (zh)", "M 50 22 L 50 74 M 22 24 L 78 72 M 78 24 L 22 72", 78);
        g["z"] = MakeGlyph("z", "З (z)", "M 28 28 C 72 18, 72 46, 40 48 C 74 50, 74 78, 28 68");
        g["i"] = MakeGlyph("i", "И (i)", "M 28 24 L 28 72 L 72 24 L 72 72");
        g["y"] = MakeGlyph("y", "Ы/Й (y)", "M 22 24 L 22 72 L 50 72 C 60 54, 54 46, 22 48 M 74 24 L 74 72", 78);
        g["k"] = MakeGlyph("k", "К (k)", "M 30 22 L 30 72 M 70 24 L 30 48 L 70 72");
        g["l"] = MakeGlyph("l", "Л (l)", "M 22 72 C 30 68, 36 42, 42 24 L 72 24 L 72 72");
        g["m"] = MakeGlyph("m", "М (m)", "M 24 72 L 24 24 L 50 56 L 76 24 L 76 72", 78);
        g["n"] = MakeGlyph("n", "Н (n)", "M 28 24 L 28 72 M 72 24 L 72 72 M 28 48 L 72 48");
        g["o"] = MakeGlyph("o", "О (o)", "M 50 24 C 76 24, 76 72, 50 72 C 24 72, 24 24, 50 24");
        g["p"] = MakeGlyph("p", "П (p)", "M 28 72 L 28 24 L 72 24 L 72 72");
        g["r"] = MakeGlyph("r", "Р (r)", "M 30 24 L 30 74 M 30 24 C 72 24, 72 52, 30 52");
        g["s"] = MakeGlyph("s", "С (s)", "M 70 30 C 26 18, 26 78, 70 66");
        g["t"] = MakeGlyph("t", "Т (t)", "M 22 24 L 78 24 M 50 24 L 50 72");
        g["u"] = MakeGlyph("u", "У (u)", "M 26 24 L 50 54 M 72 24 L 40 78");
        g["f"] = MakeGlyph("f", "Ф (f)", "M 50 16 L 50 80 M 50 28 C 82 28, 82 64, 50 64 C 18 64, 18 28, 50 28", 78);
        g["kh"] = MakeGlyph("kh", "Х (kh)", "M 26 24 L 74 72 M 74 24 L 26 72");
        g["ts"] = MakeGlyph("ts", "Ц (ts)", "M 26 24 L 26 68 L 70 68 L 70 24 M 70 68 L 76 82", 76);
        g["ch"] = MakeGlyph("ch", "Ч (ch)", "M 28 24 L 28 48 C 28 58, 68 58, 68 44 M 68 24 L 68 72");
        g["sh"] = MakeGlyph("sh", "Ш (sh)", "M 22 24 L 22 72 L 78 72 L 78 24 M 50 24 L 50 72", 78);
        g["'"] = MakeGlyph("'", "Ь (pal)", "M 34 22 L 34 72 L 64 72 C 74 52, 64 46, 34 48", 64);

        return new WritingSystemConfig
        {
            Name = "Cyrillic-Geometric Script (Русский)",
            FlowDirection = FlowDirection.LTR,
            CursiveBonding = false,
            ContextualVariants = true,
            GlyphScale = 0.68,
            LetterSpacing = 4,
            LineSpacing = 86,
            WrapLimit = 680,
            Glyphs = g
        };
    }

    private static WritingSystemConfig CreateIndonesianWritingSystem()
    {
        var g = CreateLatinPrintGlyphs();
        g["ny"] = MakeGlyph("ny", "ny", "M 18 36 L 18 72 M 18 46 C 30 32, 46 34, 46 72 M 58 36 L 72 64 M 86 36 L 62 88", 84, 84);
        g["sy"] = MakeGlyph("sy", "sy", "M 42 38 C 20 30, 18 48, 34 52 C 50 56, 42 74, 20 66 M 56 36 L 70 64 M 86 36 L 60 88", 84, 84);
        g["kh"] = MakeGlyph("kh", "kh", "M 22 18 L 22 72 M 48 34 L 22 52 L 48 72 M 60 18 L 60 72 M 60 48 C 72 34, 86 36, 86 72", 84);
        g["ə"] = MakeGlyph("ə", "ə", "M 68 48 L 30 48 C 28 70, 68 72, 68 48 C 68 28, 36 28, 32 36");
        return new WritingSystemConfig
        {
            Name = "Nusantara Latin Script (Bahasa Indonesia)",
            FlowDirection = FlowDirection.LTR,
            CursiveBonding = false,
            ContextualVariants = true,
            GlyphScale = 0.68,
            LetterSpacing = 4,
            LineSpacing = 86,
            WrapLimit = 680,
            Glyphs = g
        };
    }

    public static WritingSystemConfig CreateForLanguage(string? langId) => (langId?.ToLowerInvariant()) switch
    {
        "english" => new WritingSystemConfig
        {
            Name = "Latin Print & Digraphs (English)",
            FlowDirection = FlowDirection.LTR,
            CursiveBonding = false,
            ContextualVariants = true,
            GlyphScale = 0.66,
            LetterSpacing = 4,
            LineSpacing = 84,
            WrapLimit = 680,
            Glyphs = CreateLatinPrintGlyphs(),
            FeaturicRules = new()
            {
                new() { Id = "english-serif-tick", Name = "Word-Final Serif Tick", TargetGlyphId = "*", ContextPosition = "final", AppendSvgPath = "M 70 72 L 80 68", Enabled = false }
            }
        },
        "spanish" => CreateSpanishWritingSystem(),
        "german" => CreateGermanWritingSystem(),
        "mandarin" => CreateMandarinWritingSystem(),
        "arabic" => CreateArabicWritingSystem(),
        "japanese" => CreateJapaneseWritingSystem(),
        "turkish" => CreateTurkishWritingSystem(),
        "swahili" => CreateSwahiliWritingSystem(),
        "russian" => CreateRussianWritingSystem(),
        "indonesian" => CreateIndonesianWritingSystem(),
        _ => CreateDefaultConfig()
    };

    public static string GetSampleInput(string? langId) => (langId?.ToLowerInvariant()) switch
    {
        "english" => "the quick fox saw a mouse",
        "spanish" => "el perro pequeño vio la casa",
        "german" => "der große hund sah das buch",
        "mandarin" => "rén kàn xiǎo māo hé shū",
        "arabic" => "kataba al-rajul kitaab fii bayt",
        "japanese" => "watashi wa inu to neko o miru",
        "turkish" => "küçük köpek güzel ev gördü",
        "swahili" => "mtu mdogo aliona nyumba kubwa",
        "russian" => "bolshoy chelovek videt dom",
        "indonesian" => "anak kecil melihat rumah besar",
        _ => "shika taka person"
    };

    public static WritingSystemConfig CreatePreset(string presetKey)
    {
        var baseCfg = CreateDefaultConfig();
        if (presetKey == "vertical-mongolian")
        {
            var vGlyphs = new Dictionary<string, GlyphDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, g) in baseCfg.Glyphs)
            {
                bool isVowel = k is "a" or "e" or "i" or "o" or "u";
                vGlyphs[k] = new GlyphDefinition
                {
                    Id = g.Id,
                    Label = g.Label,
                    SvgPath = isVowel
                        ? "M 50 10 L 50 38 C 72 45, 72 65, 50 70 L 50 90"
                        : "M 50 10 L 50 32 L 74 48 L 50 55 L 50 90",
                    InitialPath = "M 50 12 C 30 12, 30 32, 50 35 L 50 90",
                    FinalPath = "M 50 10 L 50 65 C 50 85, 80 85, 88 70",
                    EnteringAnchor = new(50, 10),
                    ExitingAnchor = new(50, 90),
                    AdvanceWidth = 72,
                    AdvanceHeight = 80
                };
            }
            baseCfg.Name = "Traditional Vertical Spine";
            baseCfg.FlowDirection = FlowDirection.TTB_LTR;
            baseCfg.LetterSpacing = 0;
            baseCfg.LineSpacing = 84;
            baseCfg.WrapLimit = 360;
            baseCfg.Glyphs = vGlyphs;
            baseCfg.FeaturicRules = new()
            {
                new() { Id = "vertical-tail", Name = "Final Spine Sweep", TargetGlyphId = "*", ContextPosition = "final", AppendSvgPath = "M 50 86 Q 72 95 85 80", Enabled = true }
            };
        }
        else if (presetKey == "boustrophedon")
        {
            baseCfg.Name = "Boustrophedon Alternating Matrix";
            baseCfg.FlowDirection = FlowDirection.Boustrophedon;
            baseCfg.CursiveBonding = false;
            baseCfg.WrapLimit = 420;
        }
        return baseCfg;
    }

    public static IReadOnlyList<GraphemeToken> TokenizeRawInput(string input, IEnumerable<string> knownIds)
    {
        var sorted = knownIds.Where(k => k.Length > 1).OrderByDescending(k => k.Length).ToList();
        var words = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var tokens = new List<GraphemeToken>();

        for (int w = 0; w < words.Length; w++)
        {
            var lower = words[w].ToLowerInvariant();
            var pieces = new List<string>();
            for (int i = 0; i < lower.Length;)
            {
                var multi = sorted.FirstOrDefault(gid => lower.AsSpan(i).StartsWith(gid.AsSpan(), StringComparison.OrdinalIgnoreCase));
                if (multi is not null)
                {
                    pieces.Add(multi);
                    i += multi.Length;
                }
                else
                {
                    pieces.Add(lower[i].ToString());
                    i++;
                }
            }

            for (int p = 0; p < pieces.Count; p++)
            {
                string pos = pieces.Count == 1 ? "isolated" : p == 0 ? "initial" : p == pieces.Count - 1 ? "final" : "medial";
                tokens.Add(new GraphemeToken(pieces[p], pieces[p], w, pos));
            }
        }
        return tokens;
    }

    private static GlyphDefinition SynthesizeFallback(string id, FlowDirection flow)
    {
        int hash = id.Aggregate(0, (acc, ch) => acc * 31 + ch);
        int h = Math.Abs(hash);
        int loopY = 25 + (h % 30);
        int midX = 35 + ((h >> 3) % 30);
        if (flow is FlowDirection.TTB_LTR or FlowDirection.TTB_RTL)
        {
            return new GlyphDefinition
            {
                Id = id,
                Label = id,
                SvgPath = $"M 50 10 L 50 32 Q {midX} {loopY} 50 65 L 50 90",
                EnteringAnchor = new(50, 10),
                ExitingAnchor = new(50, 90),
                AdvanceWidth = 72,
                AdvanceHeight = 80
            };
        }
        return new GlyphDefinition
        {
            Id = id,
            Label = id,
            SvgPath = $"M 15 55 Q {midX} {loopY} 65 45 T 85 55",
            EnteringAnchor = new(15, 55),
            ExitingAnchor = new(85, 55),
            AdvanceWidth = 74,
            AdvanceHeight = 75
        };
    }

    private static bool MatchesFeaturicRule(FeaturicAssemblyRule rule, IReadOnlyList<GraphemeToken> tokens, int idx)
    {
        if (!rule.Enabled) return false;
        var tok = tokens[idx];
        if (rule.TargetGlyphId != "*" && !string.Equals(rule.TargetGlyphId, tok.Id, StringComparison.OrdinalIgnoreCase))
            return false;

        return rule.ContextPosition switch
        {
            "any" => true,
            "initial" => tok.PositionInWord is "initial" or "isolated",
            "medial" => tok.PositionInWord == "medial",
            "final" => tok.PositionInWord is "final" or "isolated",
            "after" => idx > 0 && tokens[idx - 1].WordIndex == tok.WordIndex &&
                       (string.IsNullOrEmpty(rule.ContextGlyphId) || string.Equals(tokens[idx - 1].Id, rule.ContextGlyphId, StringComparison.OrdinalIgnoreCase)),
            "before" => idx < tokens.Count - 1 && tokens[idx + 1].WordIndex == tok.WordIndex &&
                        (string.IsNullOrEmpty(rule.ContextGlyphId) || string.Equals(tokens[idx + 1].Id, rule.ContextGlyphId, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private sealed record Draft(int Idx, GraphemeToken Token, GlyphDefinition Def, string Variant, string SvgPath, List<string> Extras, List<string> Features, int Line, bool NewWord);

    public static ShapingResult RunShapingGraph(IReadOnlyList<GraphemeToken> tokens, WritingSystemConfig config)
    {
        double scale = config.GlyphScale > 0 ? config.GlyphScale : 0.72;
        const double padding = 48;
        bool isVertical = config.FlowDirection is FlowDirection.TTB_LTR or FlowDirection.TTB_RTL;
        double wordGap = 28 * scale;
        double wrap = Math.Max(200, config.WrapLimit);

        var drafts = new List<Draft>(tokens.Count);
        int line = 0;
        double span = 0;
        int prevWord = tokens.Count > 0 ? tokens[0].WordIndex : 0;

        for (int i = 0; i < tokens.Count; i++)
        {
            var tok = tokens[i];
            if (!config.Glyphs.TryGetValue(tok.Id, out var def))
                def = SynthesizeFallback(tok.Id, config.FlowDirection);

            bool newWord = i > 0 && tok.WordIndex != prevWord;
            double stepSpan = (isVertical ? def.AdvanceHeight : def.AdvanceWidth) * scale + config.LetterSpacing + (newWord ? wordGap : 0);
            if (i > 0 && span + stepSpan > wrap && newWord)
            {
                line++;
                span = 0;
            }

            string variant = config.ContextualVariants ? tok.PositionInWord : "isolated";
            string chosenPath = def.SvgPath;
            if (config.ContextualVariants)
            {
                if (variant == "initial" && !string.IsNullOrEmpty(def.InitialPath)) chosenPath = def.InitialPath;
                else if (variant == "medial" && !string.IsNullOrEmpty(def.MedialPath)) chosenPath = def.MedialPath;
                else if (variant == "final" && !string.IsNullOrEmpty(def.FinalPath)) chosenPath = def.FinalPath;
            }

            var extras = new List<string>();
            var features = new List<string>();
            foreach (var r in config.FeaturicRules)
            {
                if (MatchesFeaturicRule(r, tokens, i) && !string.IsNullOrWhiteSpace(r.AppendSvgPath))
                {
                    extras.Add(r.AppendSvgPath.Trim());
                    features.Add(r.Name);
                }
            }

            drafts.Add(new Draft(i, tok, def, variant, chosenPath, extras, features, line, newWord));
            span += stepSpan;
            prevWord = tok.WordIndex;
        }

        int totalLines = line + 1;
        double maxX = padding * 2 + 240;
        double maxY = padding * 2 + 180;
        var glyphs = new List<ShapedGlyph>(drafts.Count);
        var trace = new List<ShapingTraceEntry>(drafts.Count);

        for (int l = 0; l < totalLines; l++)
        {
            var lineDrafts = drafts.Where(d => d.Line == l).ToList();
            string lineFlow = isVertical ? "TTB"
                : config.FlowDirection == FlowDirection.RTL || (config.FlowDirection == FlowDirection.Boustrophedon && l % 2 == 1)
                ? "RTL" : "LTR";

            AnchorPoint? prevExit = null;
            double primary = lineFlow == "RTL" ? wrap + padding : padding;
            double secondary = config.FlowDirection == FlowDirection.TTB_RTL
                ? padding + (totalLines - 1 - l) * config.LineSpacing
                : padding + l * config.LineSpacing;

            for (int k = 0; k < lineDrafts.Count; k++)
            {
                var d = lineDrafts[k];
                var def = d.Def;
                bool newWordOnLine = k > 0 && d.NewWord;
                double gx, gy;
                string mode = "GridAdvance";

                if (lineFlow == "LTR")
                {
                    if (config.CursiveBonding && prevExit is not null && !newWordOnLine)
                    {
                        gx = prevExit.X - def.EnteringAnchor.X * scale + config.LetterSpacing * 0.25;
                        gy = prevExit.Y - def.EnteringAnchor.Y * scale;
                        mode = "Bond(#exiting -> #entering)";
                    }
                    else
                    {
                        if (newWordOnLine) primary += wordGap;
                        gx = primary;
                        gy = secondary;
                    }
                    primary = gx + def.AdvanceWidth * scale + config.LetterSpacing;
                }
                else if (lineFlow == "RTL")
                {
                    if (config.CursiveBonding && prevExit is not null && !newWordOnLine)
                    {
                        gx = prevExit.X - def.ExitingAnchor.X * scale - config.LetterSpacing * 0.25;
                        gy = prevExit.Y - def.ExitingAnchor.Y * scale;
                        mode = "RTL-Bond(#entering <- #exiting)";
                    }
                    else
                    {
                        if (newWordOnLine) primary -= wordGap;
                        gx = primary - def.AdvanceWidth * scale;
                        gy = secondary;
                    }
                    primary = gx - config.LetterSpacing;
                }
                else
                {
                    if (config.CursiveBonding && prevExit is not null && !newWordOnLine)
                    {
                        gx = prevExit.X - def.EnteringAnchor.X * scale;
                        gy = prevExit.Y - def.EnteringAnchor.Y * scale + config.LetterSpacing * 0.25;
                        mode = "VerticalBond(#exiting -> #entering)";
                    }
                    else
                    {
                        if (newWordOnLine) primary += wordGap;
                        gx = secondary;
                        gy = primary;
                    }
                    primary = gy + def.AdvanceHeight * scale + config.LetterSpacing;
                }

                var enterWorld = new AnchorPoint(gx + def.EnteringAnchor.X * scale, gy + def.EnteringAnchor.Y * scale);
                var exitWorld = new AnchorPoint(gx + def.ExitingAnchor.X * scale, gy + def.ExitingAnchor.Y * scale);
                prevExit = lineFlow == "RTL" ? enterWorld : exitWorld;

                maxX = Math.Max(maxX, gx + 100 * scale + padding);
                maxY = Math.Max(maxY, gy + 100 * scale + padding);

                glyphs.Add(new ShapedGlyph(d.Idx, d.Token, gx, gy, d.SvgPath, d.Extras, enterWorld, exitWorld, d.Variant, d.Features));
                trace.Add(new ShapingTraceEntry(d.Idx + 1, d.Token.Id, d.Variant, mode, $"({Math.Round(gx)}, {Math.Round(gy)})"));
            }
        }

        return new ShapingResult(glyphs, trace, Math.Ceiling(maxX), Math.Ceiling(maxY));
    }

    public static string ExportShapedSvg(ShapingResult result, WritingSystemConfig config)
    {
        double scale = config.GlyphScale > 0 ? config.GlyphScale : 0.72;
        var sb = new StringBuilder();
        sb.AppendLine($@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 {result.Width} {result.Height}"" width=""{result.Width}"" height=""{result.Height}"">");
        sb.AppendLine(@"  <rect width=""100%"" height=""100%"" fill=""#ffffff"" />");
        foreach (var g in result.Glyphs)
        {
            sb.AppendLine($@"  <g transform=""translate({g.X:F1}, {g.Y:F1}) scale({scale})"">");
            sb.AppendLine($@"    <path d=""{g.SvgPath}"" fill=""none"" stroke=""#0f172a"" stroke-width=""4.5"" stroke-linecap=""round"" stroke-linejoin=""round"" />");
            foreach (var ep in g.ExtraPaths)
                sb.AppendLine($@"    <path d=""{ep}"" fill=""none"" stroke=""#1e293b"" stroke-width=""4"" stroke-linecap=""round"" stroke-linejoin=""round"" />");
            sb.AppendLine("  </g>");
        }
        sb.AppendLine("</svg>");
        return sb.ToString();
    }
}
