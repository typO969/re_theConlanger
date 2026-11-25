using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Conlanger.Desktop.ViewModels;
using Microsoft.Win32;

namespace Conlanger.Desktop.Views;

public partial class OutputTab : UserControl
{
    public OutputTab() => InitializeComponent();

    private async void OnExportTxtClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var dlg = new SaveFileDialog
        {
            Filter = "Text File (*.txt)|*.txt",
            FileName = $"{vm.ProjectName}-corpus.txt"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var body = string.Join(Environment.NewLine, vm.GeneratedSentences);
            await File.WriteAllTextAsync(dlg.FileName, body);
            vm.StatusMessage = $"Exported TXT corpus to {Path.GetFileName(dlg.FileName)}";
        }
        catch
        {
            vm.StatusMessage = "Failed to export TXT corpus";
        }
    }

    private async void OnExportCsvClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var dlg = new SaveFileDialog
        {
            Filter = "CSV File (*.csv)|*.csv",
            FileName = $"{vm.ProjectName}-corpus.csv"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Index,Sentence");
            for (int i = 0; i < vm.GeneratedSentences.Count; i++)
                sb.AppendLine($"{i + 1},\"{vm.GeneratedSentences[i].Replace("\"", "\"\"")}\"");
            await File.WriteAllTextAsync(dlg.FileName, sb.ToString());
            vm.StatusMessage = $"Exported CSV corpus to {Path.GetFileName(dlg.FileName)}";
        }
        catch
        {
            vm.StatusMessage = "Failed to export CSV corpus";
        }
    }
}
