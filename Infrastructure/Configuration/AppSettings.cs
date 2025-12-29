namespace InternetMonitor.Infrastructure.Configuration;

public class AppSettings
{
    private static AppSettings? _instance;
    private static readonly object _lock = new();

    public static AppSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new AppSettings();
                }
            }
            return _instance;
        }
    }

    // Ping ayarları
    public string PingTarget { get; set; } = "8.8.8.8";
    public string SecondaryPingTarget { get; set; } = "1.1.1.1";
    public int PingIntervalMs { get; set; } = 1000; // Varsayılan 1 saniye
    public int PingTimeoutMs { get; set; } = 3000;
    public int PingBufferSize { get; set; } = 32;

    // Bildirim eşikleri
    public int HighLatencyThresholdMs { get; set; } = 100;
    public double PacketLossThresholdPercent { get; set; } = 5.0;
    public int DowntimeThresholdSeconds { get; set; } = 30;

    // Uygulama ayarları
    public bool StartMinimized { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool EnableNotifications { get; set; } = true;

    // Dil ayarı - varsayılan İngilizce
    public string Language { get; set; } = "en";

    // Veritabanı ayarları
    public string DatabasePath { get; set; } = "internetmonitor.db";
    public int DataRetentionDays { get; set; } = 30;

    // Jitter hesaplama
    public int JitterSampleSize { get; set; } = 10;
}
