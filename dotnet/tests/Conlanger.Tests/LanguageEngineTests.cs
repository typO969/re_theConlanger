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
    public void PhonologyEngine_MatchesFeatureBracketsAndNaturalClassesInTargetsAndContexts()
    {
        var project = ProjectFactory.CreateStarterProject();
        project.Phonemes.Add(ProjectFactory.CreatePhoneme("b", "C"));
        project.PhonologyRules =
        [
            new() { Name = "Post-stop sibilant voicing", Target = "[+sibilant,-voice]", Replacement = "z", PrevPhone = "[manner=stop]", Enabled = true }
        ];

        var res = PhonologyEngine.Derive(["b", "s", "a"], project);
        Assert.Equal(new[] { "b", "z", "a" }, res.SurfacePhones);
    }

    [Fact]
    public void PhonologyEngine_AssignsStressAndAppliesUnstressedVowelReduction()
    {
        var project = ProjectFactory.CreateStarterProject();
        project.StressPattern = StressPattern.Initial;
        project.Pipeline.EnabledPhonoPluginRules = ["unstressed-vowel-reduction"];

        var engine = new LangEngine();
        var rules = engine.ResolvePluginRules(project);
        var res = PhonologyEngine.Derive(["t", "a", "k", "a"], project, rules);

        Assert.Equal(0, res.StressIndex);
        Assert.Equal(new[] { "t", "a", "k", "ə" }, res.SurfacePhones);
    }

    [Fact]
    public void Syllabifier_RespectsMultiConsonantMaximalOnsetAndPhonotactics()
    {
        var project = ProjectFactory.CreateStarterProject();
        project.Phonotactics.AllowedOnsets = ["st", "tr", "str", "k", "p", "t", "s", "r"];
        project.Phonotactics.MaxOnsetCluster = 3;
        project.Phonotactics.MaxCodaCluster = 2;

        var syllables = PhonologyEngine.Syllabify(["ɛ", "k", "s", "t", "r", "ə"], project);

        Assert.Equal(2, syllables.Count);
        Assert.Equal(new[] { "k" }, syllables[0].Coda);
        Assert.Equal(new[] { "s", "t", "r" }, syllables[1].Onset);
    }

    [Fact]
    public void PipelineRegistry_SwapsSyntaxMorphologyOrthographyAndPluginPhonoRules()
    {
        var project = ProjectFactory.CreateStarterProject();
        project.Pipeline.SyntaxGeneratorId = "rich-np-syntax";
        project.Pipeline.MorphologyRealizerId = "reduplicative-plural";
        project.Pipeline.OrthographyRendererId = "ipa-broad";
        project.Pipeline.EnabledPhonoPluginRules = ["intervocalic-stop-lenition"];

        var engine = new LangEngine();
        var morph = engine.ResolveMorphology(project);
        var pluralPerson = morph.RealizeWords(new SyntaxTree(new SyntaxNode("N", "person", [new Feat("Number", "Pl")])), project).Single();
        Assert.Equal(new[] { "p", "e", "p", "e", "r", "s", "o", "n", "i" }, pluralPerson);

        var sentence = engine.GenerateSentence(project, seed: 42, approxWords: 2, new Dictionary<string, string> { ["Number"] = "Pl" });
        Assert.StartsWith("/", sentence.Written);
        Assert.EndsWith("/", sentence.Written);
        Assert.True(sentence.Tree.Leaves().Count >= 5);
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

    [Fact]
    public void EnglishPreset_ValidatesCleanlyAndHandlesPhonologyAllomorphyIrregulars()
    {
        var eng = ProjectFactory.CreateEnglishPresetProject();
        var diagnostics = ProjectValidator.Validate(eng);
        Assert.Empty(diagnostics);

        var morphology = new MorphologyEngine();
        Assert.Equal(new[] { "ˈk", "æ", "t", "s" }, morphology.InflectSingle("cat", "N", [new Feat("Number", "Pl")], eng));
        Assert.Equal(new[] { "ˈf", "ɑː", "k", "s", "ɪ", "z" }, morphology.InflectSingle("fox", "N", [new Feat("Number", "Pl")], eng));
        Assert.Equal(new[] { "ˈd", "ɔː", "g", "z" }, morphology.InflectSingle("dog", "N", [new Feat("Number", "Pl")], eng));
        Assert.Equal(new[] { "ˈm", "aɪ", "s" }, morphology.InflectSingle("mouse", "N", [new Feat("Number", "Pl")], eng));
        Assert.Equal(new[] { "ˈw", "ɛ", "n", "t" }, morphology.InflectSingle("go", "V", [new Feat("Tense", "Past")], eng));
        Assert.Equal(new[] { "ˈg", "oʊ", "z" }, morphology.InflectSingle("go", "V", [new Feat("Tense", "Pres"), new Feat("Person", "3"), new Feat("Number", "Sing"), new Feat("Agreement", "3sg")], eng));

        var water = PhonologyEngine.Derive(["ˈw", "ɔː", "t", "ɚ"], eng);
        Assert.Equal(new[] { "ˈw", "ɔː", "ɾ", "ɚ" }, water.SurfacePhones);

        var catAspirated = PhonologyEngine.Derive(["ˈk", "æ", "t"], eng);
        Assert.Equal(new[] { "ˈkʰ", "æ", "t" }, catAspirated.SurfacePhones);

        var tallDarkL = PhonologyEngine.Derive(["ˈt", "ɔː", "l"], eng);
        Assert.Equal(new[] { "ˈtʰ", "ɔː", "ɫ" }, tallDarkL.SurfacePhones);

        var inputAssim = PhonologyEngine.Derive(["ɪ", "n", "p", "ʊ", "t"], eng);
        Assert.Equal(new[] { "ɪ", "m", "p", "ʊ", "t" }, inputAssim.SurfacePhones);
    }

    [Fact]
    public void EnglishPreset_GeneratesCleanMorphophonemicSentences()
    {
        var eng = ProjectFactory.CreateEnglishPresetProject();
        var engine = new LangEngine();

        for (int seed = 1; seed <= 10; seed++)
        {
            var res = engine.GenerateSentence(eng, seed: seed, approxWords: 6);
            Assert.False(string.IsNullOrWhiteSpace(res.Written));
            Assert.Empty(res.Issues);
        }
    }
}
