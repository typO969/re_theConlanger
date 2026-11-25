using System.IO;
using System.Windows;
using Conlanger.Core;
using Conlanger.Desktop.Services;
using Conlanger.Desktop.ViewModels;
using Microsoft.Win32;

namespace Conlanger.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnOpenProjectClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Conlanger Project (*.clproj.json;*.json)|*.clproj.json;*.json|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var json = await File.ReadAllTextAsync(dlg.FileName);
            var loaded = ProjectFactory.FromJson(json);
            vm.LoadProject(loaded, Path.GetFileName(dlg.FileName), dlg.FileName);
            await vm.SaveBackupAndRecordRecentAsync(dlg.FileName, "opened");
            vm.StatusMessage = $"Loaded {Path.GetFileName(dlg.FileName)}";
        }
        catch
        {
            vm.StatusMessage = $"Failed to load {Path.GetFileName(dlg.FileName)}";
        }
    }

    private async void OnSaveProjectClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (!string.IsNullOrWhiteSpace(vm.CurrentFilePath) && File.Exists(vm.CurrentFilePath))
        {
            await SaveProjectToPathAsync(vm, vm.CurrentFilePath);
            return;
        }
        await PromptSaveAsAsync(vm);
    }

    private async void OnSaveAsProjectClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        await PromptSaveAsAsync(vm);
    }

    private async Task PromptSaveAsAsync(MainViewModel vm)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Conlanger Project (*.clproj.json)|*.clproj.json|JSON Files (*.json)|*.json",
            FileName = $"{vm.ProjectName}.clproj.json"
        };
        if (dlg.ShowDialog(this) == true)
            await SaveProjectToPathAsync(vm, dlg.FileName);
    }

    private static async Task SaveProjectToPathAsync(MainViewModel vm, string filePath)
    {
        try
        {
            vm.PushCollectionsBackToProject();
            await File.WriteAllTextAsync(filePath, ProjectFactory.ToJson(vm.Project));
            vm.CurrentFileName = Path.GetFileName(filePath);
            vm.CurrentFilePath = filePath;
            await vm.SaveBackupAndRecordRecentAsync(filePath, "manual-save");
            vm.StatusMessage = $"Saved {vm.CurrentFileName}";
        }
        catch
        {
            vm.StatusMessage = "Failed to save project file";
        }
    }

    private async void OnImportLexiconClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Lexicon Files (*.csv;*.json)|*.csv;*.json|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var text = await File.ReadAllTextAsync(dlg.FileName);
            var entries = ProjectPersistence.ImportLexiconFromText(dlg.FileName, text);
            if (entries.Count == 0)
            {
                vm.StatusMessage = "No valid lexeme entries found in file";
                return;
            }

            vm.PushCollectionsBackToProject();
            foreach (var entry in entries)
            {
                vm.Project.Lexemes.RemoveAll(l => string.Equals(l.Lemma, entry.Lemma, StringComparison.OrdinalIgnoreCase));
                vm.Project.Lexemes.Add(entry);
            }
            vm.LoadProject(vm.Project, vm.CurrentFileName, vm.CurrentFilePath);
            vm.StatusMessage = $"Imported {entries.Count} lexeme(s)";
        }
        catch
        {
            vm.StatusMessage = "Failed to import lexicon";
        }
    }

    private async void OnExportLexiconCsvClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        vm.PushCollectionsBackToProject();
        var dlg = new SaveFileDialog
        {
            Filter = "CSV File (*.csv)|*.csv",
            FileName = $"{vm.ProjectName}-lexicon.csv"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            await File.WriteAllTextAsync(dlg.FileName, ProjectPersistence.ExportLexiconCsv(vm.Project.Lexemes));
            vm.StatusMessage = $"Exported CSV lexicon to {Path.GetFileName(dlg.FileName)}";
        }
        catch
        {
            vm.StatusMessage = "Failed to export CSV lexicon";
        }
    }

    private async void OnExportLexiconJsonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        vm.PushCollectionsBackToProject();
        var dlg = new SaveFileDialog
        {
            Filter = "JSON File (*.json)|*.json",
            FileName = $"{vm.ProjectName}-lexicon.json"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            await File.WriteAllTextAsync(dlg.FileName, ProjectPersistence.ExportLexiconJson(vm.Project.Lexemes));
            vm.StatusMessage = $"Exported JSON lexicon to {Path.GetFileName(dlg.FileName)}";
        }
        catch
        {
            vm.StatusMessage = "Failed to export JSON lexicon";
        }
    }
}
