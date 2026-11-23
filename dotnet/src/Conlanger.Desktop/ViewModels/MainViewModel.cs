using System.Collections.ObjectModel;
using System.Windows.Media;
using Conlanger.Core;
using Conlanger.Desktop.Services;
using Conlanger.SDK;

namespace Conlanger.Desktop.ViewModels;

public sealed class RenderedGlyphItem
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Scale { get; init; }
    public Geometry PathGeometry { get; init; } = Geometry.Empty;
    public IReadOnlyList<Geometry> ExtraGeometries { get; init; } = Array.Empty<Geometry>();
    public string TokenLabel { get; init; } = "";
    public string Variant { get; init; } = "";
    public double EnterX { get; init; }
    public double EnterY { get; init; }
    public double ExitX { get; init; }
    public double ExitY { get; init; }
}

public sealed class MainViewModel : ViewModelBase
{
    private readonly ProjectHistory _history = new();
    private readonly LangEngine _engine = new();
    private LanguageProject _project;

    private string _statusMessage = "Ready";
    private string _sentenceOutput = "";
    private int _seed = 42;
    private int _approxWords = 4;
    private int _wordCount = 15;
    private int _syllablesPerWord = 2;
    private string _generatedWordsText = "";
    private string _testWordUr = "p a s a";
    private string _derivationResult = "";
    private string _shapingInput = "pata sar kana";
    private bool _showAnchors = true;
    private double _canvasWidth = 760;
    private double _canvasHeight = 360;

    public MainViewModel()
    {
        _project = ProjectPersistence.LoadAutosaveOrStarter();

        NewProjectCommand = new RelayCommand(ResetProject);
        UndoCommand = new RelayCommand(Undo, () => _history.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => _history.CanRedo);
        GenerateSentenceCommand = new RelayCommand(GenerateSentence);
        GenerateWordsCommand = new RelayCommand(GenerateWords);
        TestDerivationCommand = new RelayCommand(RunDerivationPreview);
        ApplyWritingPresetCommand = new RelayCommand(() => ApplyPreset("vertical-mongolian"));
        ApplyRtlPresetCommand = new RelayCommand(() => ApplyPreset("rtl-western"));
        RefreshAllCommand = new RelayCommand(RefreshDerivedViews);

        SyncCollectionsFromProject();
        GenerateSentence();
        RunDerivationPreview();
        RefreshWritingCanvas();
        RefreshDiagnostics();
    }

    public LanguageProject Project
    {
        get => _project;
        set
        {
            _project = value;
            ProjectFactory.SyncLegacyLists(_project);
            SyncCollectionsFromProject();
            RefreshDerivedViews();
            OnPropertyChanged();
        }
    }

    public ObservableCollection<Phoneme> Phonemes { get; } = new();
    public ObservableCollection<SyllableTemplate> SyllableTemplates { get; } = new();
    public ObservableCollection<PhonologyRuleSpec> PhonologyRules { get; } = new();
    public ObservableCollection<LexemeEntry> Lexemes { get; } = new();
    public ObservableCollection<AffixEntry> Affixes { get; } = new();
    public ObservableCollection<OrthRuleEntry> OrthographyRules { get; } = new();
    public ObservableCollection<OrthExceptionEntry> OrthographyExceptions { get; } = new();
    public ObservableCollection<DiagnosticIssue> Diagnostics { get; } = new();
    public ObservableCollection<RenderedGlyphItem> ShapedGlyphs { get; } = new();
    public ObservableCollection<ShapingTraceEntry> ShapingTrace { get; } = new();
    public ObservableCollection<DiscoveredPlugin> Plugins { get; } = new();

    public Array WordOrders => Enum.GetValues(typeof(WordOrder));
    public Array ClauseTemplates => Enum.GetValues(typeof(ClauseTemplate));
    public Array CapitalizationModes => Enum.GetValues(typeof(CapitalizationMode));
    public Array FlowDirections => Enum.GetValues(typeof(FlowDirection));

    public string ProjectName
    {
        get => _project.Name;
        set { if (_project.Name != value) { _project.Name = value; OnPropertyChanged(); PersistAutosave(); } }
    }

    public WordOrder SelectedWordOrder
    {
        get => _project.WordOrder;
        set { _project.WordOrder = value; OnPropertyChanged(); GenerateSentence(); PersistAutosave(); }
    }

    public ClauseTemplate SelectedClauseTemplate
    {
        get => _project.ClauseTemplate;
        set { _project.ClauseTemplate = value; OnPropertyChanged(); GenerateSentence(); PersistAutosave(); }
    }

    public CapitalizationMode SelectedCapitalization
    {
        get => _project.Capitalization;
        set { _project.Capitalization = value; OnPropertyChanged(); GenerateSentence(); PersistAutosave(); }
    }

    public FlowDirection SelectedFlowDirection
    {
        get => _project.WritingSystem.FlowDirection;
        set { _project.WritingSystem.FlowDirection = value; OnPropertyChanged(); RefreshWritingCanvas(); PersistAutosave(); }
    }

    public bool CursiveBonding
    {
        get => _project.WritingSystem.CursiveBonding;
        set { _project.WritingSystem.CursiveBonding = value; OnPropertyChanged(); RefreshWritingCanvas(); PersistAutosave(); }
    }

    public bool ShowAnchors
    {
        get => _showAnchors;
        set => SetField(ref _showAnchors, value);
    }

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public string SentenceOutput { get => _sentenceOutput; set => SetField(ref _sentenceOutput, value); }
    public int Seed { get => _seed; set { if (SetField(ref _seed, value)) GenerateSentence(); } }
    public int ApproxWords { get => _approxWords; set { if (SetField(ref _approxWords, value)) GenerateSentence(); } }
    public int WordCount { get => _wordCount; set => SetField(ref _wordCount, value); }
    public int SyllablesPerWord { get => _syllablesPerWord; set => SetField(ref _syllablesPerWord, value); }
    public string GeneratedWordsText { get => _generatedWordsText; set => SetField(ref _generatedWordsText, value); }
    public string TestWordUr { get => _testWordUr; set { if (SetField(ref _testWordUr, value)) RunDerivationPreview(); } }
    public string DerivationResult { get => _derivationResult; set => SetField(ref _derivationResult, value); }
    public string ShapingInput { get => _shapingInput; set { if (SetField(ref _shapingInput, value)) RefreshWritingCanvas(); } }
    public double CanvasWidth { get => _canvasWidth; set => SetField(ref _canvasWidth, value); }
    public double CanvasHeight { get => _canvasHeight; set => SetField(ref _canvasHeight, value); }

    public RelayCommand NewProjectCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand GenerateSentenceCommand { get; }
    public RelayCommand GenerateWordsCommand { get; }
    public RelayCommand TestDerivationCommand { get; }
    public RelayCommand ApplyWritingPresetCommand { get; }
    public RelayCommand ApplyRtlPresetCommand { get; }
    public RelayCommand RefreshAllCommand { get; }

    public void CommitSnapshot()
    {
        _history.RecordSnapshot(_project);
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        PersistAutosave();
    }

    private void SyncCollectionsFromProject()
    {
        ReplaceCollection(Phonemes, _project.Phonemes);
        ReplaceCollection(SyllableTemplates, _project.SyllableTemplates);
        ReplaceCollection(PhonologyRules, _project.PhonologyRules);
        ReplaceCollection(Lexemes, _project.Lexemes);
        ReplaceCollection(Affixes, _project.Affixes);
        ReplaceCollection(OrthographyRules, _project.OrthographyRules);
        ReplaceCollection(OrthographyExceptions, _project.OrthographyExceptions);
        ReplaceCollection(Plugins, new PluginService().DiscoverPlugins());

        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(SelectedWordOrder));
        OnPropertyChanged(nameof(SelectedClauseTemplate));
        OnPropertyChanged(nameof(SelectedCapitalization));
        OnPropertyChanged(nameof(SelectedFlowDirection));
        OnPropertyChanged(nameof(CursiveBonding));
    }

    public void PushCollectionsBackToProject()
    {
        _project.Phonemes = Phonemes.ToList();
        _project.SyllableTemplates = SyllableTemplates.ToList();
        _project.PhonologyRules = PhonologyRules.ToList();
        _project.Lexemes = Lexemes.ToList();
        _project.Affixes = Affixes.ToList();
        _project.OrthographyRules = OrthographyRules.ToList();
        _project.OrthographyExceptions = OrthographyExceptions.ToList();
        ProjectFactory.SyncLegacyLists(_project);
        PersistAutosave();
        RefreshDiagnostics();
    }

    private void RefreshDerivedViews()
    {
        PushCollectionsBackToProject();
        GenerateSentence();
        RunDerivationPreview();
        RefreshWritingCanvas();
        RefreshDiagnostics();
    }

    private void GenerateSentence()
    {
        var res = _engine.GenerateSentence(_project, Seed, ApproxWords);
        SentenceOutput = res.Written;
        StatusMessage = $"Generated {res.SurfaceWords.Count} words (seed={Seed})";
    }

    private void GenerateWords()
    {
        PushCollectionsBackToProject();
        var words = WordGenerator.GenerateWords(_project, WordCount, SyllablesPerWord, Seed);
        GeneratedWordsText = string.Join(Environment.NewLine, words);
        StatusMessage = $"Generated {words.Count} candidate words";
    }

    private void RunDerivationPreview()
    {
        var ur = TestWordUr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var res = PhonologyEngine.Derive(ur, _project);
        var sylText = string.Join(" . ", res.Syllables.Select(s =>
            string.Concat(s.Onset) + string.Concat(s.Nucleus) + string.Concat(s.Coda)));
        DerivationResult = $"Surface: [{string.Join(" ", res.SurfacePhones)}]  |  Syllables: /{sylText}/" +
            (res.PhonotacticIssues.Count > 0 ? $"  |  Issues: {string.Join("; ", res.PhonotacticIssues)}" : "  |  Legal");
    }

    private void ApplyPreset(string key)
    {
        CommitSnapshot();
        _project.WritingSystem = WritingSystemEngine.CreatePreset(key);
        OnPropertyChanged(nameof(SelectedFlowDirection));
        OnPropertyChanged(nameof(CursiveBonding));
        RefreshWritingCanvas();
        PersistAutosave();
    }

    private void RefreshWritingCanvas()
    {
        var tokens = WritingSystemEngine.TokenizeRawInput(ShapingInput, _project.WritingSystem.Glyphs.Keys);
        var result = WritingSystemEngine.RunShapingGraph(tokens, _project.WritingSystem);
        CanvasWidth = result.Width;
        CanvasHeight = result.Height;
        double scale = _project.WritingSystem.GlyphScale > 0 ? _project.WritingSystem.GlyphScale : 0.72;

        ShapedGlyphs.Clear();
        foreach (var g in result.Glyphs)
        {
            Geometry geom;
            try { geom = Geometry.Parse(g.SvgPath); }
            catch { geom = Geometry.Empty; }

            var extras = new List<Geometry>();
            foreach (var ep in g.ExtraPaths)
            {
                try { extras.Add(Geometry.Parse(ep)); } catch { }
            }

            ShapedGlyphs.Add(new RenderedGlyphItem
            {
                X = g.X,
                Y = g.Y,
                Scale = scale,
                PathGeometry = geom,
                ExtraGeometries = extras,
                TokenLabel = g.Token.Grapheme,
                Variant = g.VariantUsed,
                EnterX = g.EnteringWorld.X,
                EnterY = g.EnteringWorld.Y,
                ExitX = g.ExitingWorld.X,
                ExitY = g.ExitingWorld.Y
            });
        }

        ReplaceCollection(ShapingTrace, result.Trace);
    }

    private void RefreshDiagnostics() => ReplaceCollection(Diagnostics, ProjectValidator.Validate(_project));

    private void ResetProject()
    {
        CommitSnapshot();
        Project = ProjectFactory.CreateStarterProject();
        StatusMessage = "Reset to clean Proto-Conlang starter";
    }

    private void Undo()
    {
        var prev = _history.Undo(_project);
        if (prev is null) return;
        Project = prev;
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
    }

    private void Redo()
    {
        var next = _history.Redo(_project);
        if (next is null) return;
        Project = next;
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
    }

    private void PersistAutosave()
    {
        try { ProjectPersistence.SaveAutosave(_project); } catch { }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }
}
