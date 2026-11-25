using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Conlanger.Desktop.Services;
using Conlanger.Desktop.ViewModels;
using Microsoft.Win32;

namespace Conlanger.Desktop.Views;

public partial class ExtensionsTab : UserControl
{
    public ExtensionsTab() => InitializeComponent();

    private async void OnBrowsePluginFolderClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var dlg = new OpenFolderDialog
        {
            Title = "Select Custom Conlanger Plugin Folder",
            Multiselect = false
        };
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
            await vm.AddCustomPluginFolderAsync(dlg.FolderName);
    }

    private void OnOpenUserPluginsDirClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ProjectPersistence.UserPluginsDir);
            Process.Start(new ProcessStartInfo
            {
                FileName = ProjectPersistence.UserPluginsDir,
                UseShellExecute = true
            });
        }
        catch
        {
            if (DataContext is MainViewModel vm)
                vm.StatusMessage = $"Plugins folder: {ProjectPersistence.UserPluginsDir}";
        }
    }

    private async void OnSavePluginOutputClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedPluginOutput is null) return;
        var dlg = new SaveFileDialog
        {
            Filter = "All Files (*.*)|*.*",
            FileName = vm.SelectedPluginOutput.FileName
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            await File.WriteAllTextAsync(dlg.FileName, vm.SelectedPluginOutput.Content);
            vm.StatusMessage = $"Saved plugin output {Path.GetFileName(dlg.FileName)}";
        }
        catch
        {
            vm.StatusMessage = "Failed to save plugin output";
        }
    }
}
