using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using InternetMonitor.Core.Models;
using InternetMonitor.Infrastructure.Notifications;

namespace InternetMonitor.UI.Controls;

public class TrayIconManager : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly Window _mainWindow;
    private bool _isDisposed;

    public event EventHandler? ShowRequested;
    public event EventHandler? ExitRequested;

    public TrayIconManager(Window mainWindow)
    {
        _mainWindow = mainWindow;
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "Internet Stability Monitor",
            Visible = true,
            Icon = CreateDefaultIcon()
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Göster", null, (s, e) => ShowRequested?.Invoke(this, EventArgs.Empty));
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Çıkış", null, (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    private Icon CreateDefaultIcon()
    {
        // Basit bir ikon oluştur (yeşil daire)
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.FillEllipse(Brushes.LimeGreen, 2, 2, 12, 12);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void UpdateStatus(ConnectionState state)
    {
        if (_notifyIcon == null) return;

        var (color, text) = state switch
        {
            ConnectionState.Connected => (Color.LimeGreen, "Bağlı"),
            ConnectionState.Disconnected => (Color.Red, "Bağlantı Kesildi"),
            ConnectionState.Unstable => (Color.Orange, "Kararsız"),
            _ => (Color.Gray, "Bilinmiyor")
        };

        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 2, 2, 12, 12);
        }

        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = Icon.FromHandle(bitmap.GetHicon());
        _notifyIcon.Text = $"Internet Monitor - {text}";
        oldIcon?.Dispose();
    }

    public void ShowNotification(NotificationEventArgs args)
    {
        if (_notifyIcon == null) return;

        var icon = args.Type switch
        {
            NotificationType.Error => ToolTipIcon.Error,
            NotificationType.Warning => ToolTipIcon.Warning,
            NotificationType.Success => ToolTipIcon.Info,
            _ => ToolTipIcon.None
        };

        _notifyIcon.ShowBalloonTip(3000, args.Title, args.Message, icon);
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _notifyIcon?.Dispose();
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}
