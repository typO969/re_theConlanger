using System.IO;
using System.Windows;
using System.Windows.Controls;
using Conlanger.Desktop.ViewModels;
using Microsoft.Win32;

namespace Conlanger.Desktop.Views;

public partial class ExtensionsTab : UserControl
{
    public ExtensionsTab() => InitializeComponent();

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
