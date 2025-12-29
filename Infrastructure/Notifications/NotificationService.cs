using System.Windows;
using InternetMonitor.Core.Models;
using InternetMonitor.Infrastructure.Configuration;
using InternetMonitor.Infrastructure.Logging;

namespace InternetMonitor.Infrastructure.Notifications;

public class NotificationService
{
    private readonly AppSettings _settings;
    private readonly Logger _logger;
    private DateTime _lastNotificationTime = DateTime.MinValue;
    private const int MinNotificationIntervalSeconds = 30;

    public event EventHandler<NotificationEventArgs>? NotificationTriggered;

    public NotificationService()
    {
        _settings = AppSettings.Instance;
        _logger = Logger.Instance;
    }

    public void CheckAndNotify(NetworkMetrics metrics, ConnectionState previousState, ConnectionState currentState)
    {
        if (!_settings.EnableNotifications) return;

        // Rate limiting
        if ((DateTime.Now - _lastNotificationTime).TotalSeconds < MinNotificationIntervalSeconds)
            return;

        // Bağlantı kesildi
        if (previousState != ConnectionState.Disconnected && currentState == ConnectionState.Disconnected)
        {
            TriggerNotification(
                "Bağlantı Kesildi",
                "İnternet bağlantınız kesildi!",
                NotificationType.Error
            );
            return;
        }

        // Bağlantı geri geldi
        if (previousState == ConnectionState.Disconnected && currentState == ConnectionState.Connected)
        {
            TriggerNotification(
                "Bağlantı Kuruldu",
                "İnternet bağlantınız yeniden sağlandı.",
                NotificationType.Success
            );
            return;
        }

        // Yüksek ping
        if (metrics.CurrentPing > _settings.HighLatencyThresholdMs && currentState == ConnectionState.Connected)
        {
            TriggerNotification(
                "Yüksek Gecikme",
                $"Ping değeri yüksek: {metrics.CurrentPing} ms",
                NotificationType.Warning
            );
            return;
        }

        // Yüksek packet loss
        if (metrics.PacketLossPercent > _settings.PacketLossThresholdPercent)
        {
            TriggerNotification(
                "Paket Kaybı",
                $"Paket kaybı tespit edildi: %{metrics.PacketLossPercent:F1}",
                NotificationType.Warning
            );
            return;
        }
    }

    private void TriggerNotification(string title, string message, NotificationType type)
    {
        _lastNotificationTime = DateTime.Now;
        _logger.Info($"Notification: [{type}] {title} - {message}");

        NotificationTriggered?.Invoke(this, new NotificationEventArgs
        {
            Title = title,
            Message = message,
            Type = type,
            Timestamp = DateTime.Now
        });
    }
}

public class NotificationEventArgs : EventArgs
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}
