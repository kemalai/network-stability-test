using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InternetMonitor.Core.Services;
using InternetMonitor.UI.ViewModels;
using InternetMonitor.UI.Views;

namespace InternetMonitor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.StartMonitoringCommand.ExecuteAsync(null);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.Dispose();
        }
    }

    private void DeviceGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is NetworkDevice device && DataContext is MainViewModel viewModel)
        {
            viewModel.ShowDeviceDetailsCommand.Execute(device);
        }
    }
}
