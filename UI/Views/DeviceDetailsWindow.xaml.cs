using System.Windows;
using InternetMonitor.Core.Services;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace InternetMonitor.UI.Views;

public partial class DeviceDetailsWindow : Window
{
    private readonly NetworkDevice _device;
    private readonly NetworkScannerService _scannerService;

    public DeviceDetailsWindow(NetworkDevice device, NetworkScannerService scannerService)
    {
        InitializeComponent();
        _device = device;
        _scannerService = scannerService;

        LoadDeviceInfo();
    }

    private void LoadDeviceInfo()
    {
        // Header
        DeviceTypeText.Text = _device.DeviceType;
        DeviceNameText.Text = _device.DisplayName;

        // Status
        if (_device.IsBlocked)
        {
            StatusBorder.Background = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#F44336"));
            StatusText.Text = "ENGELLENDI";
            StatusText.Foreground = WpfBrushes.White;
            BlockButton.Content = "Engeli Kaldir";
            BlockButton.Background = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#4CAF50"));
        }
        else if (_device.IsOnline)
        {
            StatusBorder.Background = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#4CAF50"));
            StatusText.Text = "CEVRIMICI";
            StatusText.Foreground = WpfBrushes.White;
            BlockButton.Content = "Baglantiyi Kes";
            BlockButton.Background = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#F44336"));
        }
        else
        {
            StatusBorder.Background = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#888888"));
            StatusText.Text = "CEVRIMDISI";
            StatusText.Foreground = WpfBrushes.White;
            BlockButton.Content = "Baglantiyi Kes";
            BlockButton.Background = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#F44336"));
        }

        // Details
        IpAddressText.Text = _device.IpAddress;
        MacAddressText.Text = _device.MacAddress;
        HostnameText.Text = string.IsNullOrEmpty(_device.Hostname) ? "Bilinmiyor" : _device.Hostname;
        VendorText.Text = _device.Vendor;
        PingText.Text = _device.PingText;
        FirstSeenText.Text = _device.FirstSeenText;
        LastSeenText.Text = _device.LastSeenText;
        OnlineDurationText.Text = _device.OnlineDuration;
        ConnectionsText.Text = _device.ConnectionsText;

        // Hide block button for gateway
        if (_device.IsGateway)
        {
            BlockButton.Visibility = Visibility.Collapsed;
        }
    }

    private async void BlockButton_Click(object sender, RoutedEventArgs e)
    {
        BlockButton.IsEnabled = false;

        if (_device.IsBlocked)
        {
            await _scannerService.UnblockDeviceAsync(_device.IpAddress);
        }
        else
        {
            var result = System.Windows.MessageBox.Show(
                $"{_device.DisplayName} ({_device.IpAddress}) cihazinin internet baglantisi kesilecek.\n\nDevam etmek istiyor musunuz?",
                "Baglanti Kesme Onay",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _scannerService.BlockDeviceAsync(_device.IpAddress);
            }
        }

        LoadDeviceInfo();
        BlockButton.IsEnabled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
