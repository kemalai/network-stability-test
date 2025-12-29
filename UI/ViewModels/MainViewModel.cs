using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Measure;
using SkiaSharp;
using InternetMonitor.Core.Interfaces;
using InternetMonitor.Core.Models;
using InternetMonitor.Core.Services;
using InternetMonitor.Data;
using InternetMonitor.Infrastructure.Configuration;
using InternetMonitor.Infrastructure.Logging;
using InternetMonitor.Infrastructure.Notifications;
using InternetMonitor.UI.Views;
using InternetMonitor.Infrastructure.Localization;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace InternetMonitor.UI.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    // Static localization property for XAML bindings in TabItem headers
    public static LocalizationService Localization => LocalizationService.Instance;
    private readonly IMonitorEngine _monitorEngine;
    private readonly DataService _dataService;
    private readonly NotificationService _notificationService;
    private readonly ExportService _exportService;
    private readonly NetworkUsageService _networkUsageService;
    private readonly SpeedTestService _speedTestService;
    private readonly WebsiteMonitorService _websiteMonitorService;
    private readonly NetworkScannerService _networkScannerService;
    private readonly CacheCleanerService _cacheCleanerService;
    private readonly AppSettings _settings;
    private readonly Logger _logger;
    private bool _isDisposed;
    private ConnectionState _previousState = ConnectionState.Unknown;

    // Observable properties
    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Unknown;

    [ObservableProperty]
    private string _connectionStatusText = string.Empty;

    [ObservableProperty]
    private long _currentPing;

    [ObservableProperty]
    private long _averagePing;

    [ObservableProperty]
    private long _minPing;

    [ObservableProperty]
    private long _maxPing;

    [ObservableProperty]
    private double _jitter;

    [ObservableProperty]
    private double _packetLoss;

    [ObservableProperty]
    private string _uptimeText = "00:00:00";

    [ObservableProperty]
    private string _downtimeText = "00:00:00";

    [ObservableProperty]
    private double _uptimePercent = 100;

    [ObservableProperty]
    private int _totalPings;

    [ObservableProperty]
    private int _failedPings;

    [ObservableProperty]
    private string _lastEventText = "-";

    // Network Usage properties
    [ObservableProperty]
    private string _totalDownloadSpeed = "0 B/s";

    [ObservableProperty]
    private string _totalUploadSpeed = "0 B/s";

    [ObservableProperty]
    private int _activeConnectionCount;

    [ObservableProperty]
    private int _processCount;

    public ObservableCollection<NetworkProcessInfo> NetworkProcesses => _networkUsageService.NetworkProcesses;

    // Speed Test properties
    [ObservableProperty]
    private string _downloadSpeedText = "-";

    [ObservableProperty]
    private string _uploadSpeedText = "-";

    [ObservableProperty]
    private int _speedTestProgress;

    [ObservableProperty]
    private string _speedTestStatus = string.Empty;

    [ObservableProperty]
    private string _speedTestButtonText = string.Empty;

    [ObservableProperty]
    private string _downloadServerInfo = "-";

    [ObservableProperty]
    private string _uploadServerInfo = "-";

    // Speed test server selection
    public ObservableCollection<SpeedTestServer> AvailableServers => _speedTestService.AvailableServers;

    public SpeedTestServer? SelectedSpeedTestServer
    {
        get => _speedTestService.SelectedServer;
        set
        {
            if (_speedTestService.SelectedServer != value)
            {
                _speedTestService.SelectedServer = value;
                OnPropertyChanged(nameof(SelectedSpeedTestServer));
            }
        }
    }

    // Selected device for details
    [ObservableProperty]
    private NetworkDevice? _selectedDevice;

    // Speed test chart
    public ObservableCollection<SpeedDataPoint> SpeedHistory => _speedTestService.SpeedHistory;

    private readonly ObservableCollection<ObservablePoint> _downloadSpeedData = new();
    private readonly ObservableCollection<ObservablePoint> _uploadSpeedData = new();
    private int _speedDataIndex = 0;

    public ObservableCollection<ISeries> DownloadSpeedSeries { get; }
    public ObservableCollection<ISeries> UploadSpeedSeries { get; }

    public Axis[] SpeedTestXAxes { get; } = {
        new Axis
        {
            Name = "",
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            ShowSeparatorLines = false,
            IsVisible = false
        }
    };

    public Axis[] DownloadYAxes { get; } = {
        new Axis
        {
            Name = "Mbps",
            NamePaint = new SolidColorPaint(SKColor.Parse("#4CAF50")),
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0
        }
    };

    public Axis[] UploadYAxes { get; } = {
        new Axis
        {
            Name = "Mbps",
            NamePaint = new SolidColorPaint(SKColor.Parse("#2196F3")),
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0
        }
    };

    // Website Monitor properties
    public ObservableCollection<WebsiteStatus> Websites => _websiteMonitorService.Websites;

    public int WebsiteCount => Websites.Count;
    public int WebsitesOnlineCount => Websites.Count(w => w.IsOnline);
    public int WebsitesOfflineCount => Websites.Count(w => !w.IsOnline && w.Status != "Bekleniyor");

    // Network Scanner properties
    public ObservableCollection<NetworkDevice> NetworkDevices => _networkScannerService.Devices;

    [ObservableProperty]
    private string _localIpAddress = "-";

    [ObservableProperty]
    private string _gatewayAddress = "-";

    [ObservableProperty]
    private string _networkName = "-";

    [ObservableProperty]
    private int _networkDeviceCount;

    [ObservableProperty]
    private int _networkScanProgress;

    [ObservableProperty]
    private string _networkScanStatus = string.Empty;

    [ObservableProperty]
    private string _scanButtonText = string.Empty;

    // Cache Cleaner properties
    [ObservableProperty]
    private string _cacheCleanStatus = "";

    [ObservableProperty]
    private bool _isCleaningCache;

    // Chart data
    private readonly ObservableCollection<DateTimePoint> _pingData = new();
    private readonly ObservableCollection<DateTimePoint> _packetLossData = new();

    public ObservableCollection<ISeries> PingSeries { get; }
    public ObservableCollection<ISeries> PacketLossSeries { get; }

    public Axis[] XAxes { get; } = {
        new DateTimeAxis(TimeSpan.FromSeconds(30), date => date.ToString("HH:mm:ss"))
        {
            Name = "Zaman",
            NamePaint = new SolidColorPaint(SKColors.Gray),
            LabelsPaint = new SolidColorPaint(SKColors.Gray)
        }
    };

    public Axis[] PingYAxes { get; } = {
        new Axis
        {
            Name = "Ping (ms)",
            NamePaint = new SolidColorPaint(SKColors.Gray),
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0
        }
    };

    public Axis[] PacketLossYAxes { get; } = {
        new Axis
        {
            Name = "Paket Kaybı (%)",
            NamePaint = new SolidColorPaint(SKColors.Gray),
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0,
            MaxLimit = 100
        }
    };

    // Event history
    public ObservableCollection<ConnectionEventItem> EventHistory { get; } = new();

    public MainViewModel()
    {
        _settings = AppSettings.Instance;
        _logger = Logger.Instance;
        _dataService = new DataService();
        _monitorEngine = new MonitorEngine();
        _notificationService = new NotificationService();
        _exportService = new ExportService(_dataService.PingRepository, _dataService.ConnectionEventRepository);
        _networkUsageService = new NetworkUsageService();
        _speedTestService = new SpeedTestService();
        _websiteMonitorService = new WebsiteMonitorService();
        _networkScannerService = new NetworkScannerService();
        _cacheCleanerService = new CacheCleanerService();

        // Subscribe to events
        _monitorEngine.PingCompleted += OnPingCompleted;
        _monitorEngine.MetricsUpdated += OnMetricsUpdated;
        _monitorEngine.ConnectionStateChanged += OnConnectionStateChanged;
        _networkUsageService.StatsUpdated += OnNetworkStatsUpdated;
        _speedTestService.ProgressChanged += OnSpeedTestProgressChanged;
        _speedTestService.TestCompleted += OnSpeedTestCompleted;
        _websiteMonitorService.StatusUpdated += OnWebsiteStatusUpdated;
        _networkScannerService.ScanProgressChanged += OnNetworkScanProgressChanged;
        _networkScannerService.ScanCompleted += OnNetworkScanCompleted;
        _cacheCleanerService.StatusChanged += OnCacheCleanStatusChanged;

        // Initialize network scanner info
        LocalIpAddress = _networkScannerService.LocalIpAddress;
        GatewayAddress = _networkScannerService.GatewayAddress;
        NetworkName = _networkScannerService.NetworkName;

        // Subscribe to language changes
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        // Initialize localized strings
        InitializeLocalizedStrings();

        // Initialize chart series
        PingSeries = new ObservableCollection<ISeries>
        {
            new LineSeries<DateTimePoint>
            {
                Values = _pingData,
                Fill = null,
                GeometryFill = null,
                GeometryStroke = null,
                Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                LineSmoothness = 0.3
            }
        };

        PacketLossSeries = new ObservableCollection<ISeries>
        {
            new LineSeries<DateTimePoint>
            {
                Values = _packetLossData,
                Fill = new SolidColorPaint(SKColors.Red.WithAlpha(50)),
                GeometryFill = null,
                GeometryStroke = null,
                Stroke = new SolidColorPaint(SKColors.Red, 2),
                LineSmoothness = 0
            }
        };

        // Download speed chart series
        DownloadSpeedSeries = new ObservableCollection<ISeries>
        {
            new LineSeries<ObservablePoint>
            {
                Values = _downloadSpeedData,
                Fill = new SolidColorPaint(SKColor.Parse("#4CAF50").WithAlpha(50)),
                GeometryFill = new SolidColorPaint(SKColor.Parse("#4CAF50")),
                GeometryStroke = null,
                GeometrySize = 8,
                Stroke = new SolidColorPaint(SKColor.Parse("#4CAF50"), 3),
                LineSmoothness = 0.5,
                Name = "İndirme"
            }
        };

        // Upload speed chart series
        UploadSpeedSeries = new ObservableCollection<ISeries>
        {
            new LineSeries<ObservablePoint>
            {
                Values = _uploadSpeedData,
                Fill = new SolidColorPaint(SKColor.Parse("#2196F3").WithAlpha(50)),
                GeometryFill = new SolidColorPaint(SKColor.Parse("#2196F3")),
                GeometryStroke = null,
                GeometrySize = 8,
                Stroke = new SolidColorPaint(SKColor.Parse("#2196F3"), 3),
                LineSmoothness = 0.5,
                Name = "Yükleme"
            }
        };

        _logger.Info("MainViewModel initialized");
    }

    [RelayCommand]
    private async Task StartMonitoringAsync()
    {
        if (IsMonitoring) return;

        await _monitorEngine.StartAsync();
        IsMonitoring = true;
        StatusMessage = LocalizationService.Instance["MonitoringStarted"];
        _logger.Info("Monitoring started by user");
    }

    [RelayCommand]
    private async Task StopMonitoringAsync()
    {
        if (!IsMonitoring) return;

        await _monitorEngine.StopAsync();
        IsMonitoring = false;
        StatusMessage = LocalizationService.Instance["MonitoringStopped"];
        _logger.Info("Monitoring stopped by user");
    }

    [RelayCommand]
    private async Task ToggleMonitoringAsync()
    {
        if (IsMonitoring)
            await StopMonitoringAsync();
        else
            await StartMonitoringAsync();
    }

    [RelayCommand]
    private void ClearHistory()
    {
        _pingData.Clear();
        _packetLossData.Clear();
        EventHistory.Clear();
        StatusMessage = LocalizationService.Instance["HistoryCleared"];
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow
        {
            Owner = WpfApplication.Current.MainWindow
        };
        settingsWindow.ShowDialog();
    }

    [RelayCommand]
    private void RefreshNetworkUsage()
    {
        _networkUsageService.Refresh();
    }

    private volatile bool _isSpeedTestRunning;

    [RelayCommand]
    private async Task RunSpeedTestAsync()
    {
        // Test zaten çalışıyorsa çık
        if (_isSpeedTestRunning) return;

        _isSpeedTestRunning = true;
        var loc = LocalizationService.Instance;

        // Grafik verilerini temizle
        _downloadSpeedData.Clear();
        _uploadSpeedData.Clear();
        _speedDataIndex = 0;

        SpeedTestButtonText = loc["TestRunning"];
        SpeedTestProgress = 0;
        DownloadSpeedText = "-";
        UploadSpeedText = "-";
        SpeedTestStatus = loc["Starting"];

        try
        {
            await _speedTestService.RunSpeedTestAsync();
        }
        finally
        {
            _isSpeedTestRunning = false;
            SpeedTestButtonText = loc["StartTest"];
        }
    }

    [RelayCommand]
    private async Task RefreshWebsitesAsync()
    {
        await _websiteMonitorService.RefreshAsync();
    }

    [RelayCommand]
    private async Task ScanNetworkAsync()
    {
        if (_networkScannerService.IsScanning)
        {
            _networkScannerService.StopScan();
            return;
        }

        ScanButtonText = LocalizationService.Instance["Cancel"];
        await _networkScannerService.ScanNetworkAsync();
    }

    [RelayCommand]
    private async Task BlockDeviceAsync(NetworkDevice? device)
    {
        if (device == null || device.IsGateway) return;

        if (device.IsBlocked)
        {
            await _networkScannerService.UnblockDeviceAsync(device.IpAddress);
        }
        else
        {
            var loc = LocalizationService.Instance;
            var message = string.Format(loc["BlockConfirmMessage"], device.DisplayName, device.IpAddress);
            var result = System.Windows.MessageBox.Show(
                message,
                loc["BlockConfirmTitle"],
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                await _networkScannerService.BlockDeviceAsync(device.IpAddress);
            }
        }
    }

    [RelayCommand]
    private void ShowDeviceDetails(NetworkDevice? device)
    {
        if (device == null) return;

        var detailsWindow = new Views.DeviceDetailsWindow(device, _networkScannerService)
        {
            Owner = WpfApplication.Current.MainWindow
        };
        detailsWindow.ShowDialog();
    }

    private void OnSpeedTestProgressChanged(object? sender, EventArgs e)
    {
        WpfApplication.Current?.Dispatcher.BeginInvoke(() =>
        {
            SpeedTestProgress = _speedTestService.Progress;
            SpeedTestStatus = _speedTestService.StatusText;
            DownloadSpeedText = _speedTestService.DownloadSpeedText;
            UploadSpeedText = _speedTestService.UploadSpeedText;
            DownloadServerInfo = _speedTestService.DownloadServerInfo;
            UploadServerInfo = _speedTestService.UploadServerInfo;

            // Grafik verilerini güncelle
            if (_speedTestService.DownloadSpeedMbps > 0 && SpeedTestProgress <= 50)
            {
                _downloadSpeedData.Add(new ObservablePoint(_speedDataIndex++, _speedTestService.DownloadSpeedMbps));
                // Max 50 nokta tut
                while (_downloadSpeedData.Count > 50)
                    _downloadSpeedData.RemoveAt(0);
            }
            else if (_speedTestService.UploadSpeedMbps > 0 && SpeedTestProgress > 50)
            {
                _uploadSpeedData.Add(new ObservablePoint(_speedDataIndex++, _speedTestService.UploadSpeedMbps));
                while (_uploadSpeedData.Count > 50)
                    _uploadSpeedData.RemoveAt(0);
            }
        });
    }

    private void OnSpeedTestCompleted(object? sender, EventArgs e)
    {
        WpfApplication.Current?.Dispatcher.BeginInvoke(() =>
        {
            // Button text is handled in RunSpeedTestAsync finally block
            SpeedTestStatus = _speedTestService.StatusText;
            DownloadSpeedText = _speedTestService.DownloadSpeedText;
            UploadSpeedText = _speedTestService.UploadSpeedText;
            SpeedTestProgress = _speedTestService.Progress;
        });
    }

    private void OnWebsiteStatusUpdated(object? sender, EventArgs e)
    {
        WpfApplication.Current?.Dispatcher.BeginInvoke(() =>
        {
            OnPropertyChanged(nameof(WebsiteCount));
            OnPropertyChanged(nameof(WebsitesOnlineCount));
            OnPropertyChanged(nameof(WebsitesOfflineCount));
        });
    }

    private void OnNetworkScanProgressChanged(object? sender, EventArgs e)
    {
        WpfApplication.Current?.Dispatcher.BeginInvoke(() =>
        {
            NetworkScanProgress = _networkScannerService.ScanProgress;
            NetworkScanStatus = _networkScannerService.ScanStatus;
            NetworkDeviceCount = _networkScannerService.Devices.Count;

            if (_networkScannerService.IsScanning)
                ScanButtonText = LocalizationService.Instance["Cancel"];
        });
    }

    private void OnNetworkScanCompleted(object? sender, EventArgs e)
    {
        WpfApplication.Current?.Dispatcher.BeginInvoke(() =>
        {
            ScanButtonText = LocalizationService.Instance["ScanNetwork"];
            NetworkScanStatus = _networkScannerService.ScanStatus;
            NetworkDeviceCount = _networkScannerService.Devices.Count;
        });
    }

    [RelayCommand]
    private async Task CleanCacheAsync()
    {
        if (IsCleaningCache) return;

        IsCleaningCache = true;
        CacheCleanStatus = LocalizationService.Instance["CleaningStarting"];

        try
        {
            var result = await _cacheCleanerService.CleanAllCachesAsync();
            CacheCleanStatus = result.Summary;

            await Task.Delay(3000);
            CacheCleanStatus = "";
        }
        finally
        {
            IsCleaningCache = false;
        }
    }

    private void OnCacheCleanStatusChanged(object? sender, string status)
    {
        WpfApplication.Current?.Dispatcher.BeginInvoke(() =>
        {
            CacheCleanStatus = status;
        });
    }

    private void OnNetworkStatsUpdated(object? sender, EventArgs e)
    {
        WpfApplication.Current?.Dispatcher.BeginInvoke(() =>
        {
            TotalDownloadSpeed = _networkUsageService.TotalDownloadSpeed;
            TotalUploadSpeed = _networkUsageService.TotalUploadSpeed;
            ActiveConnectionCount = _networkUsageService.ActiveConnectionCount;
            ProcessCount = _networkUsageService.ProcessCount;
        });
    }

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        var loc = LocalizationService.Instance;
        var dialog = new WpfSaveFileDialog
        {
            Filter = $"{loc["CsvFile"]} (*.csv)|*.csv",
            FileName = $"ping_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Title = loc["ExportPingData"]
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var startDate = DateTime.Now.AddDays(-7);
                var endDate = DateTime.Now;
                await _exportService.ExportPingsToCsvAsync(startDate, endDate, dialog.FileName);
                StatusMessage = loc["DataExportedCsv"];
                WpfMessageBox.Show($"{loc["ExportSuccess"]}:\n{dialog.FileName}", loc["ExportSuccess"], WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.Error("CSV export failed", ex);
                WpfMessageBox.Show($"{loc["ExportError"]}: {ex.Message}", loc["ExportError"], WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task ExportReportAsync()
    {
        var loc = LocalizationService.Instance;
        var dialog = new WpfSaveFileDialog
        {
            Filter = $"{loc["TextFile"]} (*.txt)|*.txt",
            FileName = $"internet_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
            Title = loc["CreateSummaryReport"]
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var startDate = DateTime.Now.AddDays(-7);
                var endDate = DateTime.Now;
                await _exportService.ExportSummaryReportAsync(startDate, endDate, dialog.FileName);
                StatusMessage = loc["SummaryReportCreated"];
                WpfMessageBox.Show($"{loc["ExportSuccess"]}:\n{dialog.FileName}", loc["ExportSuccess"], WpfMessageBoxButton.OK, WpfMessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.Error("Report export failed", ex);
                WpfMessageBox.Show($"{loc["ExportError"]}: {ex.Message}", loc["ExportError"], WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
            }
        }
    }

    private void OnPingCompleted(object? sender, PingResult result)
    {
        WpfApplication.Current.Dispatcher.BeginInvoke(() =>
        {
            // Update current ping
            if (result.Success)
            {
                CurrentPing = result.RoundtripTime;

                // Add to chart
                _pingData.Add(new DateTimePoint(result.Timestamp, result.RoundtripTime));

                // Keep only last 60 data points
                while (_pingData.Count > 60)
                    _pingData.RemoveAt(0);
            }

            TotalPings++;
            if (!result.Success)
                FailedPings++;

            // Save to database (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _dataService.SavePingResultAsync(result);
                }
                catch (Exception ex)
                {
                    _logger.Error("Failed to save ping result", ex);
                }
            });
        });
    }

    private void OnMetricsUpdated(object? sender, NetworkMetrics metrics)
    {
        WpfApplication.Current.Dispatcher.BeginInvoke(() =>
        {
            AveragePing = metrics.AveragePing;
            MinPing = metrics.MinPing;
            MaxPing = metrics.MaxPing;
            Jitter = Math.Round(metrics.Jitter, 2);
            PacketLoss = Math.Round(metrics.PacketLossPercent, 2);

            UptimeText = FormatTimeSpan(metrics.TotalUptime);
            DowntimeText = FormatTimeSpan(metrics.TotalDowntime);
            UptimePercent = Math.Round(metrics.UptimePercent, 2);

            // Update packet loss chart
            _packetLossData.Add(new DateTimePoint(DateTime.Now, metrics.PacketLossPercent));
            while (_packetLossData.Count > 60)
                _packetLossData.RemoveAt(0);
        });
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        WpfApplication.Current.Dispatcher.BeginInvoke(() =>
        {
            ConnectionState = e.NewState;
            ConnectionStatusText = GetStateText(e.NewState);

            // Check for notifications
            var metrics = _monitorEngine.CurrentMetrics;
            _notificationService.CheckAndNotify(metrics, _previousState, e.NewState);
            _previousState = e.NewState;

            var eventItem = new ConnectionEventItem
            {
                Timestamp = e.Timestamp,
                EventType = e.NewState.ToString(),
                Description = GetStateDescription(e.OldState, e.NewState),
                Duration = e.PreviousStateDuration
            };

            EventHistory.Insert(0, eventItem);

            // Keep only last 50 events
            while (EventHistory.Count > 50)
                EventHistory.RemoveAt(EventHistory.Count - 1);

            LastEventText = $"{e.Timestamp:HH:mm:ss} - {eventItem.Description}";

            // Save to database (fire and forget)
            var eventType = e.NewState switch
            {
                ConnectionState.Connected => ConnectionEventType.Connected,
                ConnectionState.Disconnected => ConnectionEventType.Disconnected,
                ConnectionState.Unstable => ConnectionEventType.Unstable,
                _ => ConnectionEventType.Connected
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    await _dataService.SaveConnectionEventAsync(eventType, e.PreviousStateDuration);
                }
                catch (Exception ex)
                {
                    _logger.Error("Failed to save connection event", ex);
                }
            });
        });
    }

    private string GetStateText(ConnectionState state)
    {
        var loc = LocalizationService.Instance;
        return state switch
        {
            ConnectionState.Connected => loc["Connected"],
            ConnectionState.Disconnected => loc["Disconnected"],
            ConnectionState.Unstable => loc["Unstable"],
            ConnectionState.Reconnecting => loc["Reconnecting"],
            _ => loc["Unknown"]
        };
    }

    private string GetStateDescription(ConnectionState oldState, ConnectionState newState)
    {
        var loc = LocalizationService.Instance;
        return (oldState, newState) switch
        {
            (_, ConnectionState.Connected) => loc["ConnectionEstablished"],
            (_, ConnectionState.Disconnected) => loc["ConnectionLost"],
            (_, ConnectionState.Unstable) => loc["ConnectionUnstable"],
            (_, ConnectionState.Reconnecting) => loc["Reconnecting"],
            _ => loc["StateChanged"]
        };
    }

    private string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}g {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private void InitializeLocalizedStrings()
    {
        var loc = LocalizationService.Instance;
        ConnectionStatusText = loc["Unknown"];
        SpeedTestStatus = loc["Ready"];
        SpeedTestButtonText = loc["StartTest"];
        NetworkScanStatus = loc["Ready"];
        ScanButtonText = loc["ScanNetwork"];
        StatusMessage = loc["MonitoringStarted"];

        // Update chart axis names
        UpdateChartAxisNames();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        WpfApplication.Current?.Dispatcher.BeginInvoke(() =>
        {
            InitializeLocalizedStrings();

            // Refresh connection status text
            ConnectionStatusText = GetStateText(ConnectionState);

            // Refresh button texts based on current state
            if (_isSpeedTestRunning)
                SpeedTestButtonText = LocalizationService.Instance["TestRunning"];
            else
                SpeedTestButtonText = LocalizationService.Instance["StartTest"];

            if (_networkScannerService.IsScanning)
                ScanButtonText = LocalizationService.Instance["Cancel"];
            else
                ScanButtonText = LocalizationService.Instance["ScanNetwork"];
        });
    }

    private void UpdateChartAxisNames()
    {
        var loc = LocalizationService.Instance;

        // Update X axis name
        if (XAxes.Length > 0)
            XAxes[0].Name = loc["Time"];

        // Update Y axis names
        if (PingYAxes.Length > 0)
            PingYAxes[0].Name = loc["PingMs"];

        if (PacketLossYAxes.Length > 0)
            PacketLossYAxes[0].Name = loc["PacketLossPercent"];
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _monitorEngine.PingCompleted -= OnPingCompleted;
        _monitorEngine.MetricsUpdated -= OnMetricsUpdated;
        _monitorEngine.ConnectionStateChanged -= OnConnectionStateChanged;
        _networkUsageService.StatsUpdated -= OnNetworkStatsUpdated;
        _speedTestService.ProgressChanged -= OnSpeedTestProgressChanged;
        _speedTestService.TestCompleted -= OnSpeedTestCompleted;
        _websiteMonitorService.StatusUpdated -= OnWebsiteStatusUpdated;
        _networkScannerService.ScanProgressChanged -= OnNetworkScanProgressChanged;
        _networkScannerService.ScanCompleted -= OnNetworkScanCompleted;
        _cacheCleanerService.StatusChanged -= OnCacheCleanStatusChanged;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;

        if (_monitorEngine is IDisposable disposable)
            disposable.Dispose();

        _networkUsageService.Dispose();
        _websiteMonitorService.Dispose();
        _networkScannerService.Dispose();
        _speedTestService.Dispose();
        _dataService.Dispose();
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}

public class ConnectionEventItem
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan? Duration { get; set; }

    public string TimeText => Timestamp.ToString("HH:mm:ss");
    public string DurationText => Duration.HasValue ? $"{Duration.Value.TotalSeconds:F0}s" : "-";
}
