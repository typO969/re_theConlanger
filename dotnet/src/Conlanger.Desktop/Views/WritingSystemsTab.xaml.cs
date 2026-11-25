using System.IO;
using System.Windows;
using System.Windows.Controls;
using Conlanger.Core;
using Conlanger.Desktop.ViewModels;
using Microsoft.Win32;

namespace Conlanger.Desktop.Views;

public partial class WritingSystemsTab : UserControl
{
    public WritingSystemsTab() => InitializeComponent();

    private async void OnExportSvgClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var tokens = vm.BuildCurrentLayer1Tokens(out _);
        var shaped = WritingSystemEngine.RunShapingGraph(tokens, vm.Project.WritingSystem);
        var svg = WritingSystemEngine.ExportShapedSvg(shaped, vm.Project.WritingSystem);

        var dlg = new SaveFileDialog
        {
            Filter = "Scalable Vector Graphics (*.svg)|*.svg",
            FileName = $"{vm.ProjectName}-script.svg"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            await File.WriteAllTextAsync(dlg.FileName, svg);
            vm.StatusMessage = $"Exported SVG to {Path.GetFileName(dlg.FileName)}";
        }
        catch
        {
            vm.StatusMessage = "Failed to export SVG";
        }
    }
}
