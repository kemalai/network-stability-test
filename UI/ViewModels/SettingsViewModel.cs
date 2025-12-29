using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternetMonitor.Infrastructure.Configuration;
using InternetMonitor.Infrastructure.Localization;
using InternetMonitor.Infrastructure.Logging;

namespace InternetMonitor.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly Logger _logger;

    [ObservableProperty]
    private string _pingTarget = string.Empty;

    [ObservableProperty]
    private string _secondaryPingTarget = string.Empty;

    [ObservableProperty]
    private int _pingIntervalMs;

    // Kullanıcı dostu saniye gösterimi
    private double _pingIntervalSeconds;
    public double PingIntervalSeconds
    {
        get => _pingIntervalSeconds;
        set
        {
            if (SetProperty(ref _pingIntervalSeconds, value))
            {
                // Minimum 0.5 saniye, maksimum 60 saniye
                var clampedValue = Math.Max(0.5, Math.Min(60, value));
                PingIntervalMs = (int)(clampedValue * 1000);
                OnPropertyChanged(nameof(PingIntervalDisplayText));
            }
        }
    }

    public string PingIntervalDisplayText => $"{PingIntervalSeconds:F1} saniye ({PingIntervalMs} ms)";

    [ObservableProperty]
    private int _pingTimeoutMs;

    [ObservableProperty]
    private int _highLatencyThreshold;

    [ObservableProperty]
    private double _packetLossThreshold;

    [ObservableProperty]
    private int _downtimeThreshold;

    [ObservableProperty]
    private bool _enableNotifications;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private int _dataRetentionDays;

    // Dil ayarları
    public List<LanguageInfo> AvailableLanguages => LocalizationService.Instance.SupportedLanguages;

    private LanguageInfo? _selectedLanguage;
    public LanguageInfo? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value) && value != null)
            {
                LocalizationService.Instance.CurrentLanguage = value.Code;
            }
        }
    }

    public SettingsViewModel()
    {
        _settings = AppSettings.Instance;
        _logger = Logger.Instance;

        // Load current settings
        LoadSettings();
    }

    private void LoadSettings()
    {
        PingTarget = _settings.PingTarget;
        SecondaryPingTarget = _settings.SecondaryPingTarget;
        PingIntervalMs = _settings.PingIntervalMs;
        _pingIntervalSeconds = _settings.PingIntervalMs / 1000.0;
        OnPropertyChanged(nameof(PingIntervalSeconds));
        OnPropertyChanged(nameof(PingIntervalDisplayText));
        PingTimeoutMs = _settings.PingTimeoutMs;
        HighLatencyThreshold = _settings.HighLatencyThresholdMs;
        PacketLossThreshold = _settings.PacketLossThresholdPercent;
        DowntimeThreshold = _settings.DowntimeThresholdSeconds;
        EnableNotifications = _settings.EnableNotifications;
        MinimizeToTray = _settings.MinimizeToTray;
        StartWithWindows = _settings.StartWithWindows;
        DataRetentionDays = _settings.DataRetentionDays;

        // Dil ayarını yükle
        _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == _settings.Language)
                          ?? AvailableLanguages.FirstOrDefault(l => l.Code == "en");
        OnPropertyChanged(nameof(SelectedLanguage));
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settings.PingTarget = PingTarget;
        _settings.SecondaryPingTarget = SecondaryPingTarget;
        _settings.PingIntervalMs = PingIntervalMs;
        _settings.PingTimeoutMs = PingTimeoutMs;
        _settings.HighLatencyThresholdMs = HighLatencyThreshold;
        _settings.PacketLossThresholdPercent = PacketLossThreshold;
        _settings.DowntimeThresholdSeconds = DowntimeThreshold;
        _settings.EnableNotifications = EnableNotifications;
        _settings.MinimizeToTray = MinimizeToTray;
        _settings.StartWithWindows = StartWithWindows;
        _settings.DataRetentionDays = DataRetentionDays;

        // Dil ayarını kaydet
        if (SelectedLanguage != null)
        {
            _settings.Language = SelectedLanguage.Code;
        }

        _logger.Info($"Settings saved, language: {_settings.Language}");
        StatusMessage = LocalizationService.Instance["SettingsSaved"];
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        PingTarget = "8.8.8.8";
        SecondaryPingTarget = "1.1.1.1";
        PingIntervalSeconds = 1.0; // Varsayılan 1 saniye
        PingTimeoutMs = 3000;
        HighLatencyThreshold = 100;
        PacketLossThreshold = 5.0;
        DowntimeThreshold = 30;
        EnableNotifications = true;
        MinimizeToTray = true;
        StartWithWindows = false;
        DataRetentionDays = 30;

        // Dili İngilizce'ye sıfırla
        SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == "en");

        StatusMessage = LocalizationService.Instance["DefaultsLoaded"];
    }
}
