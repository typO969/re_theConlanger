using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
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

public sealed class PipelineOptionItem(string id, string name, string description)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;
}

public sealed class PipelinePhonoRuleItem(string id, string name, string description, bool isEnabled, Action onChanged) : ViewModelBase
{
    private bool _isEnabled = isEnabled;
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetField(ref _isEnabled, value))
                onChanged();
        }
    }
}

public sealed class PluginActionArgRow : ViewModelBase
{
    private string _value;
    public string Key { get; }
    public string Label { get; }
    public PluginArgType ArgType { get; }
    public bool Required { get; }
    public string Description { get; }
    public IReadOnlyList<string>? Choices { get; }

    public PluginActionArgRow(PluginArgSpec spec)
    {
        Key = spec.Key;
        Label = spec.Label;
        ArgType = spec.Type;
        Required = spec.Required;
        Description = spec.Description;
        Choices = spec.Choices;
        _value = spec.DefaultValue?.ToString() ?? "";
    }

    public string Value
    {
        get => _value;
        set => SetField(ref _value, value);
    }
}

public sealed class MainViewModel : ViewModelBase
{
    private readonly ProjectHistory _history = new();
    private readonly PipelineRegistry _pipelineRegistry;
    private readonly LangEngine _engine;
    private readonly bool _isDesignMode;
    private LanguageProject _project;
    private CancellationTokenSource? _autosaveCts;

    private string _currentFileName = "(unsaved)";
    private string _currentFilePath = "";
    private string _autosaveTime = "";
    private string _statusMessage = "Ready";
    private int _errorCount;
    private int _warningCount;

    // Phonology tab state
    private string _newPhonemeSymbol = "";
    private int _newPhonemeWeight = 1;
    private bool _newAllowOnset = true;
    private bool _newAllowCoda = true;
    private bool _showFeatureEditor;
    private string _newTemplatePattern = "CCV";
    private int _newTemplateWeight = 1;
    private string _newRuleName = "Voicing assimilation";
    private string _newRuleTarget = "s";
    private string _newRuleReplacement = "z";
    private string _newRulePrev = "";
    private string _newRuleNext = "";
    private string _newRuleCond = "";
    private PhonologyRuleSpec? _selectedRule;
    private string _testWordUr = "a s a";
    private string _derivationResult = "";

    // Morphology tab state
    private string _newLexLemma = "";
    private string _newLexPos = "N";
    private string _newLexUr = "";
    private string _newAffixName = "";
    private string _newAffixPos = "N";
    private string _newAffixFeatName = "Number";
    private string _newAffixFeatVal = "Pl";
    private string _newAffixForm = "-i";
    private bool _newAffixIsPrefix;
    private string _morphPreviewLemma = "person";
    private string _morphPreviewPos = "N";
    private string _morphPreviewFeatName = "Number";
    private string _morphPreviewFeatVal = "Pl";
    private string _morphPreviewOutput = "";

    // Syntax tab state
    private int _syntaxPreviewSeed = 1;
    private int _syntaxApproxWords = 4;
    private string _syntaxLinearPreview = "";
    private string _syntaxTreeSummary = "";

    // Orthography tab state
    private string _newOrthPhone = "";
    private string _newOrthGrapheme = "";
    private string _newOrthPrev = "";
    private string _newOrthNext = "";
    private string _newExInput = "";
    private string _newExOutput = "";
    private string _orthPreviewInput = "ʃika taka";
    private string _orthPreviewOutput = "";
    private string _orthPreviewTokens = "";

    // Writing Systems tab state
    private bool _useEngineLayer1;
    private string _shapingInput = "shika taka person";
    private int _wsEngineSeed = 1;
    private string _layer1SourceDisplay = "";
    private string _layer1TokensSummary = "";
    private bool _showAnchors = true;
    private string _selectedGlyphId = "a";
    private string _newGlyphId = "";
    private string _newFeaturicName = "Inter-word Dot";
    private string _newFeaturicTarget = "*";
    private string _newFeaturicContext = "initial";
    private string _newFeaturicPath = "M 48 8 A 3 3 0 1 0 54 8 A 3 3 0 1 0 48 8";
    private double _canvasWidth = 760;
    private double _canvasHeight = 340;

    // Output tab state
    private int _sampleCount = 5;
    private int _outputApproxWords = 6;
    private string _outputSeedText = "";
    private string _constraintFeatName = "Tense";
    private string _constraintFeatVal = "Past";
    private string _batchOutputText = "";
    private int _wordCount = 15;
    private int _syllablesPerWord = 2;
    private string _generatedWordsText = "";

    // Inspectors tab state
    private string _selectedInspectorLexeme = "person";
    private string _inspectorLexemeText = "";
    private string _inspectorSyntaxTreeText = "";
    private string _inspectorPhonologyTraceText = "";

    // Extensions tab state
    private DiscoveredPlugin? _selectedPlugin;
    private DiscoveredAction? _selectedAction;
    private ExecutedPluginOutput? _selectedPluginOutput;
    private string _pluginLogsText = "";

    public MainViewModel()
    {
        _isDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
        var pluginDirs = _isDesignMode ? Array.Empty<string>() : PluginService.GetPluginSearchPaths();
        _pipelineRegistry = PipelineRegistry.CreateDefaultWithPlugins(pluginDirs.FirstOrDefault());
        if (!_isDesignMode)
        {
            foreach (var dir in pluginDirs.Skip(1))
                _pipelineRegistry.LoadExtensionsFromDirectory(dir);
        }
        _engine = new LangEngine(_pipelineRegistry);
        _project = ProjectFactory.CreateStarterProject();
        _history.PushSnapshot(_project);

        NewProjectCommand = new AsyncRelayCommand(ResetProjectAsync);
        UndoCommand = new RelayCommand(Undo, () => _history.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => _history.CanRedo);
        RefreshAllCommand = new RelayCommand(RefreshDerivedViews);
        OpenRecentCommand = new AsyncRelayCommand(p => OpenRecentAsync(p as RecentProjectRecord));
        RestoreBackupCommand = new AsyncRelayCommand(p => RestoreBackupAsync(p as BackupSnapshotRecord));

        AddConsonantCommand = new RelayCommand(() => AddPhoneme("C"), () => !string.IsNullOrWhiteSpace(NewPhonemeSymbol));
        AddVowelCommand = new RelayCommand(() => AddPhoneme("V"), () => !string.IsNullOrWhiteSpace(NewPhonemeSymbol));
        RemovePhonemeCommand = new RelayCommand(p => RemovePhoneme(p as Phoneme));
        ToggleFeatureEditorCommand = new RelayCommand(() => ShowFeatureEditor = !ShowFeatureEditor);
        AddTemplateCommand = new RelayCommand(AddTemplate, () => !string.IsNullOrWhiteSpace(NewTemplatePattern));
        RemoveTemplateCommand = new RelayCommand(p => RemoveTemplate(p as SyllableTemplate));
        AddRuleCommand = new RelayCommand(AddPhonoRule, () => !string.IsNullOrWhiteSpace(NewRuleName));
        RemoveRuleCommand = new RelayCommand(p => RemovePhonoRule(p as PhonologyRuleSpec));
        MoveRuleUpCommand = new RelayCommand(p => MovePhonoRule(p as PhonologyRuleSpec, -1));
        MoveRuleDownCommand = new RelayCommand(p => MovePhonoRule(p as PhonologyRuleSpec, 1));
        TestDerivationCommand = new RelayCommand(RunDerivationPreview);

        AddLexemeCommand = new RelayCommand(AddLexeme, () => !string.IsNullOrWhiteSpace(NewLexLemma));
        RemoveLexemeCommand = new RelayCommand(p => RemoveLexeme(p as LexemeEntry));
        AddAffixCommand = new RelayCommand(AddAffix, () => !string.IsNullOrWhiteSpace(NewAffixName));
        RemoveAffixCommand = new RelayCommand(p => RemoveAffix(p as AffixEntry));
        RefreshMorphPreviewCommand = new RelayCommand(RefreshMorphPreview);

        SampleSyntaxSeedCommand = new RelayCommand(() => { SyntaxPreviewSeed++; RefreshSyntaxPreview(); });

        AddOrthRuleCommand = new RelayCommand(AddOrthRule, () => !string.IsNullOrWhiteSpace(NewOrthPhone));
        RemoveOrthRuleCommand = new RelayCommand(p => RemoveOrthRule(p as OrthRuleEntry));
        AddOrthExceptionCommand = new RelayCommand(AddOrthException, () => !string.IsNullOrWhiteSpace(NewExInput));
        RemoveOrthExceptionCommand = new RelayCommand(p => RemoveOrthException(p as OrthExceptionEntry));
        RefreshOrthPreviewCommand = new RelayCommand(RefreshOrthPreview);

        ApplyRtlPresetCommand = new RelayCommand(() => ApplyWritingPreset("rtl-western"));
        ApplyVerticalPresetCommand = new RelayCommand(() => ApplyWritingPreset("vertical-mongolian"));
        ApplyBoustrophedonPresetCommand = new RelayCommand(() => ApplyWritingPreset("boustrophedon"));
        NextEngineSeedCommand = new RelayCommand(() => { WsEngineSeed++; RefreshWritingCanvas(); });
        AddGlyphCommand = new RelayCommand(AddCustomGlyph, () => !string.IsNullOrWhiteSpace(NewGlyphId));
        AddFeaturicRuleCommand = new RelayCommand(AddFeaturicRule, () => !string.IsNullOrWhiteSpace(NewFeaturicName) && !string.IsNullOrWhiteSpace(NewFeaturicPath));
        RemoveFeaturicRuleCommand = new RelayCommand(p => RemoveFeaturicRule(p as FeaturicAssemblyRule));

        GenerateBatchCommand = new RelayCommand(GenerateBatchSentences);
        GenerateWordsCommand = new RelayCommand(GenerateWords);
        RefreshInspectorsCommand = new RelayCommand(RefreshInspectors);

        RefreshPluginsCommand = new RelayCommand(LoadPluginsCatalog);
        RunActionCommand = new AsyncRelayCommand(p => RunPluginActionAsync(p as DiscoveredAction));

        PopulatePipelineOptions();
        SyncCollectionsFromProject();
        RefreshDerivedViews();

        if (!_isDesignMode)
        {
            _ = InitializePersistenceAndPluginsAsync();
        }
        else
        {
            LoadPluginsCatalog();
        }
    }

    private async Task InitializePersistenceAndPluginsAsync()
    {
        try
        {
            var loaded = await ProjectPersistence.LoadAutosaveOrStarterAsync().ConfigureAwait(true);
            var recents = await ProjectPersistence.LoadRecentProjectsAsync().ConfigureAwait(true);
            var backups = ProjectPersistence.LoadBackupSnapshots();

            _project = loaded;
            _history.PushSnapshot(_project);
            ReplaceCollection(RecentProjects, recents);
            ReplaceCollection(BackupSnapshots, backups);
            SyncCollectionsFromProject();
            RefreshDerivedViews();
            LoadPluginsCatalog();
        }
        catch
        {
            // Keep starter workspace if startup disk read fails
        }
    }

    public LanguageProject Project
    {
        get => _project;
        private set => _project = value;
    }

    public void LoadProject(LanguageProject project, string fileName, string filePath, bool recordHistory = true)
    {
        ProjectFactory.SyncLegacyLists(project);
        _project = project;
        CurrentFileName = fileName;
        CurrentFilePath = filePath;
        if (recordHistory) _history.PushSnapshot(_project);
        SyncCollectionsFromProject();
        RefreshDerivedViews();
        ScheduleAutosave();
    }

    public ObservableCollection<Phoneme> Phonemes { get; } = new();
    public ObservableCollection<Phoneme> Consonants { get; } = new();
    public ObservableCollection<Phoneme> Vowels { get; } = new();
    public ObservableCollection<SyllableTemplate> SyllableTemplates { get; } = new();
    public ObservableCollection<PhonologyRuleSpec> PhonologyRules { get; } = new();
    public ObservableCollection<LexemeEntry> Lexemes { get; } = new();
    public ObservableCollection<string> LexemeIds { get; } = new();
    public ObservableCollection<AffixEntry> Affixes { get; } = new();
    public ObservableCollection<OrthRuleEntry> OrthographyRules { get; } = new();
    public ObservableCollection<OrthExceptionEntry> OrthographyExceptions { get; } = new();
    public ObservableCollection<DiagnosticIssue> Diagnostics { get; } = new();
    public ObservableCollection<RenderedGlyphItem> ShapedGlyphs { get; } = new();
    public ObservableCollection<ShapingTraceEntry> ShapingTrace { get; } = new();
    public ObservableCollection<string> GlyphIds { get; } = new();
    public ObservableCollection<FeaturicAssemblyRule> FeaturicRules { get; } = new();
    public ObservableCollection<string> GeneratedSentences { get; } = new();
    public ObservableCollection<RecentProjectRecord> RecentProjects { get; } = new();
    public ObservableCollection<BackupSnapshotRecord> BackupSnapshots { get; } = new();

    public ObservableCollection<PipelineOptionItem> SyntaxModuleOptions { get; } = new();
    public ObservableCollection<PipelineOptionItem> MorphologyModuleOptions { get; } = new();
    public ObservableCollection<PipelineOptionItem> OrthographyModuleOptions { get; } = new();
    public ObservableCollection<PipelinePhonoRuleItem> PipelinePhonoRuleItems { get; } = new();

    public ObservableCollection<DiscoveredPlugin> Plugins { get; } = new();
    public ObservableCollection<DiscoveredAction> Actions { get; } = new();
    public ObservableCollection<PluginActionArgRow> ActionArgs { get; } = new();
    public ObservableCollection<ExecutedPluginOutput> PluginOutputs { get; } = new();

    public Array WordOrders => Enum.GetValues(typeof(WordOrder));
    public Array ClauseTemplates => Enum.GetValues(typeof(ClauseTemplate));
    public Array CapitalizationModes => Enum.GetValues(typeof(CapitalizationMode));
    public Array StressPatterns => Enum.GetValues(typeof(StressPattern));
    public Array FlowDirections => Enum.GetValues(typeof(FlowDirection));
    public IReadOnlyList<string> FeaturicContexts { get; } = ["any", "initial", "medial", "final"];

    // Toolbar properties
    public string ProjectName
    {
        get => _project.Name;
        set { if (_project.Name != value) { _project.Name = value; OnPropertyChanged(); ScheduleAutosave(); } }
    }

    public string CurrentFileName { get => _currentFileName; set => SetField(ref _currentFileName, value); }
    public string CurrentFilePath { get => _currentFilePath; set => SetField(ref _currentFilePath, value); }
    public string AutosaveTime { get => _autosaveTime; set => SetField(ref _autosaveTime, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public string DiagnosticsSummary => $"Diagnostics: {_errorCount} Error(s), {_warningCount} Warning(s)  |  Unified Schema v{_project.SchemaVersion}";

    // Phonology properties
    public string NewPhonemeSymbol
    {
        get => _newPhonemeSymbol;
        set { if (SetField(ref _newPhonemeSymbol, value)) { AddConsonantCommand.RaiseCanExecuteChanged(); AddVowelCommand.RaiseCanExecuteChanged(); } }
    }
    public int NewPhonemeWeight { get => _newPhonemeWeight; set => SetField(ref _newPhonemeWeight, Math.Max(1, value)); }
    public bool NewAllowOnset { get => _newAllowOnset; set => SetField(ref _newAllowOnset, value); }
    public bool NewAllowCoda { get => _newAllowCoda; set => SetField(ref _newAllowCoda, value); }
    public bool ShowFeatureEditor { get => _showFeatureEditor; set => SetField(ref _showFeatureEditor, value); }
    public string NewTemplatePattern
    {
        get => _newTemplatePattern;
        set { if (SetField(ref _newTemplatePattern, value)) AddTemplateCommand.RaiseCanExecuteChanged(); }
    }
    public int NewTemplateWeight { get => _newTemplateWeight; set => SetField(ref _newTemplateWeight, Math.Max(1, value)); }

    public string AllowedOnsetsText
    {
        get => string.Join(", ", _project.Phonotactics.AllowedOnsets);
        set
        {
            _project.Phonotactics.AllowedOnsets = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            OnPropertyChanged();
            CommitAndRefresh("Phonotactics updated");
        }
    }

    public string AllowedCodasText
    {
        get => string.Join(", ", _project.Phonotactics.AllowedCodas);
        set
        {
            _project.Phonotactics.AllowedCodas = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            OnPropertyChanged();
            CommitAndRefresh("Phonotactics updated");
        }
    }

    public int MaxOnsetCluster
    {
        get => _project.Phonotactics.MaxOnsetCluster;
        set { _project.Phonotactics.MaxOnsetCluster = Math.Clamp(value, 0, 6); OnPropertyChanged(); CommitAndRefresh("Phonotactics updated"); }
    }

    public int MaxCodaCluster
    {
        get => _project.Phonotactics.MaxCodaCluster;
        set { _project.Phonotactics.MaxCodaCluster = Math.Clamp(value, 0, 6); OnPropertyChanged(); CommitAndRefresh("Phonotactics updated"); }
    }

    public string ForbiddenPatternsText
    {
        get => string.Join(Environment.NewLine, _project.Phonotactics.ForbiddenPatterns);
        set
        {
            _project.Phonotactics.ForbiddenPatterns = value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            OnPropertyChanged();
            CommitAndRefresh("Phonotactics updated");
        }
    }

    public StressPattern SelectedStressPattern
    {
        get => _project.StressPattern;
        set { _project.StressPattern = value; OnPropertyChanged(); CommitAndRefresh("Stress pattern updated"); }
    }

    public string NewRuleName { get => _newRuleName; set { if (SetField(ref _newRuleName, value)) AddRuleCommand.RaiseCanExecuteChanged(); } }
    public string NewRuleTarget { get => _newRuleTarget; set => SetField(ref _newRuleTarget, value); }
    public string NewRuleReplacement { get => _newRuleReplacement; set => SetField(ref _newRuleReplacement, value); }
    public string NewRulePrev { get => _newRulePrev; set => SetField(ref _newRulePrev, value); }
    public string NewRuleNext { get => _newRuleNext; set => SetField(ref _newRuleNext, value); }
    public string NewRuleCond { get => _newRuleCond; set => SetField(ref _newRuleCond, value); }
    public PhonologyRuleSpec? SelectedRule { get => _selectedRule; set => SetField(ref _selectedRule, value); }
    public string TestWordUr { get => _testWordUr; set { if (SetField(ref _testWordUr, value)) RunDerivationPreview(); } }
    public string DerivationResult { get => _derivationResult; set => SetField(ref _derivationResult, value); }

    // Morphology properties
    public string NewLexLemma { get => _newLexLemma; set { if (SetField(ref _newLexLemma, value)) AddLexemeCommand.RaiseCanExecuteChanged(); } }
    public string NewLexPos { get => _newLexPos; set => SetField(ref _newLexPos, value); }
    public string NewLexUr { get => _newLexUr; set => SetField(ref _newLexUr, value); }
    public string NewAffixName { get => _newAffixName; set { if (SetField(ref _newAffixName, value)) AddAffixCommand.RaiseCanExecuteChanged(); } }
    public string NewAffixPos { get => _newAffixPos; set => SetField(ref _newAffixPos, value); }
    public string NewAffixFeatName { get => _newAffixFeatName; set => SetField(ref _newAffixFeatName, value); }
    public string NewAffixFeatVal { get => _newAffixFeatVal; set => SetField(ref _newAffixFeatVal, value); }
    public string NewAffixForm { get => _newAffixForm; set => SetField(ref _newAffixForm, value); }
    public bool NewAffixIsPrefix { get => _newAffixIsPrefix; set => SetField(ref _newAffixIsPrefix, value); }
    public string MorphPreviewLemma { get => _morphPreviewLemma; set { if (SetField(ref _morphPreviewLemma, value)) RefreshMorphPreview(); } }
    public string MorphPreviewPos { get => _morphPreviewPos; set { if (SetField(ref _morphPreviewPos, value)) RefreshMorphPreview(); } }
    public string MorphPreviewFeatName { get => _morphPreviewFeatName; set { if (SetField(ref _morphPreviewFeatName, value)) RefreshMorphPreview(); } }
    public string MorphPreviewFeatVal { get => _morphPreviewFeatVal; set { if (SetField(ref _morphPreviewFeatVal, value)) RefreshMorphPreview(); } }
    public string MorphPreviewOutput { get => _morphPreviewOutput; set => SetField(ref _morphPreviewOutput, value); }

    // Syntax properties
    public WordOrder SelectedWordOrder
    {
        get => _project.WordOrder;
        set { _project.WordOrder = value; OnPropertyChanged(); CommitAndRefresh("Word order updated"); }
    }
    public ClauseTemplate SelectedClauseTemplate
    {
        get => _project.ClauseTemplate;
        set { _project.ClauseTemplate = value; OnPropertyChanged(); CommitAndRefresh("Clause template updated"); }
    }
    public int SyntaxApproxWords { get => _syntaxApproxWords; set { if (SetField(ref _syntaxApproxWords, Math.Clamp(value, 1, 8))) RefreshSyntaxPreview(); } }
    public int SyntaxPreviewSeed { get => _syntaxPreviewSeed; set { if (SetField(ref _syntaxPreviewSeed, value)) RefreshSyntaxPreview(); } }
    public string SyntaxLinearPreview { get => _syntaxLinearPreview; set => SetField(ref _syntaxLinearPreview, value); }
    public string SyntaxTreeSummary { get => _syntaxTreeSummary; set => SetField(ref _syntaxTreeSummary, value); }

    // Orthography properties
    public CapitalizationMode SelectedCapitalization
    {
        get => _project.Capitalization;
        set { _project.Capitalization = value; OnPropertyChanged(); CommitAndRefresh("Capitalization updated"); }
    }
    public string NewOrthPhone { get => _newOrthPhone; set { if (SetField(ref _newOrthPhone, value)) AddOrthRuleCommand.RaiseCanExecuteChanged(); } }
    public string NewOrthGrapheme { get => _newOrthGrapheme; set => SetField(ref _newOrthGrapheme, value); }
    public string NewOrthPrev { get => _newOrthPrev; set => SetField(ref _newOrthPrev, value); }
    public string NewOrthNext { get => _newOrthNext; set => SetField(ref _newOrthNext, value); }
    public string NewExInput { get => _newExInput; set { if (SetField(ref _newExInput, value)) AddOrthExceptionCommand.RaiseCanExecuteChanged(); } }
    public string NewExOutput { get => _newExOutput; set => SetField(ref _newExOutput, value); }
    public string OrthPreviewInput { get => _orthPreviewInput; set { if (SetField(ref _orthPreviewInput, value)) RefreshOrthPreview(); } }
    public string OrthPreviewOutput { get => _orthPreviewOutput; set => SetField(ref _orthPreviewOutput, value); }
    public string OrthPreviewTokens { get => _orthPreviewTokens; set => SetField(ref _orthPreviewTokens, value); }

    // Writing systems properties
    public bool UseEngineLayer1
    {
        get => _useEngineLayer1;
        set { if (SetField(ref _useEngineLayer1, value)) { OnPropertyChanged(nameof(UseSandboxLayer1)); RefreshWritingCanvas(); } }
    }
    public bool UseSandboxLayer1 => !_useEngineLayer1;
    public string ShapingInput { get => _shapingInput; set { if (SetField(ref _shapingInput, value)) RefreshWritingCanvas(); } }
    public int WsEngineSeed { get => _wsEngineSeed; set { if (SetField(ref _wsEngineSeed, value)) RefreshWritingCanvas(); } }
    public string Layer1SourceDisplay { get => _layer1SourceDisplay; set => SetField(ref _layer1SourceDisplay, value); }
    public string Layer1TokensSummary { get => _layer1TokensSummary; set => SetField(ref _layer1TokensSummary, value); }
    public bool ShowAnchors { get => _showAnchors; set => SetField(ref _showAnchors, value); }
    public FlowDirection SelectedFlowDirection
    {
        get => _project.WritingSystem.FlowDirection;
        set { _project.WritingSystem.FlowDirection = value; OnPropertyChanged(); RefreshWritingCanvas(); ScheduleAutosave(); }
    }
    public bool CursiveBonding
    {
        get => _project.WritingSystem.CursiveBonding;
        set { _project.WritingSystem.CursiveBonding = value; OnPropertyChanged(); RefreshWritingCanvas(); ScheduleAutosave(); }
    }
    public bool ContextualVariants
    {
        get => _project.WritingSystem.ContextualVariants;
        set { _project.WritingSystem.ContextualVariants = value; OnPropertyChanged(); RefreshWritingCanvas(); ScheduleAutosave(); }
    }
    public double GlyphScale
    {
        get => _project.WritingSystem.GlyphScale;
        set { _project.WritingSystem.GlyphScale = Math.Clamp(value, 0.3, 1.5); OnPropertyChanged(); RefreshWritingCanvas(); ScheduleAutosave(); }
    }
    public double LetterSpacing
    {
        get => _project.WritingSystem.LetterSpacing;
        set { _project.WritingSystem.LetterSpacing = Math.Clamp(value, -20, 40); OnPropertyChanged(); RefreshWritingCanvas(); ScheduleAutosave(); }
    }
    public double WrapLimit
    {
        get => _project.WritingSystem.WrapLimit;
        set { _project.WritingSystem.WrapLimit = Math.Clamp(value, 200, 1000); OnPropertyChanged(); RefreshWritingCanvas(); ScheduleAutosave(); }
    }

    public string SelectedGlyphId
    {
        get => _selectedGlyphId;
        set
        {
            if (SetField(ref _selectedGlyphId, value))
            {
                OnPropertyChanged(nameof(SelectedGlyphSvgPath));
                OnPropertyChanged(nameof(SelectedGlyphPreviewGeometry));
                OnPropertyChanged(nameof(SelectedGlyphEnterX));
                OnPropertyChanged(nameof(SelectedGlyphEnterY));
                OnPropertyChanged(nameof(SelectedGlyphExitX));
                OnPropertyChanged(nameof(SelectedGlyphExitY));
            }
        }
    }

    private GlyphDefinition? ActiveGlyph =>
        _project.WritingSystem.Glyphs.TryGetValue(_selectedGlyphId, out var g) ? g : null;

    public string SelectedGlyphSvgPath
    {
        get => ActiveGlyph?.SvgPath ?? "";
        set
        {
            if (ActiveGlyph is null) return;
            ActiveGlyph.SvgPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedGlyphPreviewGeometry));
            RefreshWritingCanvas();
            ScheduleAutosave();
        }
    }

    public Geometry SelectedGlyphPreviewGeometry
    {
        get
        {
            try { return string.IsNullOrWhiteSpace(SelectedGlyphSvgPath) ? Geometry.Empty : Geometry.Parse(SelectedGlyphSvgPath); }
            catch { return Geometry.Empty; }
        }
    }

    public double SelectedGlyphEnterX
    {
        get => ActiveGlyph?.EnteringAnchor.X ?? 15;
        set { if (ActiveGlyph is null) return; ActiveGlyph.EnteringAnchor = ActiveGlyph.EnteringAnchor with { X = value }; OnPropertyChanged(); RefreshWritingCanvas(); ScheduleAutosave(); }
    }
    public double SelectedGlyphEnterY
    {
        get => ActiveGlyph?.EnteringAnchor.Y ?? 55;
        set { if (ActiveGlyph is null) return; ActiveGlyph.EnteringAnchor = ActiveGlyph.EnteringAnchor with { Y = value }; OnPropertyChanged(); RefreshWritingCanvas(); ScheduleAutosave(); }
    }
    public double SelectedGlyphExitX
    {
        get => ActiveGlyph?.ExitingAnchor.X ?? 85;
        set { if (ActiveGlyph is null) return; ActiveGlyph.ExitingAnchor = ActiveGlyph.ExitingAnchor with { X = value }; OnPropertyChanged(); RefreshWritingCanvas(); ScheduleAutosave(); }
    }
    public double SelectedGlyphExitY
    {
        get => ActiveGlyph?.ExitingAnchor.Y ?? 55;
        set { if (ActiveGlyph is null) return; ActiveGlyph.ExitingAnchor = ActiveGlyph.ExitingAnchor with { Y = value }; OnPropertyChanged(); RefreshWritingCanvas(); ScheduleAutosave(); }
    }

    public string NewGlyphId { get => _newGlyphId; set { if (SetField(ref _newGlyphId, value)) AddGlyphCommand.RaiseCanExecuteChanged(); } }
    public string NewFeaturicName { get => _newFeaturicName; set { if (SetField(ref _newFeaturicName, value)) AddFeaturicRuleCommand.RaiseCanExecuteChanged(); } }
    public string NewFeaturicTarget { get => _newFeaturicTarget; set => SetField(ref _newFeaturicTarget, value); }
    public string NewFeaturicContext { get => _newFeaturicContext; set => SetField(ref _newFeaturicContext, value); }
    public string NewFeaturicPath { get => _newFeaturicPath; set { if (SetField(ref _newFeaturicPath, value)) AddFeaturicRuleCommand.RaiseCanExecuteChanged(); } }
    public double CanvasWidth { get => _canvasWidth; set => SetField(ref _canvasWidth, value); }
    public double CanvasHeight { get => _canvasHeight; set => SetField(ref _canvasHeight, value); }

    // Output properties
    public int SampleCount { get => _sampleCount; set => SetField(ref _sampleCount, Math.Max(1, value)); }
    public int OutputApproxWords { get => _outputApproxWords; set => SetField(ref _outputApproxWords, Math.Max(1, value)); }
    public string OutputSeedText { get => _outputSeedText; set => SetField(ref _outputSeedText, value); }
    public string ConstraintFeatName { get => _constraintFeatName; set => SetField(ref _constraintFeatName, value); }
    public string ConstraintFeatVal { get => _constraintFeatVal; set => SetField(ref _constraintFeatVal, value); }
    public string BatchOutputText { get => _batchOutputText; set => SetField(ref _batchOutputText, value); }
    public int WordCount { get => _wordCount; set => SetField(ref _wordCount, Math.Max(1, value)); }
    public int SyllablesPerWord { get => _syllablesPerWord; set => SetField(ref _syllablesPerWord, Math.Max(1, value)); }
    public bool HyphenateSyllables
    {
        get => _project.GeneratorOptions.HyphenateSyllables;
        set { _project.GeneratorOptions.HyphenateSyllables = value; OnPropertyChanged(); ScheduleAutosave(); }
    }
    public bool AllowDuplicates
    {
        get => _project.GeneratorOptions.AllowDuplicates;
        set { _project.GeneratorOptions.AllowDuplicates = value; OnPropertyChanged(); ScheduleAutosave(); }
    }
    public string GeneratedWordsText { get => _generatedWordsText; set => SetField(ref _generatedWordsText, value); }

    // Inspectors properties
    public string SelectedInspectorLexeme { get => _selectedInspectorLexeme; set { if (SetField(ref _selectedInspectorLexeme, value)) RefreshInspectors(); } }
    public string InspectorLexemeText { get => _inspectorLexemeText; set => SetField(ref _inspectorLexemeText, value); }
    public string InspectorSyntaxTreeText { get => _inspectorSyntaxTreeText; set => SetField(ref _inspectorSyntaxTreeText, value); }
    public string InspectorPhonologyTraceText { get => _inspectorPhonologyTraceText; set => SetField(ref _inspectorPhonologyTraceText, value); }

    // Phase 4 & Extensions properties
    public string SelectedSyntaxGeneratorId
    {
        get => _project.Pipeline.SyntaxGeneratorId;
        set { _project.Pipeline.SyntaxGeneratorId = value; OnPropertyChanged(); CommitAndRefresh("Swapped syntax generator"); }
    }
    public string SelectedMorphologyRealizerId
    {
        get => _project.Pipeline.MorphologyRealizerId;
        set { _project.Pipeline.MorphologyRealizerId = value; OnPropertyChanged(); CommitAndRefresh("Swapped morphology realizer"); }
    }
    public string SelectedOrthographyRendererId
    {
        get => _project.Pipeline.OrthographyRendererId;
        set { _project.Pipeline.OrthographyRendererId = value; OnPropertyChanged(); CommitAndRefresh("Swapped orthography renderer"); }
    }
    public DiscoveredPlugin? SelectedPlugin { get => _selectedPlugin; set => SetField(ref _selectedPlugin, value); }
    public DiscoveredAction? SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (SetField(ref _selectedAction, value))
            {
                ActionArgs.Clear();
                if (value is not null)
                {
                    foreach (var a in value.Args) ActionArgs.Add(new PluginActionArgRow(a));
                }
            }
        }
    }
    public ExecutedPluginOutput? SelectedPluginOutput { get => _selectedPluginOutput; set => SetField(ref _selectedPluginOutput, value); }
    public string PluginLogsText { get => _pluginLogsText; set => SetField(ref _pluginLogsText, value); }

    // Commands
    public AsyncRelayCommand NewProjectCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand RefreshAllCommand { get; }
    public AsyncRelayCommand OpenRecentCommand { get; }
    public AsyncRelayCommand RestoreBackupCommand { get; }
    public RelayCommand AddConsonantCommand { get; }
    public RelayCommand AddVowelCommand { get; }
    public RelayCommand RemovePhonemeCommand { get; }
    public RelayCommand ToggleFeatureEditorCommand { get; }
    public RelayCommand AddTemplateCommand { get; }
    public RelayCommand RemoveTemplateCommand { get; }
    public RelayCommand AddRuleCommand { get; }
    public RelayCommand RemoveRuleCommand { get; }
    public RelayCommand MoveRuleUpCommand { get; }
    public RelayCommand MoveRuleDownCommand { get; }
    public RelayCommand TestDerivationCommand { get; }
    public RelayCommand AddLexemeCommand { get; }
    public RelayCommand RemoveLexemeCommand { get; }
    public RelayCommand AddAffixCommand { get; }
    public RelayCommand RemoveAffixCommand { get; }
    public RelayCommand RefreshMorphPreviewCommand { get; }
    public RelayCommand SampleSyntaxSeedCommand { get; }
    public RelayCommand AddOrthRuleCommand { get; }
    public RelayCommand RemoveOrthRuleCommand { get; }
    public RelayCommand AddOrthExceptionCommand { get; }
    public RelayCommand RemoveOrthExceptionCommand { get; }
    public RelayCommand RefreshOrthPreviewCommand { get; }
    public RelayCommand ApplyRtlPresetCommand { get; }
    public RelayCommand ApplyVerticalPresetCommand { get; }
    public RelayCommand ApplyBoustrophedonPresetCommand { get; }
    public RelayCommand NextEngineSeedCommand { get; }
    public RelayCommand AddGlyphCommand { get; }
    public RelayCommand AddFeaturicRuleCommand { get; }
    public RelayCommand RemoveFeaturicRuleCommand { get; }
    public RelayCommand GenerateBatchCommand { get; }
    public RelayCommand GenerateWordsCommand { get; }
    public RelayCommand RefreshInspectorsCommand { get; }
    public RelayCommand RefreshPluginsCommand { get; }
    public AsyncRelayCommand RunActionCommand { get; }

    private void PopulatePipelineOptions()
    {
        ReplaceCollection(SyntaxModuleOptions, _pipelineRegistry.SyntaxGenerators.Select(m => new PipelineOptionItem(m.Id, m.Name, m.Description)));
        ReplaceCollection(MorphologyModuleOptions, _pipelineRegistry.MorphologyRealizers.Select(m => new PipelineOptionItem(m.Id, m.Name, m.Description)));
        ReplaceCollection(OrthographyModuleOptions, _pipelineRegistry.OrthographyRenderers.Select(m => new PipelineOptionItem(m.Id, m.Name, m.Description)));
    }

    private void RebuildPipelinePhonoItems()
    {
        var enabled = _project.Pipeline.EnabledPhonoPluginRules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = _pipelineRegistry.PhonoRules.Select(r =>
            new PipelinePhonoRuleItem(r.Id, r.Name, r.Description, enabled.Contains(r.Id), SyncPipelinePhonoFromUi));
        ReplaceCollection(PipelinePhonoRuleItems, items);
    }

    private void SyncPipelinePhonoFromUi()
    {
        _project.Pipeline.EnabledPhonoPluginRules = PipelinePhonoRuleItems.Where(i => i.IsEnabled).Select(i => i.Id).ToList();
        CommitAndRefresh("Toggled plugin phonology rule");
    }

    private void CommitAndRefresh(string reason)
    {
        PushCollectionsBackToProject();
        _history.PushSnapshot(_project);
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        RefreshDerivedViews();
        StatusMessage = reason;
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
        _project.WritingSystem.FeaturicRules = FeaturicRules.ToList();
        ProjectFactory.SyncLegacyLists(_project);
        ScheduleAutosave();
    }

    private void SyncCollectionsFromProject()
    {
        ReplaceCollection(Phonemes, _project.Phonemes);
        ReplaceCollection(Consonants, _project.Phonemes.Where(p => p.Type == "C"));
        ReplaceCollection(Vowels, _project.Phonemes.Where(p => p.Type == "V"));
        ReplaceCollection(SyllableTemplates, _project.SyllableTemplates);
        ReplaceCollection(PhonologyRules, _project.PhonologyRules);
        ReplaceCollection(Lexemes, _project.Lexemes);
        ReplaceCollection(LexemeIds, _project.Lexemes.OrderBy(l => l.Lemma).Select(l => l.Lemma));
        if (!LexemeIds.Contains(_selectedInspectorLexeme) && LexemeIds.Count > 0)
            _selectedInspectorLexeme = LexemeIds[0];

        ReplaceCollection(Affixes, _project.Affixes);
        ReplaceCollection(OrthographyRules, _project.OrthographyRules);
        ReplaceCollection(OrthographyExceptions, _project.OrthographyExceptions);
        ReplaceCollection(GlyphIds, _project.WritingSystem.Glyphs.Keys.OrderBy(k => k));
        if (!_project.WritingSystem.Glyphs.ContainsKey(_selectedGlyphId) && GlyphIds.Count > 0)
            SelectedGlyphId = GlyphIds[0];
        ReplaceCollection(FeaturicRules, _project.WritingSystem.FeaturicRules);
        RebuildPipelinePhonoItems();

        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(AllowedOnsetsText));
        OnPropertyChanged(nameof(AllowedCodasText));
        OnPropertyChanged(nameof(MaxOnsetCluster));
        OnPropertyChanged(nameof(MaxCodaCluster));
        OnPropertyChanged(nameof(ForbiddenPatternsText));
        OnPropertyChanged(nameof(SelectedStressPattern));
        OnPropertyChanged(nameof(SelectedWordOrder));
        OnPropertyChanged(nameof(SelectedClauseTemplate));
        OnPropertyChanged(nameof(SelectedCapitalization));
        OnPropertyChanged(nameof(SelectedFlowDirection));
        OnPropertyChanged(nameof(CursiveBonding));
        OnPropertyChanged(nameof(ContextualVariants));
        OnPropertyChanged(nameof(GlyphScale));
        OnPropertyChanged(nameof(LetterSpacing));
        OnPropertyChanged(nameof(WrapLimit));
        OnPropertyChanged(nameof(HyphenateSyllables));
        OnPropertyChanged(nameof(AllowDuplicates));
        OnPropertyChanged(nameof(SelectedSyntaxGeneratorId));
        OnPropertyChanged(nameof(SelectedMorphologyRealizerId));
        OnPropertyChanged(nameof(SelectedOrthographyRendererId));
        OnPropertyChanged(nameof(SelectedInspectorLexeme));
    }

    public void RefreshDerivedViews()
    {
        RunDerivationPreview();
        RefreshMorphPreview();
        RefreshSyntaxPreview();
        RefreshOrthPreview();
        RefreshWritingCanvas();
        RefreshInspectors();
        RefreshDiagnostics();
        if (GeneratedSentences.Count == 0) GenerateBatchSentences();
    }

    private void AddPhoneme(string kind)
    {
        var sym = NewPhonemeSymbol.Trim();
        if (string.IsNullOrEmpty(sym)) return;
        var existing = _project.Phonemes.FirstOrDefault(p => p.Symbol == sym && p.Type == kind);
        var created = ProjectFactory.CreatePhoneme(sym, kind, NewPhonemeWeight, NewAllowOnset, NewAllowCoda);
        if (existing is not null) created.Features = existing.Features;

        _project.Phonemes.RemoveAll(p => p.Symbol == sym && p.Type == kind);
        _project.Phonemes.Add(created);
        NewPhonemeSymbol = "";
        SyncCollectionsFromProject();
        CommitAndRefresh($"Added phoneme '{sym}'");
    }

    private void RemovePhoneme(Phoneme? p)
    {
        if (p is null) return;
        _project.Phonemes.Remove(p);
        SyncCollectionsFromProject();
        CommitAndRefresh($"Removed phoneme '{p.Symbol}'");
    }

    private void AddTemplate()
    {
        if (string.IsNullOrWhiteSpace(NewTemplatePattern)) return;
        SyllableTemplates.Add(new SyllableTemplate { Pattern = NewTemplatePattern.Trim(), Weight = Math.Max(1, NewTemplateWeight) });
        NewTemplatePattern = "";
        CommitAndRefresh("Added syllable template");
    }

    private void RemoveTemplate(SyllableTemplate? t)
    {
        if (t is null) return;
        SyllableTemplates.Remove(t);
        CommitAndRefresh("Removed syllable template");
    }

    private void AddPhonoRule()
    {
        if (string.IsNullOrWhiteSpace(NewRuleName)) return;
        PhonologyRules.Add(new PhonologyRuleSpec
        {
            Name = NewRuleName.Trim(),
            Target = string.IsNullOrWhiteSpace(NewRuleTarget) ? "∅" : NewRuleTarget.Trim(),
            Replacement = string.IsNullOrWhiteSpace(NewRuleReplacement) ? "∅" : NewRuleReplacement.Trim(),
            PrevPhone = string.IsNullOrWhiteSpace(NewRulePrev) ? null : NewRulePrev.Trim(),
            NextPhone = string.IsNullOrWhiteSpace(NewRuleNext) ? null : NewRuleNext.Trim(),
            Condition = NewRuleCond.Trim(),
            Enabled = true
        });
        CommitAndRefresh("Added phonological rule");
    }

    private void RemovePhonoRule(PhonologyRuleSpec? r)
    {
        var target = r ?? SelectedRule;
        if (target is null) return;
        PhonologyRules.Remove(target);
        CommitAndRefresh("Removed phonological rule");
    }

    private void MovePhonoRule(PhonologyRuleSpec? r, int delta)
    {
        var target = r ?? SelectedRule;
        if (target is null) return;
        int idx = PhonologyRules.IndexOf(target);
        int next = idx + delta;
        if (idx < 0 || next < 0 || next >= PhonologyRules.Count) return;
        PhonologyRules.Move(idx, next);
        SelectedRule = target;
        CommitAndRefresh("Reordered phonological rule");
    }

    private void RunDerivationPreview()
    {
        var ur = TestWordUr.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var res = PhonologyEngine.Derive(ur, _project, _engine.ResolvePluginRules(_project));
        var sylText = string.Join(".", res.Syllables.Select((s, idx) =>
            (idx == res.StressIndex ? "ˈ" : "") + string.Concat(s.Onset) + string.Concat(s.Nucleus) + string.Concat(s.Coda)));
        var lines = res.Steps.Select(s => $"{s.Label}: {s.Value}").ToList();
        lines.Add($"Syllables: {(string.IsNullOrEmpty(sylText) ? "(none)" : sylText)}");
        DerivationResult = string.Join(Environment.NewLine, lines);
    }

    private void AddLexeme()
    {
        var lemma = NewLexLemma.Trim();
        if (string.IsNullOrEmpty(lemma)) return;
        _project.Lexemes.RemoveAll(l => string.Equals(l.Lemma, lemma, StringComparison.OrdinalIgnoreCase));
        _project.Lexemes.Add(new LexemeEntry
        {
            Lemma = lemma,
            Pos = string.IsNullOrWhiteSpace(NewLexPos) ? "N" : NewLexPos.Trim(),
            UR = string.Join(" ", NewLexUr.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        });
        NewLexLemma = "";
        NewLexUr = "";
        SyncCollectionsFromProject();
        CommitAndRefresh($"Added lexeme '{lemma}'");
    }

    private void RemoveLexeme(LexemeEntry? l)
    {
        if (l is null) return;
        _project.Lexemes.Remove(l);
        SyncCollectionsFromProject();
        CommitAndRefresh($"Removed lexeme '{l.Lemma}'");
    }

    private void AddAffix()
    {
        if (string.IsNullOrWhiteSpace(NewAffixName)) return;
        Affixes.Add(new AffixEntry
        {
            Name = NewAffixName.Trim(),
            Pos = string.IsNullOrWhiteSpace(NewAffixPos) ? "N" : NewAffixPos.Trim(),
            FeatureName = NewAffixFeatName.Trim(),
            FeatureValue = NewAffixFeatVal.Trim(),
            Affix = NewAffixForm.Trim(),
            IsPrefix = NewAffixIsPrefix,
            Enabled = true
        });
        NewAffixName = "";
        CommitAndRefresh("Added affix rule");
    }

    private void RemoveAffix(AffixEntry? a)
    {
        if (a is null) return;
        Affixes.Remove(a);
        CommitAndRefresh("Removed affix rule");
    }

    private void RefreshMorphPreview()
    {
        var morph = _engine.ResolveMorphology(_project);
        IReadOnlyList<Feat> feats = string.IsNullOrWhiteSpace(MorphPreviewFeatName)
            ? Array.Empty<Feat>()
            : new[] { new Feat(MorphPreviewFeatName.Trim(), MorphPreviewFeatVal.Trim()) };
        MorphPreviewOutput = morph.RealizeForPreview(MorphPreviewLemma, MorphPreviewPos, feats, _project);
    }

    private void RefreshSyntaxPreview()
    {
        var syntax = _engine.ResolveSyntax(_project);
        var tree = syntax.Generate(new SentenceSpec(_project.ClauseTemplate.ToString(), SyntaxApproxWords, new Dictionary<string, string> { ["Tense"] = "Past" }), _project, new Random(SyntaxPreviewSeed));
        SyntaxLinearPreview = string.Join(" ", tree.Leaves().Select(l => $"{l.Lemma}/{l.Category}"));
        SyntaxTreeSummary = ConfigurableSyntaxGenerator.FormatSyntaxTree(tree.Root, 0, false);
    }

    private void AddOrthRule()
    {
        if (string.IsNullOrWhiteSpace(NewOrthPhone)) return;
        OrthographyRules.Add(new OrthRuleEntry
        {
            Phone = NewOrthPhone.Trim(),
            Grapheme = NewOrthGrapheme,
            PrevContext = string.IsNullOrWhiteSpace(NewOrthPrev) ? null : NewOrthPrev.Trim(),
            NextContext = string.IsNullOrWhiteSpace(NewOrthNext) ? null : NewOrthNext.Trim(),
            Enabled = true
        });
        NewOrthPhone = "";
        NewOrthGrapheme = "";
        NewOrthPrev = "";
        NewOrthNext = "";
        CommitAndRefresh("Added orthography mapping");
    }

    private void RemoveOrthRule(OrthRuleEntry? r)
    {
        if (r is null) return;
        OrthographyRules.Remove(r);
        CommitAndRefresh("Removed orthography mapping");
    }

    private void AddOrthException()
    {
        if (string.IsNullOrWhiteSpace(NewExInput)) return;
        OrthographyExceptions.Add(new OrthExceptionEntry { Input = NewExInput.Trim(), Output = NewExOutput.Trim() });
        NewExInput = "";
        NewExOutput = "";
        CommitAndRefresh("Added orthography exception");
    }

    private void RemoveOrthException(OrthExceptionEntry? e)
    {
        if (e is null) return;
        OrthographyExceptions.Remove(e);
        CommitAndRefresh("Removed orthography exception");
    }

    private void RefreshOrthPreview()
    {
        var orth = _engine.ResolveOrthography(_project);
        var words = OrthPreviewInput
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => (IReadOnlyList<string>)w.Select(c => c.ToString()).ToList())
            .ToList();
        var res = orth.Render(words, _project);
        OrthPreviewOutput = res.Text;
        OrthPreviewTokens = "Layer 1 Tokens: " + string.Join(" ", res.Tokens.Select(t => $"[{t.Id}:{t.PositionInWord}]"));
    }

    private void ApplyWritingPreset(string key)
    {
        _project.WritingSystem = WritingSystemEngine.CreatePreset(key);
        SyncCollectionsFromProject();
        CommitAndRefresh($"Applied writing preset: {key}");
    }

    private void AddCustomGlyph()
    {
        var id = NewGlyphId.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(id) || _project.WritingSystem.Glyphs.ContainsKey(id)) return;
        bool isVert = _project.WritingSystem.FlowDirection is FlowDirection.TTB_LTR or FlowDirection.TTB_RTL;
        _project.WritingSystem.Glyphs[id] = new GlyphDefinition
        {
            Id = id,
            Label = id,
            SvgPath = isVert ? "M 50 10 L 50 38 L 72 52 L 50 66 L 50 90" : "M 15 55 L 40 30 L 65 70 L 85 55",
            EnteringAnchor = isVert ? new(50, 10) : new(15, 55),
            ExitingAnchor = isVert ? new(50, 90) : new(85, 55),
            AdvanceWidth = 74,
            AdvanceHeight = 80
        };
        NewGlyphId = "";
        ReplaceCollection(GlyphIds, _project.WritingSystem.Glyphs.Keys.OrderBy(k => k));
        SelectedGlyphId = id;
        CommitAndRefresh($"Added glyph '{id}'");
    }

    private void AddFeaturicRule()
    {
        if (string.IsNullOrWhiteSpace(NewFeaturicName) || string.IsNullOrWhiteSpace(NewFeaturicPath)) return;
        FeaturicRules.Add(new FeaturicAssemblyRule
        {
            Id = $"rule-{DateTime.UtcNow.Ticks}",
            Name = NewFeaturicName.Trim(),
            TargetGlyphId = string.IsNullOrWhiteSpace(NewFeaturicTarget) ? "*" : NewFeaturicTarget.Trim(),
            ContextPosition = NewFeaturicContext,
            AppendSvgPath = NewFeaturicPath.Trim(),
            Enabled = true
        });
        CommitAndRefresh("Added featuric assembly rule");
    }

    private void RemoveFeaturicRule(FeaturicAssemblyRule? r)
    {
        if (r is null) return;
        FeaturicRules.Remove(r);
        CommitAndRefresh("Removed featuric assembly rule");
    }

    public IReadOnlyList<GraphemeToken> BuildCurrentLayer1Tokens(out string sourceLabel)
    {
        if (UseEngineLayer1)
        {
            var res = _engine.GenerateSentence(_project, WsEngineSeed, 4, new Dictionary<string, string> { ["Tense"] = "Past" });
            sourceLabel = res.Written;
            return res.Tokens;
        }
        sourceLabel = ShapingInput;
        return WritingSystemEngine.TokenizeRawInput(ShapingInput, _project.WritingSystem.Glyphs.Keys);
    }

    private void RefreshWritingCanvas()
    {
        var tokens = BuildCurrentLayer1Tokens(out var sourceLabel);
        Layer1SourceDisplay = sourceLabel;
        Layer1TokensSummary = string.Join("  ", tokens.Select(t => $"{t.Id}:{(t.PositionInWord.Length > 0 ? t.PositionInWord[0] : 'i')}"));

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
                TokenLabel = g.Token.Id,
                Variant = g.VariantUsed,
                EnterX = g.EnteringWorld.X,
                EnterY = g.EnteringWorld.Y,
                ExitX = g.ExitingWorld.X,
                ExitY = g.ExitingWorld.Y
            });
        }

        ReplaceCollection(ShapingTrace, result.Trace);
    }

    private void GenerateBatchSentences()
    {
        PushCollectionsBackToProject();
        var constraints = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(ConstraintFeatName) && !string.IsNullOrWhiteSpace(ConstraintFeatVal))
            constraints[ConstraintFeatName.Trim()] = ConstraintFeatVal.Trim();

        int baseSeed = int.TryParse(OutputSeedText, out var parsed) ? parsed : Environment.TickCount;
        var list = new List<string>(SampleCount);
        for (int i = 0; i < SampleCount; i++)
        {
            var res = _engine.GenerateSentence(_project, baseSeed + i, OutputApproxWords, constraints);
            list.Add(res.Written);
        }

        ReplaceCollection(GeneratedSentences, list);
        BatchOutputText = string.Join(Environment.NewLine, list.Select((s, idx) => $"{idx + 1}. {s}"));
        StatusMessage = $"Generated {list.Count} sentence(s)";
    }

    private void GenerateWords()
    {
        PushCollectionsBackToProject();
        int seed = int.TryParse(OutputSeedText, out var parsed) ? parsed : Environment.TickCount;
        var words = WordGenerator.GenerateWords(_project, WordCount, SyllablesPerWord, seed, _pipelineRegistry);
        GeneratedWordsText = string.Join(Environment.NewLine, words);
        StatusMessage = $"Generated {words.Count} candidate word(s)";
    }

    private IReadOnlyList<string> RealizeSingleWord(IMorphologyRealizer morph, string lemma, string pos, IReadOnlyList<Feat> features) =>
        morph.RealizeWords(new SyntaxTree(new SyntaxNode(pos, lemma, features)), _project).FirstOrDefault()
        ?? Array.Empty<string>();

    private void RefreshInspectors()
    {
        var lex = _project.Lexemes.FirstOrDefault(l => string.Equals(l.Lemma, SelectedInspectorLexeme, StringComparison.OrdinalIgnoreCase))
            ?? new LexemeEntry { Lemma = SelectedInspectorLexeme, Pos = "N", UR = SelectedInspectorLexeme };

        var morph = _engine.ResolveMorphology(_project);
        var orth = _engine.ResolveOrthography(_project);
        var pluginRules = _engine.ResolvePluginRules(_project);

        var presPhones = RealizeSingleWord(morph, lex.Lemma, lex.Pos, Array.Empty<Feat>());
        var pastPhones = RealizeSingleWord(morph, lex.Lemma, lex.Pos, new[] { new Feat("Tense", "Past") });
        var plurPhones = RealizeSingleWord(morph, lex.Lemma, lex.Pos, new[] { new Feat("Number", "Pl") });

        var lines = new List<string>
        {
            $"Lemma: {lex.Lemma}",
            $"POS: {lex.Pos}",
            $"UR: {lex.UR}",
            $"SR(Pres): {string.Join(" ", presPhones)}",
            $"SR(Past): {string.Join(" ", pastPhones)}",
            $"SR(Plural): {string.Join(" ", plurPhones)}",
            $"Orth(Pres): {orth.Render(new[] { PhonologyEngine.Derive(presPhones, _project, pluginRules).SurfacePhones }, _project).Text}",
            "",
            "-- Enabled Affix Paradigm --"
        };

        foreach (var afx in _project.Affixes.Where(a => a.Enabled && string.Equals(a.Pos, lex.Pos, StringComparison.OrdinalIgnoreCase)))
        {
            var inflected = RealizeSingleWord(morph, lex.Lemma, lex.Pos, new[] { new Feat(afx.FeatureName, afx.FeatureValue) });
            var derivedForm = PhonologyEngine.Derive(inflected, _project, pluginRules);
            var writtenForm = orth.Render(new[] { derivedForm.SurfacePhones }, _project).Text;
            lines.Add($"{afx.Name} [{afx.FeatureName}={afx.FeatureValue}]: {string.Join(" ", inflected)} -> {writtenForm}");
        }
        InspectorLexemeText = string.Join(Environment.NewLine, lines);

        var syntax = _engine.ResolveSyntax(_project);
        var tree = syntax.Generate(new SentenceSpec(_project.ClauseTemplate.ToString(), 5, new Dictionary<string, string> { ["Tense"] = "Past" }), _project, new Random(3));
        InspectorSyntaxTreeText = ConfigurableSyntaxGenerator.FormatSyntaxTree(tree.Root, 0, true);

        var urPhones = MorphologyEngine.SplitPhones(lex.UR);
        var derived = PhonologyEngine.Derive(urPhones, _project, pluginRules);
        var syls = string.Join(" . ", derived.Syllables.Select((s, idx) =>
            $"{(idx == derived.StressIndex ? "ˈ" : "")}[{string.Concat(s.Onset)}|{string.Concat(s.Nucleus)}|{string.Concat(s.Coda)}]"));
        InspectorPhonologyTraceText = string.Join(Environment.NewLine, derived.Steps.Select(s => $"{s.Label}: {s.Value}").Append($"Syllabified: {syls}"));
    }

    private void RefreshDiagnostics()
    {
        var issues = ProjectValidator.Validate(_project);
        ReplaceCollection(Diagnostics, issues);
        _errorCount = issues.Count(i => i.Severity == DiagnosticSeverity.Error);
        _warningCount = issues.Count(i => i.Severity == DiagnosticSeverity.Warning);
        OnPropertyChanged(nameof(DiagnosticsSummary));
    }

    private void LoadPluginsCatalog()
    {
        var discovered = new PluginService().DiscoverPlugins();
        ReplaceCollection(Plugins, discovered);
        var acts = discovered.SelectMany(p => p.Actions).ToList();
        ReplaceCollection(Actions, acts);
        if (SelectedPlugin is null && Plugins.Count > 0) SelectedPlugin = Plugins[0];
        if (SelectedAction is null && Actions.Count > 0) SelectedAction = Actions[0];

        if (!_isDesignMode)
        {
            foreach (var dir in PluginService.GetPluginSearchPaths())
                _pipelineRegistry.LoadExtensionsFromDirectory(dir);
            PopulatePipelineOptions();
            RebuildPipelinePhonoItems();
        }
        AppendPluginLog($"Discovered {Plugins.Count} plugin(s), {Actions.Count} action(s), and {_pipelineRegistry.PhonoRules.Count} pipeline phonology rule(s).");
    }

    private async Task RunPluginActionAsync(DiscoveredAction? action)
    {
        var act = action ?? SelectedAction;
        if (act is null) return;
        var plugin = Plugins.FirstOrDefault(p => string.Equals(p.Manifest.Id, act.PluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin is null) return;

        PushCollectionsBackToProject();
        var argDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ActionArgs) argDict[row.Key] = row.Value;

        StatusMessage = $"Running {act.Title}...";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var (res, files, logLine) = await PluginService.ExecutePluginActionAsync(plugin, act, _project, argDict, cts.Token);

        AppendPluginLog(logLine);
        foreach (var f in files) PluginOutputs.Insert(0, f);
        if (files.Count > 0) SelectedPluginOutput = files[0];
        StatusMessage = res.Message;
    }

    private void AppendPluginLog(string line)
    {
        var entry = $"{DateTime.Now:HH:mm:ss} {line}";
        PluginLogsText = string.IsNullOrEmpty(PluginLogsText) ? entry : $"{PluginLogsText}{Environment.NewLine}{entry}";
    }

    public async Task SaveBackupAndRecordRecentAsync(string filePath, string reason)
    {
        var recents = await ProjectPersistence.RecordRecentProjectAsync(filePath, _project);
        var backups = await ProjectPersistence.SaveVersionedBackupAsync(_project, reason);
        ReplaceCollection(RecentProjects, recents);
        ReplaceCollection(BackupSnapshots, backups);
    }

    private async Task ResetProjectAsync()
    {
        var backups = await ProjectPersistence.SaveVersionedBackupAsync(_project, "before-reset");
        ReplaceCollection(BackupSnapshots, backups);
        LoadProject(ProjectFactory.CreateStarterProject(), "(unsaved)", "");
        StatusMessage = "Reset project to starter defaults";
    }

    private async Task OpenRecentAsync(RecentProjectRecord? rec)
    {
        if (rec is null || !File.Exists(rec.FilePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(rec.FilePath);
            LoadProject(ProjectFactory.FromJson(json), rec.FileName, rec.FilePath);
            StatusMessage = $"Opened {rec.FileName}";
        }
        catch
        {
            StatusMessage = $"Failed to open {rec.FileName}";
        }
    }

    private async Task RestoreBackupAsync(BackupSnapshotRecord? snap)
    {
        if (snap is null || !File.Exists(snap.FilePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(snap.FilePath);
            LoadProject(ProjectFactory.FromJson(json), CurrentFileName, CurrentFilePath);
            StatusMessage = $"Restored backup {snap.Label}";
        }
        catch
        {
            StatusMessage = "Failed to restore backup";
        }
    }

    private void Undo()
    {
        var prev = _history.Undo(_project);
        if (prev is null) return;
        LoadProject(prev, CurrentFileName, CurrentFilePath, recordHistory: false);
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        StatusMessage = "Undo";
    }

    private void Redo()
    {
        var next = _history.Redo();
        if (next is null) return;
        LoadProject(next, CurrentFileName, CurrentFilePath, recordHistory: false);
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        StatusMessage = "Redo";
    }

    private void ScheduleAutosave()
    {
        if (_isDesignMode) return;
        _autosaveCts?.Cancel();
        _autosaveCts?.Dispose();
        _autosaveCts = new CancellationTokenSource();
        var token = _autosaveCts.Token;
        _ = DebouncedAutosaveAsync(token);
    }

    private async Task DebouncedAutosaveAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(350, ct);
            await ProjectPersistence.SaveAutosaveAsync(_project, ct);
            AutosaveTime = $"Autosaved {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException)
        {
            // Superseded by newer edit
        }
        catch
        {
            // Keep UI resilient
        }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }
}
