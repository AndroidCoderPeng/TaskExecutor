using System.Windows;
using System.Windows.Controls;
using TaskExecutor.ViewModels;

namespace TaskExecutor.Views;

public partial class ImportExportView : UserControl
{
    public ImportExportView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImportExportViewModel viewModel)
        {
            viewModel.StartDateRefresh();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImportExportViewModel viewModel)
        {
            viewModel.StopDateRefresh();
        }
    }
}