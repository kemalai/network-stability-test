using System.IO;
using InternetMonitor.Infrastructure.Configuration;
using InternetMonitor.Infrastructure.Helpers;
using InternetMonitor.Infrastructure.Localization;
using InternetMonitor.Infrastructure.Logging;

namespace InternetMonitor;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Logger'ı başlat
        var logger = Logger.Instance;
        logger.Info("Application starting...");

        // Dil ayarını yükle - varsayılan İngilizce
        var settings = AppSettings.Instance;
        LocalizationService.Instance.CurrentLanguage = settings.Language;
        logger.Info($"Language set to: {settings.Language}");

        // Eski logları temizle
        logger.CleanOldLogs(30);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        Logger.Instance.Info("Application exiting...");
        Logger.Instance.Flush();
        base.OnExit(e);
    }
}
