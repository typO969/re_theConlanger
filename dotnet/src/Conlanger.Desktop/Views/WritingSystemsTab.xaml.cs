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

    private void OnExportSvgClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var tokens = WritingSystemEngine.TokenizeRawInput(vm.ShapingInput, vm.Project.WritingSystem.Glyphs.Keys);
        var shaped = WritingSystemEngine.RunShapingGraph(tokens, vm.Project.WritingSystem);
        var svg = WritingSystemEngine.ExportShapedSvg(shaped, vm.Project.WritingSystem);

        var dlg = new SaveFileDialog
        {
            Filter = "Scalable Vector Graphics (*.svg)|*.svg",
            FileName = $"{vm.ProjectName}-script.svg"
        };
        if (dlg.ShowDialog() == true)
        {
            File.WriteAllText(dlg.FileName, svg);
            vm.StatusMessage = $"Exported SVG to {Path.GetFileName(dlg.FileName)}";
        }
    }
}
