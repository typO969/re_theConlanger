using Conlanger.Core;
using Conlanger.SDK;
using Xunit;

namespace Conlanger.Tests;

public sealed class WritingSystemTests
{
    [Fact]
    public void ShapingGraph_RtlDecreasesXAcrossWord()
    {
        var cfg = WritingSystemEngine.CreatePreset("rtl-western");
        var tokens = WritingSystemEngine.TokenizeRawInput("pata", cfg.Glyphs.Keys);
        var shaped = WritingSystemEngine.RunShapingGraph(tokens, cfg);

        Assert.Equal(4, shaped.Glyphs.Count);
        Assert.True(shaped.Glyphs[1].X < shaped.Glyphs[0].X);
    }

    [Fact]
    public void ShapingGraph_VerticalMongolianAdvancesDownwardSpine()
    {
        var cfg = WritingSystemEngine.CreatePreset("vertical-mongolian");
        var tokens = WritingSystemEngine.TokenizeRawInput("kana", cfg.Glyphs.Keys);
        var shaped = WritingSystemEngine.RunShapingGraph(tokens, cfg);

        Assert.Equal(4, shaped.Glyphs.Count);
        Assert.True(shaped.Glyphs[1].Y > shaped.Glyphs[0].Y);
        Assert.Contains("VerticalBond", shaped.Trace[1].AnchorMode);
    }

    [Fact]
    public void ExportSvg_ContainsAllShapedGlyphPaths()
    {
        var cfg = WritingSystemEngine.CreateDefaultConfig();
        var tokens = WritingSystemEngine.TokenizeRawInput("sar", cfg.Glyphs.Keys);
        var shaped = WritingSystemEngine.RunShapingGraph(tokens, cfg);
        var svg = WritingSystemEngine.ExportShapedSvg(shaped, cfg);

        Assert.StartsWith("<svg", svg);
        Assert.Contains("</svg>", svg);
    }
}
