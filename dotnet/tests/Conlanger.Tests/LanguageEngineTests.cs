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

    [Fact]
    public void NonLinearMorphology_SupportsSemiticTemplatesTagalogInfixGermanCircumfix()
    {
        var project = ProjectFactory.CreateStarterProject();
        project.Phonemes.Add(ProjectFactory.CreatePhoneme("x", "C"));
        project.Phonemes.Add(ProjectFactory.CreatePhoneme("b", "C"));
        project.Phonemes.Add(ProjectFactory.CreatePhoneme("l", "C"));
        project.Phonemes.Add(ProjectFactory.CreatePhoneme("m", "C"));
        project.Phonemes.Add(ProjectFactory.CreatePhoneme("g", "C"));
        project.Phonemes.Add(ProjectFactory.CreatePhoneme("r", "C"));

        project.Lexemes =
        [
            new() { Lemma = "write", Pos = "V", UR = "k t b" },
            new() { Lemma = "s_write", Pos = "V", UR = "s u l a t" },
            new() { Lemma = "v_study", Pos = "V", UR = "a r a l" },
            new() { Lemma = "make", Pos = "V", UR = "m a x" }
        ];
        project.Affixes =
        [
            new() { Name = "Form I Perf", Pos = "V", FeatureName = "Voice", FeatureValue = "FormI", Affix = "C1 a C2 a C3 a", AffixType = AffixType.Template, Enabled = true },
            new() { Name = "Form II", Pos = "V", FeatureName = "Voice", FeatureValue = "FormII", Affix = "C1 a C2 C2 a C3 a", AffixType = AffixType.Template, Enabled = true },
            new() { Name = "Passive Part", Pos = "N", FeatureName = "Form", FeatureValue = "Part", Affix = "m a C1 C2 uː C3", AffixType = AffixType.Template, Enabled = true },
            new() { Name = "Actor Focus", Pos = "V", FeatureName = "Focus", FeatureValue = "Actor", Affix = "um", AffixType = AffixType.Infix, Enabled = true },
            new() { Name = "Past Part", Pos = "V", FeatureName = "Aspect", FeatureValue = "Perf", Affix = "ge+...+t", AffixType = AffixType.Circumfix, Enabled = true }
        ];

        var morph = new MorphologyEngine();
        Assert.Equal("kataba", string.Concat(morph.InflectSingle("write", "V", [new Feat("Voice", "FormI")], project)));
        Assert.Equal("kattaba", string.Concat(morph.InflectSingle("write", "V", [new Feat("Voice", "FormII")], project)));
        project.Lexemes.Add(new() { Lemma = "written", Pos = "N", UR = "k t b" });
        Assert.Equal("maktuːb", string.Concat(morph.InflectSingle("written", "N", [new Feat("Form", "Part")], project)));

        Assert.Equal("sumulat", string.Concat(morph.InflectSingle("s_write", "V", [new Feat("Focus", "Actor")], project)));
        Assert.Equal("umaral", string.Concat(morph.InflectSingle("v_study", "V", [new Feat("Focus", "Actor")], project)));
        Assert.Equal("gemaxt", string.Concat(morph.InflectSingle("make", "V", [new Feat("Aspect", "Perf")], project)));
    }

    [Fact]
    public void PhonologyEngine_SupportsTurkishVowelHarmonyAndMandarinToneSandhi()
    {
        var turkish = ProjectFactory.CreateStarterProject();
        turkish.Phonemes =
        [
            .. new[] { "v", "l", "r", "t", "k", "j", "z" }.Select(c => ProjectFactory.CreatePhoneme(c, "C")),
            .. new[] { "a", "e", "i", "o", "u", "y", "ø", "ɯ" }.Select(v => ProjectFactory.CreatePhoneme(v, "V"))
        ];
        turkish.PhonologyRules =
        [
            new() { Name = "2-way Back Harmony", Target = "e", Replacement = "[αback]", PrevPhone = "C", Enabled = true },
            new() { Name = "4-way Back+Round Harmony", Target = "i", Replacement = "[αback, αround]", PrevPhone = "C", Enabled = true }
        ];

        Assert.Equal("evler", string.Concat(PhonologyEngine.Derive(["e", "v", "l", "e", "r"], turkish).SurfacePhones));
        Assert.Equal("atlar", string.Concat(PhonologyEngine.Derive(["a", "t", "l", "e", "r"], turkish).SurfacePhones));
        Assert.Equal("køjlyk", string.Concat(PhonologyEngine.Derive(["k", "ø", "j", "l", "i", "k"], turkish).SurfacePhones));
        Assert.Equal("kolluk", string.Concat(PhonologyEngine.Derive(["k", "o", "l", "l", "i", "k"], turkish).SurfacePhones));
        Assert.Equal("kɯzlɯk", string.Concat(PhonologyEngine.Derive(["k", "ɯ", "z", "l", "i", "k"], turkish).SurfacePhones));

        var mandarin = ProjectFactory.CreateStarterProject();
        mandarin.Phonemes =
        [
            .. new[] { "n", "x" }.Select(c => ProjectFactory.CreatePhoneme(c, "C")),
            .. new[] { "i", "aʊ" }.Select(v => ProjectFactory.CreatePhoneme(v, "V"))
        ];
        mandarin.Lexemes =
        [
            new() { Lemma = "you", Pos = "PRON", UR = "n i˧˩˧" },
            new() { Lemma = "good", Pos = "V", UR = "x aʊ˧˩˧" }
        ];
        mandarin.PhonologyRules =
        [
            new() { Name = "Third-Tone Sandhi", Target = "[tone=LHL]", Replacement = "[tone=LH]", NextPhone = "[tone=LHL]", Enabled = true }
        ];

        Assert.DoesNotContain(ProjectValidator.Validate(mandarin), i => i.Message.Contains("unknown phone", StringComparison.OrdinalIgnoreCase));
        var sandhi = PhonologyEngine.Derive(["n", "i˧˩˧", "x", "aʊ˧˩˧"], mandarin);
        Assert.Equal("ni˧˥xaʊ˧˩˧", string.Concat(sandhi.SurfacePhones));
    }

    [Fact]
    public void SyntaxAndMorphology_PropagatesLexClassAgreementAcrossDetAdjAndNoun()
    {
        var project = ProjectFactory.CreateStarterProject();
        project.Phonemes =
        [
            .. new[] { "l", "f", "m", "p", "t", "v", "w", "n" }.Select(c => ProjectFactory.CreatePhoneme(c, "C")),
            .. new[] { "a", "e", "i", "o", "u" }.Select(v => ProjectFactory.CreatePhoneme(v, "V"))
        ];
        project.Lexemes =
        [
            new() { Lemma = "the", Pos = "DET", UR = "l" },
            new() { Lemma = "small", Pos = "ADJ", UR = "p e t i t" },
            new() { Lemma = "woman", Pos = "N", UR = "f a m", LexClass = "Fem" },
            new() { Lemma = "see", Pos = "V", UR = "v w a" }
        ];
        project.Affixes =
        [
            new() { Name = "Fem Det", Pos = "DET", FeatureName = "Class", FeatureValue = "Fem", Affix = "-a", Enabled = true },
            new() { Name = "Fem Adj", Pos = "ADJ", FeatureName = "Class", FeatureValue = "Fem", Affix = "-e", Enabled = true },
            new() { Name = "Fem N", Pos = "N", FeatureName = "Class", FeatureValue = "Fem", Affix = "-e", Enabled = true }
        ];
        project.OrthographyRules = [];
        project.Capitalization = CapitalizationMode.None;

        var engine = new LangEngine();
        // ConfigurableSyntaxGenerator deterministically expands both SubjNP and ObjNP with ADJ at approxWords=5
        project.Pipeline.SyntaxGeneratorId = "configurable-syntax";
        var configSentence = engine.GenerateSentence(project, seed: 1, approxWords: 5, new Dictionary<string, string> { ["Number"] = "Sing" });
        Assert.Equal("petite fame vwa petite fame", configSentence.Written);

        // PhraseStructureSyntaxGenerator propagates Fem Class onto DET, optional ADJ, and N
        project.Pipeline.SyntaxGeneratorId = "phrase-structure-syntax";
        var psSentence = engine.GenerateSentence(project, seed: 4, approxWords: 5, new Dictionary<string, string> { ["Number"] = "Sing" });
        Assert.Contains("la ", psSentence.Written);
        Assert.Contains("fame", psSentence.Written);
        Assert.DoesNotContain("petit ", psSentence.Written);
    }

    [Fact]
    public void SyntaxEngine_SupportsErgAbsTripartiteAlignmentAndPolypersonalProDrop()
    {
        var ergProject = ProjectFactory.CreateStarterProject();
        ergProject.Phonemes =
        [
            .. new[] { "m", "n", "k", "t", "s", "p", "v" }.Select(c => ProjectFactory.CreatePhoneme(c, "C")),
            .. new[] { "a", "e", "i", "o", "u" }.Select(v => ProjectFactory.CreatePhoneme(v, "V"))
        ];
        ergProject.Lexemes =
        [
            new() { Lemma = "dog", Pos = "N", UR = "k a n" },
            new() { Lemma = "run", Pos = "V", UR = "k u r" }
        ];
        ergProject.Affixes =
        [
            new() { Name = "Erg", Pos = "N", FeatureName = "Case", FeatureValue = "Erg", Affix = "-n", Enabled = true },
            new() { Name = "Abs", Pos = "N", FeatureName = "Case", FeatureValue = "Abs", Affix = "-a", Enabled = true },
            new() { Name = "Intrans", Pos = "N", FeatureName = "Case", FeatureValue = "Intrans", Affix = "-o", Enabled = true }
        ];
        ergProject.OrthographyRules = [];
        ergProject.Capitalization = CapitalizationMode.None;
        ergProject.SyntaxConfig.Alignment = AlignmentSystem.ErgAbs;

        var engine = new LangEngine();
        ergProject.ClauseTemplate = ClauseTemplate.Transitive;
        var transRes = engine.GenerateSentence(ergProject, seed: 1, approxWords: 3, new Dictionary<string, string> { ["Number"] = "Sing" });
        Assert.Contains("kann", transRes.Written);
        Assert.Contains("kana", transRes.Written);

        ergProject.ClauseTemplate = ClauseTemplate.Intransitive;
        var intransRes = engine.GenerateSentence(ergProject, seed: 1, approxWords: 3, new Dictionary<string, string> { ["Number"] = "Sing" });
        Assert.Contains("kana", intransRes.Written);

        ergProject.SyntaxConfig.Alignment = AlignmentSystem.Tripartite;
        var triRes = engine.GenerateSentence(ergProject, seed: 1, approxWords: 3, new Dictionary<string, string> { ["Number"] = "Sing" });
        Assert.Contains("kano", triRes.Written);

        // Polypersonal agreement + Pro-Drop
        var poly = ProjectFactory.CreateStarterProject();
        poly.Lexemes =
        [
            new() { Lemma = "I", Pos = "PRON", UR = "m i" },
            new() { Lemma = "they", Pos = "PRON", UR = "t e" },
            new() { Lemma = "see", Pos = "V", UR = "v i d" }
        ];
        poly.Affixes =
        [
            new() { Name = "1sgSubj", Pos = "V", FeatureName = "SubjAgr", FeatureValue = "1sg", Affix = "ni-", IsPrefix = true, Enabled = true },
            new() { Name = "3plObj", Pos = "V", FeatureName = "ObjAgr", FeatureValue = "3pl", Affix = "-wa", IsPrefix = false, Enabled = true }
        ];
        poly.OrthographyRules = [];
        poly.Capitalization = CapitalizationMode.None;
        poly.Pipeline.SyntaxGeneratorId = "phrase-structure-syntax";
        poly.SyntaxConfig.ProDrop = true;
        poly.SyntaxConfig.PolypersonalAgreement = true;

        var polyEngine = new LangEngine();
        var polyTree = polyEngine.ResolveSyntax(poly).Generate(new SentenceSpec("Transitive", 3), poly, new Random(2));
        Assert.DoesNotContain(polyTree.Leaves(), n => n.Category == "PRON");
        var vLeaf = polyTree.Leaves().First(n => n.Category == "V");
        Assert.Contains(vLeaf.Features ?? [], f => f.Name == "SubjAgr");
        Assert.Contains(vLeaf.Features ?? [], f => f.Name == "ObjAgr");
    }

    [Fact]
    public void EnglishPreset_SupportsYesNoQuestionsImperativesPassivesAndRecursiveClauses()
    {
        var eng = ProjectFactory.CreateEnglishPresetProject();
        var engine = new LangEngine();

        var qRes = engine.GenerateSentence(eng, seed: 2, approxWords: 4, new Dictionary<string, string> { ["Mood"] = "YNQuestion", ["Tense"] = "Past", ["Voice"] = "Active" });
        Assert.EndsWith("?", qRes.Written);
        Assert.True(qRes.Written.StartsWith("Did", StringComparison.Ordinal) || qRes.Written.StartsWith("Was", StringComparison.Ordinal) || qRes.Written.StartsWith("Were", StringComparison.Ordinal));

        var impRes = engine.GenerateSentence(eng, seed: 3, approxWords: 4, new Dictionary<string, string> { ["Mood"] = "Imp" });
        Assert.EndsWith("!", impRes.Written);

        var passRes = engine.GenerateSentence(eng, seed: 4, approxWords: 6, new Dictionary<string, string> { ["Voice"] = "Passive", ["Tense"] = "Past" });
        Assert.Contains("Voice=Passive", ConfigurableSyntaxGenerator.FormatSyntaxTree(passRes.Tree.Root));
        Assert.Contains("by", passRes.Written.ToLowerInvariant());

        var compRes = engine.GenerateSentence(eng, seed: 5, approxWords: 6, new Dictionary<string, string> { ["Embedded"] = "Complement", ["Relative"] = "false" });
        Assert.Contains("that", compRes.Written.ToLowerInvariant());
        Assert.Contains("CP:CompCP", ConfigurableSyntaxGenerator.FormatSyntaxTree(compRes.Tree.Root));

        var relRes = engine.GenerateSentence(eng, seed: 6, approxWords: 5, new Dictionary<string, string> { ["Relative"] = "true", ["Embedded"] = "false", ["Voice"] = "Active" });
        Assert.Contains("CP:RelCP", ConfigurableSyntaxGenerator.FormatSyntaxTree(relRes.Tree.Root));
    }

    [Fact]
    public void SyntaxEngine_SupportsHeadFinalPostpositionsQuestionParticleAndCustomCfg()
    {
        var jpn = ProjectFactory.CreateStarterProject();
        jpn.WordOrder = WordOrder.SOV;
        jpn.Pipeline.SyntaxGeneratorId = "phrase-structure-syntax";
        jpn.Lexemes =
        [
            new() { Lemma = "dog", Pos = "N", UR = "i n u", Spelling = "inu" },
            new() { Lemma = "park", Pos = "N", UR = "k o e n", Spelling = "koen" },
            new() { Lemma = "walk", Pos = "V", UR = "a r u k u", Spelling = "aruku" },
            new() { Lemma = "small", Pos = "ADJ", UR = "k o", Spelling = "ko" },
            new() { Lemma = "the", Pos = "DET", UR = "k o n o", Spelling = "kono" },
            new() { Lemma = "in", Pos = "POSTP", UR = "n i", Spelling = "ni" },
            new() { Lemma = "ka", Pos = "Q", UR = "k a", Spelling = "ka" }
        ];
        jpn.OrthographyRules = [];
        jpn.Capitalization = CapitalizationMode.None;
        jpn.SyntaxConfig.AdpositionType = AdpositionOrder.Postposition;
        jpn.SyntaxConfig.QuestionStrategy = QuestionStrategy.Particle;
        jpn.SyntaxConfig.CustomRules = ["NP -> DET N ADJ", "PP -> NP POSTP"];

        var engine = new LangEngine();
        var res = engine.GenerateSentence(jpn, seed: 1, approxWords: 6, new Dictionary<string, string> { ["Mood"] = "YNQuestion" });
        Assert.EndsWith("ka?", res.Written);
        var igt = LangEngine.FormatInterlinearGloss(engine, res.Tree, jpn);
        Assert.Contains("Orth :", igt);
        Assert.Contains("Morph:", igt);
        Assert.Contains("Gloss:", igt);
        Assert.Contains("Cat  :", igt);
    }

    [Fact]
    public void DiachronicEngine_PresetsAndEvolutionTrace()
    {
        var presets = DiachronicEngine.GetPresets();
        Assert.True(presets.Count >= 4);

        var eng = ProjectFactory.CreateEnglishPresetProject();
        var gvsRules = presets.First(p => p.Id == "great-vowel-shift").Rules;
        var traces = DiachronicEngine.TraceProtoForm("ˈk n iː x t", gvsRules, eng);
        Assert.True(traces.Count >= 2);

        var sim = DiachronicEngine.SimulateEvolution(eng, gvsRules, "Modern English");
        Assert.NotNull(sim);
        Assert.NotEmpty(sim.CognateDiffs);
    }

    [Fact]
    public void DiscourseEngine_GeneratesCoherentParagraph()
    {
        var eng = ProjectFactory.CreateEnglishPresetProject();
        var discourse = DiscourseEngine.GenerateDiscourse(eng, seed: 42, sentenceCount: 3, approxWords: 5);
        Assert.False(string.IsNullOrWhiteSpace(discourse.ParagraphText));
        Assert.Equal(3, discourse.Sentences.Count);
        Assert.NotEmpty(discourse.TrackedEntities);
    }
}

