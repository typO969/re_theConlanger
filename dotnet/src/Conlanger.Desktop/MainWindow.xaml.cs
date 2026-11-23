using System.IO;
using System.Windows;
using Conlanger.Core;
using Conlanger.Desktop.ViewModels;
using Microsoft.Win32;

namespace Conlanger.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnOpenProjectClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Conlanger Project (*.clproj.json;*.json)|*.clproj.json;*.json|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) == true)
        {
            vm.CommitSnapshot();
            vm.Project = ProjectFactory.FromJson(File.ReadAllText(dlg.FileName));
            vm.StatusMessage = $"Loaded {Path.GetFileName(dlg.FileName)}";
        }
    }

    private void OnSaveProjectClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        vm.PushCollectionsBackToProject();
        var dlg = new SaveFileDialog
        {
            Filter = "Conlanger Project (*.clproj.json)|*.clproj.json",
            FileName = $"{vm.ProjectName}.clproj.json"
        };
        if (dlg.ShowDialog(this) == true)
        {
            File.WriteAllText(dlg.FileName, ProjectFactory.ToJson(vm.Project));
            vm.StatusMessage = $"Saved {Path.GetFileName(dlg.FileName)}";
        }
    }
}
