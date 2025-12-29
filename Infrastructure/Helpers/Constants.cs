namespace InternetMonitor.Infrastructure.Helpers;

public static class Constants
{
    public const string AppName = "Internet Stability Monitor";
    public const string AppVersion = "1.0.0";

    // Varsayılan ping hedefleri
    public static class PingTargets
    {
        public const string GoogleDns = "8.8.8.8";
        public const string CloudflareDns = "1.1.1.1";
        public const string OpenDns = "208.67.222.222";
        public const string Quad9Dns = "9.9.9.9";
    }

    // Bağlantı durumları
    public static class ConnectionStatus
    {
        public const string Connected = "Bağlı";
        public const string Disconnected = "Bağlantı Kesildi";
        public const string Unstable = "Kararsız";
        public const string Unknown = "Bilinmiyor";
    }

    // Renk kodları
    public static class StatusColors
    {
        public const string Green = "#4CAF50";
        public const string Yellow = "#FFC107";
        public const string Red = "#F44336";
        public const string Gray = "#9E9E9E";
    }

    // Zaman aralıkları
    public static class TimeRanges
    {
        public const int OneHourMinutes = 60;
        public const int OneDayMinutes = 1440;
        public const int OneWeekMinutes = 10080;
        public const int OneMonthMinutes = 43200;
    }

    // Veritabanı
    public static class Database
    {
        public const string FileName = "internetmonitor.db";
        public const int DefaultRetentionDays = 30;
    }
}
