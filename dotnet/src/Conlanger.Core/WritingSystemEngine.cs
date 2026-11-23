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
    public static WritingSystemConfig CreateDefaultConfig()
    {
        var glyphs = new Dictionary<string, GlyphDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = new() { Id = "a", Label = "a", SvgPath = "M 15 55 C 15 30, 55 25, 65 45 L 65 70 C 65 75, 80 75, 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 75, AdvanceHeight = 75 },
            ["e"] = new() { Id = "e", Label = "e", SvgPath = "M 15 55 L 70 50 C 75 30, 35 25, 30 50 C 25 75, 70 75, 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 75, AdvanceHeight = 75 },
            ["i"] = new() { Id = "i", Label = "i", SvgPath = "M 15 55 C 35 55, 45 30, 50 25 C 50 45, 55 70, 85 55 M 48 12 A 4 4 0 1 0 56 12 A 4 4 0 1 0 48 12", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 65, AdvanceHeight = 75 },
            ["o"] = new() { Id = "o", Label = "o", SvgPath = "M 15 55 C 25 35, 65 30, 65 52 C 65 72, 30 72, 30 52 C 30 42, 60 55, 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 75, AdvanceHeight = 75 },
            ["u"] = new() { Id = "u", Label = "u", SvgPath = "M 15 55 L 32 30 L 35 65 C 40 72, 62 72, 65 30 L 68 65 L 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 75, AdvanceHeight = 75 },
            ["p"] = new() { Id = "p", Label = "p", SvgPath = "M 15 55 L 35 28 L 35 88 M 35 38 C 68 25, 72 60, 35 58 L 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 75, AdvanceHeight = 80 },
            ["t"] = new() { Id = "t", Label = "t", SvgPath = "M 15 55 L 45 15 L 48 68 C 52 74, 72 70, 85 55 M 28 32 L 66 32", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 70, AdvanceHeight = 75 },
            ["k"] = new() { Id = "k", Label = "k", SvgPath = "M 15 55 L 35 15 L 35 72 M 66 30 L 35 52 L 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 75, AdvanceHeight = 75 },
            ["c"] = new() { Id = "c", Label = "c", SvgPath = "M 15 55 C 35 30, 70 28, 72 38 M 36 45 C 25 68, 65 72, 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 70, AdvanceHeight = 75 },
            ["s"] = new() { Id = "s", Label = "s", SvgPath = "M 15 55 L 42 30 C 65 38, 25 62, 55 70 L 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 70, AdvanceHeight = 75 },
            ["sh"] = new() { Id = "sh", Label = "sh", SvgPath = "M 15 55 C 30 22, 48 22, 52 55 C 56 80, 72 80, 85 55 M 32 18 L 68 18", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 78, AdvanceHeight = 75 },
            ["z"] = new() { Id = "z", Label = "z", SvgPath = "M 15 55 L 32 32 L 66 32 L 32 68 L 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 75, AdvanceHeight = 75 },
            ["n"] = new() { Id = "n", Label = "n", SvgPath = "M 15 55 L 32 30 L 34 68 M 34 45 C 48 25, 65 28, 66 65 L 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 75, AdvanceHeight = 75 },
            ["r"] = new() { Id = "r", Label = "r", SvgPath = "M 15 55 L 35 30 L 35 68 M 35 44 C 48 28, 65 28, 70 40 L 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 70, AdvanceHeight = 75 },
            ["m"] = new() { Id = "m", Label = "m", SvgPath = "M 15 55 L 25 32 L 35 65 L 50 32 L 62 65 L 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 78, AdvanceHeight = 75 },
            ["l"] = new() { Id = "l", Label = "l", SvgPath = "M 15 55 C 35 45, 55 12, 48 12 C 40 12, 42 70, 85 55", EnteringAnchor = new(15, 55), ExitingAnchor = new(85, 55), AdvanceWidth = 68, AdvanceHeight = 75 }
        };

        return new WritingSystemConfig
        {
            Name = "Agnostic Cursive Matrix",
            FlowDirection = FlowDirection.RTL,
            CursiveBonding = true,
            ContextualVariants = true,
            GlyphScale = 0.72,
            LetterSpacing = 6,
            LineSpacing = 88,
            WrapLimit = 680,
            Glyphs = glyphs,
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
    }

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
