using System;
using System.Linq;
using System.Windows;
using Jung.Gpredict.ViewModels;

namespace Jung.Gpredict;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var firstFile = files.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstFile))
        {
            ViewModel.LoadTleFile(firstFile);
        }
    }
}
