using Conlanger.Core;
using Conlanger.SDK;
using Xunit;

namespace Conlanger.Tests;

public sealed class LanguageEngineTests
{
    [Fact]
    public void LangEngine_PreservesWordBoundariesAcrossPipeline()
    {
        var project = ProjectFactory.CreateStarterProject();
        var engine = new LangEngine();
        var result = engine.GenerateSentence(project, seed: 42, approxWords: 4);

        Assert.True(result.SurfaceWords.Count >= 3);
        Assert.Contains(" ", result.Written);
        Assert.Equal(result.SurfaceWords.Count - 1, result.Tokens.Max(t => t.WordIndex));
    }

    [Fact]
    public void PhonologyEngine_SupportsIntervocalicEpenthesisDeletionAndFeatures()
    {
        var project = ProjectFactory.CreateStarterProject();
        project.PhonologyRules =
        [
            new() { Name = "Intervocalic Voicing", Target = "p", Replacement = "b", PrevPhone = "V", NextPhone = "V", Condition = "[-voice]", Enabled = true },
            new() { Name = "Final Deletion", Target = "t", Replacement = "∅", PrevPhone = "V", NextPhone = "#", Enabled = true },
            new() { Name = "Epenthetic t", Target = "∅", Replacement = "t", PrevPhone = "a", NextPhone = "a", Enabled = true }
        ];

        var res = PhonologyEngine.Derive(["a", "p", "a", "a", "t"], project);
        Assert.Equal(new[] { "a", "b", "a", "t", "a" }, res.SurfacePhones);
    }

    [Fact]
    public void Syllabifier_RespectsMaximalOnsetAndPhonotactics()
    {
        var project = ProjectFactory.CreateStarterProject();
        var syllables = PhonologyEngine.Syllabify(["p", "a", "s", "t", "a"], project);

        Assert.Equal(2, syllables.Count);
        Assert.Equal(new[] { "p" }, syllables[0].Onset);
        Assert.Equal(new[] { "s", "t" }, syllables[1].Onset);
    }

    [Fact]
    public void WordGenerator_ProducesUniquePhonotacticallyLegalWords()
    {
        var project = ProjectFactory.CreateStarterProject();
        var words = WordGenerator.GenerateWords(project, count: 12, syllablesPerWord: 2, seed: 7);

        Assert.Equal(12, words.Count);
        Assert.Equal(words.Count, words.Distinct().Count());
    }

    [Fact]
    public void Validator_ReportsErrorsWhenInventoryEmpty()
    {
        var project = ProjectFactory.CreateStarterProject();
        project.Phonemes.RemoveAll(p => p.Type == "V");
        var issues = ProjectValidator.Validate(project);

        Assert.Contains(issues, i => i.Severity == DiagnosticSeverity.Error && i.Subsystem == "Phonology");
    }
}
