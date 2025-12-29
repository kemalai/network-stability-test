using System.ComponentModel;
using System.Globalization;

namespace InternetMonitor.Infrastructure.Localization;

public class LanguageInfo
{
    public string Code { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;

    public string DisplayName => $"{NativeName} ({EnglishName})";
}

public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    private static readonly object _lock = new();

    public static LocalizationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new LocalizationService();
                }
            }
            return _instance;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Desteklenen diller
    public List<LanguageInfo> SupportedLanguages { get; } = new()
    {
        new LanguageInfo { Code = "en", NativeName = "English", EnglishName = "English" },
        new LanguageInfo { Code = "tr", NativeName = "Turkce", EnglishName = "Turkish" },
        new LanguageInfo { Code = "es", NativeName = "Espanol", EnglishName = "Spanish" },
        new LanguageInfo { Code = "fr", NativeName = "Francais", EnglishName = "French" },
        new LanguageInfo { Code = "de", NativeName = "Deutsch", EnglishName = "German" },
        new LanguageInfo { Code = "pt", NativeName = "Portugues", EnglishName = "Portuguese" },
        new LanguageInfo { Code = "ru", NativeName = "Russkiy", EnglishName = "Russian" }
    };

    private string _currentLanguage = "en";
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value && _translations.ContainsKey(value))
            {
                _currentLanguage = value;
                OnPropertyChanged(nameof(CurrentLanguage));
                OnPropertyChanged(nameof(CurrentLanguageInfo));

                // Tüm çeviri key'lerini güncelle
                RefreshAllBindings();

                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // Tüm binding'leri yenile
    private void RefreshAllBindings()
    {
        // Indexer binding'leri için Item[] property'sini güncelle
        OnPropertyChanged("Item[]");

        // Tüm bilinen key'ler için PropertyChanged tetikle
        foreach (var key in _translations["en"].Keys)
        {
            OnPropertyChanged($"[{key}]");
        }
    }

    public LanguageInfo CurrentLanguageInfo => SupportedLanguages.FirstOrDefault(l => l.Code == _currentLanguage) ?? SupportedLanguages[0];

    public event EventHandler? LanguageChanged;

    // Tum ceviri sozlukleri
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();

    private LocalizationService()
    {
        InitializeTranslations();
    }

    private void InitializeTranslations()
    {
        // English (Default)
        _translations["en"] = new Dictionary<string, string>
        {
            // Main Window
            ["AppTitle"] = "Internet Stability Monitor",
            ["ConnectionStatus"] = "Connection Status",
            ["Connected"] = "Connected",
            ["Disconnected"] = "Disconnected",
            ["Unstable"] = "Unstable",
            ["Reconnecting"] = "Reconnecting",
            ["Unknown"] = "Unknown",
            ["LastEvent"] = "Last Event",
            ["ClearCache"] = "Clear Cache",
            ["CSV"] = "CSV",
            ["Report"] = "Report",
            ["Settings"] = "Settings",

            // Dashboard Tab
            ["Dashboard"] = "Dashboard",
            ["CurrentPing"] = "Current Ping",
            ["AveragePing"] = "Average Ping",
            ["Jitter"] = "Jitter",
            ["PacketLoss"] = "Packet Loss",
            ["Uptime"] = "Uptime",
            ["UptimePercent"] = "Uptime %",
            ["PingHistory"] = "Ping History",
            ["PacketLossHistory"] = "Packet Loss History",
            ["EventHistory"] = "Event History",
            ["Clear"] = "Clear",

            // Network Usage Tab
            ["NetworkUsage"] = "Network Usage",
            ["TotalDownload"] = "Total Download",
            ["TotalUpload"] = "Total Upload",
            ["ActiveConnections"] = "Active Connections",
            ["MonitoredProcesses"] = "Monitored Processes",
            ["ProcessBasedUsage"] = "Process Based Network Usage",
            ["Refresh"] = "Refresh",
            ["ProcessName"] = "Process Name",
            ["PID"] = "PID",
            ["Download"] = "Download",
            ["Upload"] = "Upload",
            ["TotalDownloaded"] = "Total Downloaded",
            ["TotalUploaded"] = "Total Uploaded",
            ["Connections"] = "Connections",

            // Speed Test Tab
            ["SpeedTest"] = "Speed Test",
            ["DownloadSpeed"] = "Download Speed",
            ["UploadSpeed"] = "Upload Speed",
            ["TestServer"] = "Test Server",
            ["StartTest"] = "Start Test",
            ["TestRunning"] = "Testing...",
            ["Ready"] = "Ready",
            ["Starting"] = "Starting...",
            ["DownloadServer"] = "Download Server",
            ["UploadServer"] = "Upload Server",
            ["RealTimeSpeed"] = "Real Time Speed",

            // Website Monitor Tab
            ["Websites"] = "Websites",
            ["MonitoredSites"] = "Monitored Sites",
            ["Online"] = "Online",
            ["Offline"] = "Offline",
            ["WebsiteStatus"] = "Website Status",
            ["SiteName"] = "Site Name",
            ["URL"] = "URL",
            ["Status"] = "Status",
            ["ResponseTime"] = "Response Time",
            ["LastCheck"] = "Last Check",

            // Network Devices Tab
            ["NetworkDevices"] = "Network Devices",
            ["LocalIP"] = "Local IP",
            ["Gateway"] = "Gateway",
            ["NetworkName"] = "Network Name",
            ["FoundDevices"] = "Found Devices",
            ["ScanNetwork"] = "Scan Network",
            ["Cancel"] = "Cancel",
            ["DevicesOnNetwork"] = "Devices on Network",
            ["DoubleClickForDetails"] = "(Double click for details)",
            ["DeviceType"] = "Device Type",
            ["IPAddress"] = "IP Address",
            ["Hostname"] = "Hostname",
            ["Manufacturer"] = "Manufacturer",
            ["Ping"] = "Ping",
            ["Action"] = "Action",
            ["Block"] = "Block",
            ["Unblock"] = "Unblock",

            // Settings Window
            ["PingSettings"] = "Ping Settings",
            ["PrimaryTarget"] = "Primary Target (IP)",
            ["SecondaryTarget"] = "Secondary Target (IP)",
            ["PingInterval"] = "Ping Interval",
            ["Timeout"] = "Timeout (ms)",
            ["NotificationThresholds"] = "Notification Thresholds",
            ["HighPing"] = "High Ping (ms)",
            ["PacketLossPercent"] = "Packet Loss (%)",
            ["DowntimeThreshold"] = "Downtime Threshold (seconds)",
            ["ApplicationSettings"] = "Application Settings",
            ["EnableNotifications"] = "Enable Notifications",
            ["MinimizeToTray"] = "Minimize to Tray",
            ["StartWithWindows"] = "Start with Windows",
            ["DataRetention"] = "Data Retention (days)",
            ["Language"] = "Language",
            ["ResetToDefaults"] = "Reset to Defaults",
            ["Save"] = "Save",
            ["SettingsSaved"] = "Settings saved",
            ["DefaultsLoaded"] = "Default settings loaded",

            // Status Bar
            ["TotalPings"] = "Total Pings",
            ["Failed"] = "Failed",
            ["Target"] = "Target",

            // Tooltips
            ["CurrentPingTooltip"] = "Round trip time of packet sent to server.\n\nIdeal values:\n- 0-30 ms: Excellent\n- 30-60 ms: Very Good\n- 60-100 ms: Good\n- 100-150 ms: Average\n- 150+ ms: Poor",
            ["AveragePingTooltip"] = "Average of all ping values. Shows overall connection quality.",
            ["JitterTooltip"] = "Fluctuation in ping values. Low jitter = stable connection.\n\nIdeal values:\n- 0-5 ms: Excellent\n- 5-15 ms: Good\n- 15-30 ms: Average\n- 30+ ms: Poor",
            ["PacketLossTooltip"] = "Percentage of packets that failed to reach destination.\n\nIdeal values:\n- 0%: Excellent\n- 0-1%: Acceptable\n- 1-2.5%: Noticeable issues\n- 2.5%+: Serious connection problem",
            ["UptimeTooltip"] = "Total uninterrupted internet uptime.\n\nFormat: DAYS HOURS:MINUTES:SECONDS",
            ["UptimePercentTooltip"] = "Percentage of total monitoring time without interruption.",

            // Messages
            ["ConnectionEstablished"] = "Connection established",
            ["ConnectionLost"] = "Connection lost",
            ["ConnectionUnstable"] = "Connection became unstable",
            ["TestCompleted"] = "Completed",
            ["CouldNotConnect"] = "Could not connect to server",
            ["DownloadError"] = "Download error",
            ["MonitoringStarted"] = "Monitoring started",
            ["MonitoringStopped"] = "Monitoring stopped",
            ["HistoryCleared"] = "History cleared",
            ["CleaningStarting"] = "Cleaning starting...",
            ["StateChanged"] = "State changed",

            // Chart Labels
            ["Time"] = "Time",
            ["PingMs"] = "Ping (ms)",
            ["PacketLossPercent"] = "Packet Loss (%)",

            // Export
            ["ExportSuccess"] = "Report saved successfully",
            ["ExportError"] = "Export error",
            ["DataExportedCsv"] = "Data exported as CSV",
            ["SummaryReportCreated"] = "Summary report created",
            ["CsvFile"] = "CSV File",
            ["TextFile"] = "Text File",
            ["ExportPingData"] = "Export Ping Data",
            ["CreateSummaryReport"] = "Create Summary Report",

            // Block Device
            ["BlockConfirmTitle"] = "Block Connection Confirmation",
            ["BlockConfirmMessage"] = "{0} ({1}) device's internet connection will be blocked.\n\nDo you want to continue?",

            // Device Details
            ["DeviceDetails"] = "Device Details",
            ["BasicInfo"] = "Basic Information",
            ["NetworkInfo"] = "Network Information",
            ["Close"] = "Close"
        };

        // Turkish
        _translations["tr"] = new Dictionary<string, string>
        {
            // Main Window
            ["AppTitle"] = "Internet Stabilite Monitoru",
            ["ConnectionStatus"] = "Baglanti Durumu",
            ["Connected"] = "Bagli",
            ["Disconnected"] = "Baglanti Kesildi",
            ["Unstable"] = "Kararsiz",
            ["Reconnecting"] = "Yeniden Baglaniyor",
            ["Unknown"] = "Bilinmiyor",
            ["LastEvent"] = "Son Olay",
            ["ClearCache"] = "Onbellek Temizle",
            ["CSV"] = "CSV",
            ["Report"] = "Rapor",
            ["Settings"] = "Ayarlar",

            // Dashboard Tab
            ["Dashboard"] = "Dashboard",
            ["CurrentPing"] = "Anlik Ping",
            ["AveragePing"] = "Ortalama Ping",
            ["Jitter"] = "Jitter",
            ["PacketLoss"] = "Paket Kaybi",
            ["Uptime"] = "Uptime",
            ["UptimePercent"] = "Uptime %",
            ["PingHistory"] = "Ping Gecmisi",
            ["PacketLossHistory"] = "Paket Kaybi Gecmisi",
            ["EventHistory"] = "Olay Gecmisi",
            ["Clear"] = "Temizle",

            // Network Usage Tab
            ["NetworkUsage"] = "Network Kullanimi",
            ["TotalDownload"] = "Toplam Indirme",
            ["TotalUpload"] = "Toplam Yukleme",
            ["ActiveConnections"] = "Aktif Baglanti",
            ["MonitoredProcesses"] = "Izlenen Islem",
            ["ProcessBasedUsage"] = "Islem Bazli Network Kullanimi",
            ["Refresh"] = "Yenile",
            ["ProcessName"] = "Islem Adi",
            ["PID"] = "PID",
            ["Download"] = "Indirme",
            ["Upload"] = "Yukleme",
            ["TotalDownloaded"] = "Toplam Indirilen",
            ["TotalUploaded"] = "Toplam Yuklenen",
            ["Connections"] = "Baglanti",

            // Speed Test Tab
            ["SpeedTest"] = "Hiz Testi",
            ["DownloadSpeed"] = "Indirme Hizi",
            ["UploadSpeed"] = "Yukleme Hizi",
            ["TestServer"] = "Test Sunucusu",
            ["StartTest"] = "Testi Baslat",
            ["TestRunning"] = "Test Yapiliyor...",
            ["Ready"] = "Hazir",
            ["Starting"] = "Baslatiliyor...",
            ["DownloadServer"] = "Indirme Sunucu",
            ["UploadServer"] = "Yukleme Sunucu",
            ["RealTimeSpeed"] = "Gercek Zamanli Hiz",

            // Website Monitor Tab
            ["Websites"] = "Web Siteleri",
            ["MonitoredSites"] = "Izlenen Site",
            ["Online"] = "Erisimde",
            ["Offline"] = "Erisim Disi",
            ["WebsiteStatus"] = "Website Durumu",
            ["SiteName"] = "Site Adi",
            ["URL"] = "URL",
            ["Status"] = "Durum",
            ["ResponseTime"] = "Yanit Suresi",
            ["LastCheck"] = "Son Kontrol",

            // Network Devices Tab
            ["NetworkDevices"] = "Ag Cihazlari",
            ["LocalIP"] = "Yerel IP",
            ["Gateway"] = "Gateway",
            ["NetworkName"] = "Ag Adi",
            ["FoundDevices"] = "Bulunan Cihaz",
            ["ScanNetwork"] = "Agi Tara",
            ["Cancel"] = "Iptal Et",
            ["DevicesOnNetwork"] = "Agdaki Cihazlar",
            ["DoubleClickForDetails"] = "(Detay icin cift tiklayin)",
            ["DeviceType"] = "Cihaz Tipi",
            ["IPAddress"] = "IP Adresi",
            ["Hostname"] = "Hostname",
            ["Manufacturer"] = "Uretici",
            ["Ping"] = "Ping",
            ["Action"] = "Islem",
            ["Block"] = "Engelle",
            ["Unblock"] = "Engeli Kaldir",

            // Settings Window
            ["PingSettings"] = "Ping Ayarlari",
            ["PrimaryTarget"] = "Birincil Hedef (IP)",
            ["SecondaryTarget"] = "Yedek Hedef (IP)",
            ["PingInterval"] = "Ping Araligi",
            ["Timeout"] = "Timeout (ms)",
            ["NotificationThresholds"] = "Bildirim Esikleri",
            ["HighPing"] = "Yuksek Ping (ms)",
            ["PacketLossPercent"] = "Paket Kaybi (%)",
            ["DowntimeThreshold"] = "Kesinti Esigi (saniye)",
            ["ApplicationSettings"] = "Uygulama Ayarlari",
            ["EnableNotifications"] = "Bildirimleri Etkinlestir",
            ["MinimizeToTray"] = "System Tray'e Kucult",
            ["StartWithWindows"] = "Windows ile Baslat",
            ["DataRetention"] = "Veri Saklama Suresi (gun)",
            ["Language"] = "Dil",
            ["ResetToDefaults"] = "Varsayilana Don",
            ["Save"] = "Kaydet",
            ["SettingsSaved"] = "Ayarlar kaydedildi",
            ["DefaultsLoaded"] = "Varsayilan ayarlar yuklendi",

            // Status Bar
            ["TotalPings"] = "Toplam Ping",
            ["Failed"] = "Basarisiz",
            ["Target"] = "Hedef",

            // Tooltips
            ["CurrentPingTooltip"] = "Sunucuya gonderilen paketin geri donme suresi.\n\nIdeal degerler:\n- 0-30 ms: Mukemmel\n- 30-60 ms: Cok Iyi\n- 60-100 ms: Iyi\n- 100-150 ms: Orta\n- 150+ ms: Kotu",
            ["AveragePingTooltip"] = "Tum ping degerlerinin ortalamasi. Genel baglanti kalitesini gosterir.",
            ["JitterTooltip"] = "Ping degerlerindeki dalgalanma miktari. Dusuk jitter = kararli baglanti.\n\nIdeal degerler:\n- 0-5 ms: Mukemmel\n- 5-15 ms: Iyi\n- 15-30 ms: Orta\n- 30+ ms: Kotu",
            ["PacketLossTooltip"] = "Gonderilen paketlerin hedefe ulasamama orani.\n\nIdeal degerler:\n- %0: Mukemmel\n- %0-1: Kabul edilebilir\n- %1-2.5: Fark edilir sorunlar\n- %2.5+: Ciddi baglanti sorunu",
            ["UptimeTooltip"] = "Internetin kesintisiz calistigi toplam sure.\n\nFormat: GUN SAAT:DAKIKA:SANIYE",
            ["UptimePercentTooltip"] = "Toplam izleme suresinin yuzde kaci kesintisiz gecti.",

            // Messages
            ["ConnectionEstablished"] = "Baglanti kuruldu",
            ["ConnectionLost"] = "Baglanti kesildi",
            ["ConnectionUnstable"] = "Baglanti kararsiz hale geldi",
            ["TestCompleted"] = "Tamamlandi",
            ["CouldNotConnect"] = "Sunucuya baglanilamadi",
            ["DownloadError"] = "Indirme hatasi",
            ["MonitoringStarted"] = "Izleme baslatildi",
            ["MonitoringStopped"] = "Izleme durduruldu",
            ["HistoryCleared"] = "Gecmis temizlendi",
            ["CleaningStarting"] = "Temizlik basliyor...",
            ["StateChanged"] = "Durum degisti",

            // Chart Labels
            ["Time"] = "Zaman",
            ["PingMs"] = "Ping (ms)",
            ["PacketLossPercent"] = "Paket Kaybi (%)",

            // Export
            ["ExportSuccess"] = "Rapor basariyla kaydedildi",
            ["ExportError"] = "Disa aktarma hatasi",
            ["DataExportedCsv"] = "Veriler CSV olarak disa aktarildi",
            ["SummaryReportCreated"] = "Ozet rapor olusturuldu",
            ["CsvFile"] = "CSV Dosyasi",
            ["TextFile"] = "Metin Dosyasi",
            ["ExportPingData"] = "Ping Verilerini Disa Aktar",
            ["CreateSummaryReport"] = "Ozet Rapor Olustur",

            // Block Device
            ["BlockConfirmTitle"] = "Baglanti Kesme Onayi",
            ["BlockConfirmMessage"] = "{0} ({1}) cihazinin internet baglantisi kesilecek.\n\nDevam etmek istiyor musunuz?",

            // Device Details
            ["DeviceDetails"] = "Cihaz Detaylari",
            ["BasicInfo"] = "Temel Bilgiler",
            ["NetworkInfo"] = "Ag Bilgileri",
            ["Close"] = "Kapat"
        };

        // Spanish
        _translations["es"] = new Dictionary<string, string>
        {
            ["AppTitle"] = "Monitor de Estabilidad de Internet",
            ["ConnectionStatus"] = "Estado de Conexion",
            ["Connected"] = "Conectado",
            ["Disconnected"] = "Desconectado",
            ["Unstable"] = "Inestable",
            ["Reconnecting"] = "Reconectando",
            ["Unknown"] = "Desconocido",
            ["LastEvent"] = "Ultimo Evento",
            ["ClearCache"] = "Limpiar Cache",
            ["Settings"] = "Configuracion",
            ["Dashboard"] = "Panel",
            ["CurrentPing"] = "Ping Actual",
            ["AveragePing"] = "Ping Promedio",
            ["Jitter"] = "Jitter",
            ["PacketLoss"] = "Perdida de Paquetes",
            ["Uptime"] = "Tiempo Activo",
            ["UptimePercent"] = "% Activo",
            ["PingHistory"] = "Historial de Ping",
            ["PacketLossHistory"] = "Historial de Perdida",
            ["EventHistory"] = "Historial de Eventos",
            ["Clear"] = "Limpiar",
            ["NetworkUsage"] = "Uso de Red",
            ["TotalDownload"] = "Descarga Total",
            ["TotalUpload"] = "Carga Total",
            ["ActiveConnections"] = "Conexiones Activas",
            ["MonitoredProcesses"] = "Procesos Monitoreados",
            ["Refresh"] = "Actualizar",
            ["SpeedTest"] = "Prueba de Velocidad",
            ["DownloadSpeed"] = "Velocidad de Descarga",
            ["UploadSpeed"] = "Velocidad de Carga",
            ["TestServer"] = "Servidor de Prueba",
            ["StartTest"] = "Iniciar Prueba",
            ["TestRunning"] = "Probando...",
            ["Websites"] = "Sitios Web",
            ["NetworkDevices"] = "Dispositivos de Red",
            ["LocalIP"] = "IP Local",
            ["Gateway"] = "Puerta de Enlace",
            ["ScanNetwork"] = "Escanear Red",
            ["Language"] = "Idioma",
            ["Save"] = "Guardar",
            ["ResetToDefaults"] = "Restablecer",
            ["PingSettings"] = "Configuracion de Ping",
            ["ApplicationSettings"] = "Configuracion de Aplicacion",
            ["EnableNotifications"] = "Habilitar Notificaciones",
            ["SettingsSaved"] = "Configuracion guardada",
            ["Close"] = "Cerrar"
        };

        // French
        _translations["fr"] = new Dictionary<string, string>
        {
            ["AppTitle"] = "Moniteur de Stabilite Internet",
            ["ConnectionStatus"] = "Etat de Connexion",
            ["Connected"] = "Connecte",
            ["Disconnected"] = "Deconnecte",
            ["Unstable"] = "Instable",
            ["Reconnecting"] = "Reconnexion",
            ["Unknown"] = "Inconnu",
            ["LastEvent"] = "Dernier Evenement",
            ["ClearCache"] = "Vider le Cache",
            ["Settings"] = "Parametres",
            ["Dashboard"] = "Tableau de Bord",
            ["CurrentPing"] = "Ping Actuel",
            ["AveragePing"] = "Ping Moyen",
            ["Jitter"] = "Gigue",
            ["PacketLoss"] = "Perte de Paquets",
            ["Uptime"] = "Temps de Fonctionnement",
            ["UptimePercent"] = "% Fonctionnement",
            ["PingHistory"] = "Historique Ping",
            ["PacketLossHistory"] = "Historique Pertes",
            ["EventHistory"] = "Historique Evenements",
            ["Clear"] = "Effacer",
            ["NetworkUsage"] = "Utilisation Reseau",
            ["TotalDownload"] = "Telechargement Total",
            ["TotalUpload"] = "Envoi Total",
            ["ActiveConnections"] = "Connexions Actives",
            ["MonitoredProcesses"] = "Processus Surveilles",
            ["Refresh"] = "Actualiser",
            ["SpeedTest"] = "Test de Vitesse",
            ["DownloadSpeed"] = "Vitesse de Telechargement",
            ["UploadSpeed"] = "Vitesse d'Envoi",
            ["TestServer"] = "Serveur de Test",
            ["StartTest"] = "Demarrer le Test",
            ["TestRunning"] = "Test en cours...",
            ["Websites"] = "Sites Web",
            ["NetworkDevices"] = "Peripheriques Reseau",
            ["LocalIP"] = "IP Locale",
            ["Gateway"] = "Passerelle",
            ["ScanNetwork"] = "Scanner le Reseau",
            ["Language"] = "Langue",
            ["Save"] = "Enregistrer",
            ["ResetToDefaults"] = "Reinitialiser",
            ["PingSettings"] = "Parametres Ping",
            ["ApplicationSettings"] = "Parametres Application",
            ["EnableNotifications"] = "Activer les Notifications",
            ["SettingsSaved"] = "Parametres enregistres",
            ["Close"] = "Fermer"
        };

        // German
        _translations["de"] = new Dictionary<string, string>
        {
            ["AppTitle"] = "Internet Stabilitatsmonitor",
            ["ConnectionStatus"] = "Verbindungsstatus",
            ["Connected"] = "Verbunden",
            ["Disconnected"] = "Getrennt",
            ["Unstable"] = "Instabil",
            ["Reconnecting"] = "Verbinde erneut",
            ["Unknown"] = "Unbekannt",
            ["LastEvent"] = "Letztes Ereignis",
            ["ClearCache"] = "Cache leeren",
            ["Settings"] = "Einstellungen",
            ["Dashboard"] = "Ubersicht",
            ["CurrentPing"] = "Aktueller Ping",
            ["AveragePing"] = "Durchschnittlicher Ping",
            ["Jitter"] = "Jitter",
            ["PacketLoss"] = "Paketverlust",
            ["Uptime"] = "Betriebszeit",
            ["UptimePercent"] = "Betriebszeit %",
            ["PingHistory"] = "Ping-Verlauf",
            ["PacketLossHistory"] = "Paketverlust-Verlauf",
            ["EventHistory"] = "Ereignisverlauf",
            ["Clear"] = "Loschen",
            ["NetworkUsage"] = "Netzwerknutzung",
            ["TotalDownload"] = "Gesamtdownload",
            ["TotalUpload"] = "Gesamtupload",
            ["ActiveConnections"] = "Aktive Verbindungen",
            ["MonitoredProcesses"] = "Uberwachte Prozesse",
            ["Refresh"] = "Aktualisieren",
            ["SpeedTest"] = "Geschwindigkeitstest",
            ["DownloadSpeed"] = "Download-Geschwindigkeit",
            ["UploadSpeed"] = "Upload-Geschwindigkeit",
            ["TestServer"] = "Testserver",
            ["StartTest"] = "Test starten",
            ["TestRunning"] = "Test lauft...",
            ["Websites"] = "Webseiten",
            ["NetworkDevices"] = "Netzwerkgerate",
            ["LocalIP"] = "Lokale IP",
            ["Gateway"] = "Gateway",
            ["ScanNetwork"] = "Netzwerk scannen",
            ["Language"] = "Sprache",
            ["Save"] = "Speichern",
            ["ResetToDefaults"] = "Zurucksetzen",
            ["PingSettings"] = "Ping-Einstellungen",
            ["ApplicationSettings"] = "Anwendungseinstellungen",
            ["EnableNotifications"] = "Benachrichtigungen aktivieren",
            ["SettingsSaved"] = "Einstellungen gespeichert",
            ["Close"] = "Schliessen"
        };

        // Portuguese
        _translations["pt"] = new Dictionary<string, string>
        {
            ["AppTitle"] = "Monitor de Estabilidade de Internet",
            ["ConnectionStatus"] = "Status da Conexao",
            ["Connected"] = "Conectado",
            ["Disconnected"] = "Desconectado",
            ["Unstable"] = "Instavel",
            ["Reconnecting"] = "Reconectando",
            ["Unknown"] = "Desconhecido",
            ["LastEvent"] = "Ultimo Evento",
            ["ClearCache"] = "Limpar Cache",
            ["Settings"] = "Configuracoes",
            ["Dashboard"] = "Painel",
            ["CurrentPing"] = "Ping Atual",
            ["AveragePing"] = "Ping Medio",
            ["Jitter"] = "Jitter",
            ["PacketLoss"] = "Perda de Pacotes",
            ["Uptime"] = "Tempo Ativo",
            ["UptimePercent"] = "% Ativo",
            ["PingHistory"] = "Historico de Ping",
            ["PacketLossHistory"] = "Historico de Perdas",
            ["EventHistory"] = "Historico de Eventos",
            ["Clear"] = "Limpar",
            ["NetworkUsage"] = "Uso da Rede",
            ["TotalDownload"] = "Download Total",
            ["TotalUpload"] = "Upload Total",
            ["ActiveConnections"] = "Conexoes Ativas",
            ["MonitoredProcesses"] = "Processos Monitorados",
            ["Refresh"] = "Atualizar",
            ["SpeedTest"] = "Teste de Velocidade",
            ["DownloadSpeed"] = "Velocidade de Download",
            ["UploadSpeed"] = "Velocidade de Upload",
            ["TestServer"] = "Servidor de Teste",
            ["StartTest"] = "Iniciar Teste",
            ["TestRunning"] = "Testando...",
            ["Websites"] = "Sites",
            ["NetworkDevices"] = "Dispositivos de Rede",
            ["LocalIP"] = "IP Local",
            ["Gateway"] = "Gateway",
            ["ScanNetwork"] = "Escanear Rede",
            ["Language"] = "Idioma",
            ["Save"] = "Salvar",
            ["ResetToDefaults"] = "Restaurar Padroes",
            ["PingSettings"] = "Configuracoes de Ping",
            ["ApplicationSettings"] = "Configuracoes do Aplicativo",
            ["EnableNotifications"] = "Ativar Notificacoes",
            ["SettingsSaved"] = "Configuracoes salvas",
            ["Close"] = "Fechar"
        };

        // Russian
        _translations["ru"] = new Dictionary<string, string>
        {
            ["AppTitle"] = "Monitor Stabilnosti Interneta",
            ["ConnectionStatus"] = "Status Podklyucheniya",
            ["Connected"] = "Podklyucheno",
            ["Disconnected"] = "Otklyucheno",
            ["Unstable"] = "Nestabilno",
            ["Reconnecting"] = "Perepodklyuchenie",
            ["Unknown"] = "Neizvestno",
            ["LastEvent"] = "Poslednee Sobytie",
            ["ClearCache"] = "Ochistit Kesh",
            ["Settings"] = "Nastroyki",
            ["Dashboard"] = "Panel",
            ["CurrentPing"] = "Tekushchiy Ping",
            ["AveragePing"] = "Sredniy Ping",
            ["Jitter"] = "Dzhitter",
            ["PacketLoss"] = "Poterya Paketov",
            ["Uptime"] = "Vremya Raboty",
            ["UptimePercent"] = "% Raboty",
            ["PingHistory"] = "Istoriya Ping",
            ["PacketLossHistory"] = "Istoriya Poter",
            ["EventHistory"] = "Istoriya Sobytiy",
            ["Clear"] = "Ochistit",
            ["NetworkUsage"] = "Ispolzovanie Seti",
            ["TotalDownload"] = "Vsego Zagruzheno",
            ["TotalUpload"] = "Vsego Otpravleno",
            ["ActiveConnections"] = "Aktivnye Podklyucheniya",
            ["MonitoredProcesses"] = "Otslezhivaemye Protsessy",
            ["Refresh"] = "Obnovit",
            ["SpeedTest"] = "Test Skorosti",
            ["DownloadSpeed"] = "Skorost Zagruzki",
            ["UploadSpeed"] = "Skorost Otdachi",
            ["TestServer"] = "Testovyy Server",
            ["StartTest"] = "Zapustit Test",
            ["TestRunning"] = "Testirovanie...",
            ["Websites"] = "Veb-sayty",
            ["NetworkDevices"] = "Setevye Ustroystva",
            ["LocalIP"] = "Lokalnyy IP",
            ["Gateway"] = "Shlyuz",
            ["ScanNetwork"] = "Skanirovat Set",
            ["Language"] = "Yazyk",
            ["Save"] = "Sokhranit",
            ["ResetToDefaults"] = "Sbrosit",
            ["PingSettings"] = "Nastroyki Ping",
            ["ApplicationSettings"] = "Nastroyki Prilozheniya",
            ["EnableNotifications"] = "Vklyuchit Uvedomleniya",
            ["SettingsSaved"] = "Nastroyki sokhraneny",
            ["Close"] = "Zakryt"
        };

        // Fill missing keys from English for all languages
        foreach (var lang in _translations.Keys.Where(k => k != "en"))
        {
            foreach (var key in _translations["en"].Keys)
            {
                if (!_translations[lang].ContainsKey(key))
                {
                    _translations[lang][key] = _translations["en"][key];
                }
            }
        }
    }

    public string this[string key]
    {
        get => GetString(key);
    }

    public string GetString(string key)
    {
        if (_translations.TryGetValue(_currentLanguage, out var langDict))
        {
            if (langDict.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        // Fallback to English
        if (_translations.TryGetValue("en", out var enDict))
        {
            if (enDict.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return $"[{key}]";
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
