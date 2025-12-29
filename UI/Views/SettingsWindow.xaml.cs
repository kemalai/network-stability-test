using System.Windows;
using InternetMonitor.UI.ViewModels;
using WpfButton = System.Windows.Controls.Button;

namespace InternetMonitor.UI.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void QuickInterval_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton button && button.Tag != null)
        {
            if (double.TryParse(button.Tag.ToString(), out double seconds))
            {
                if (DataContext is SettingsViewModel viewModel)
                {
                    viewModel.PingIntervalSeconds = seconds;
                }
            }
        }
    }
}
