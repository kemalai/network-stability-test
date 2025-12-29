using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using InternetMonitor.Infrastructure.Logging;

namespace InternetMonitor.Core.Services;

public class WebsiteMonitorService : IDisposable
{
    private readonly Logger _logger = Logger.Instance;
    private readonly HttpClient _httpClient;
    private readonly System.Threading.Timer _updateTimer;
    private bool _isDisposed;

    public ObservableCollection<WebsiteStatus> Websites { get; } = new();

    // Varsayilan izlenecek siteler
    private readonly List<WebsiteConfig> _defaultWebsites = new()
    {
        new("Google", "https://www.google.com"),
        new("YouTube", "https://www.youtube.com"),
        new("Facebook", "https://www.facebook.com"),
        new("Twitter/X", "https://www.x.com"),
        new("Instagram", "https://www.instagram.com"),
        new("Wikipedia", "https://www.wikipedia.org"),
        new("Amazon", "https://www.amazon.com"),
        new("Netflix", "https://www.netflix.com"),
        new("Cloudflare", "https://www.cloudflare.com"),
        new("GitHub", "https://www.github.com")
    };

    public event EventHandler? StatusUpdated;

    public WebsiteMonitorService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) InternetMonitor/1.0");

        // Initialize websites
        foreach (var site in _defaultWebsites)
        {
            Websites.Add(new WebsiteStatus
            {
                Name = site.Name,
                Url = site.Url,
                Status = "Bekleniyor",
                ResponseTime = "-",
                StatusColor = "#888888"
            });
        }

        // Check every 30 seconds
        _updateTimer = new System.Threading.Timer(async _ => await CheckAllWebsitesAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

        _logger.Info("WebsiteMonitorService initialized");
    }

    public async Task CheckAllWebsitesAsync()
    {
        var tasks = Websites.Select((site, index) => CheckWebsiteAsync(index)).ToList();
        await Task.WhenAll(tasks);
        StatusUpdated?.Invoke(this, EventArgs.Empty);
    }

    private async Task CheckWebsiteAsync(int index)
    {
        if (index < 0 || index >= Websites.Count) return;

        var website = Websites[index];
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await _httpClient.GetAsync(website.Url, HttpCompletionOption.ResponseHeadersRead);
            stopwatch.Stop();

            var responseTime = stopwatch.ElapsedMilliseconds;

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                website.ResponseTime = $"{responseTime} ms";
                website.LastCheck = DateTime.Now;
                website.IsOnline = response.IsSuccessStatusCode;
                website.HttpStatusCode = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    website.Status = "Erisimde";
                    website.StatusColor = responseTime < 500 ? "#4CAF50" : (responseTime < 1000 ? "#FF9800" : "#F44336");
                }
                else
                {
                    website.Status = $"Hata ({(int)response.StatusCode})";
                    website.StatusColor = "#F44336";
                }
            });
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                website.Status = "Zaman asimi";
                website.ResponseTime = ">10s";
                website.StatusColor = "#F44336";
                website.IsOnline = false;
                website.LastCheck = DateTime.Now;
            });
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                website.Status = "Baglanti hatasi";
                website.ResponseTime = "-";
                website.StatusColor = "#F44336";
                website.IsOnline = false;
                website.LastCheck = DateTime.Now;
            });
            _logger.Warning($"Website check failed for {website.Url}: {ex.Message}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                website.Status = "Hata";
                website.ResponseTime = "-";
                website.StatusColor = "#F44336";
                website.IsOnline = false;
                website.LastCheck = DateTime.Now;
            });
            _logger.Error($"Unexpected error checking {website.Url}", ex);
        }
    }

    public void AddWebsite(string name, string url)
    {
        if (Websites.Any(w => w.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
            return;

        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            Websites.Add(new WebsiteStatus
            {
                Name = name,
                Url = url,
                Status = "Bekleniyor",
                ResponseTime = "-",
                StatusColor = "#888888"
            });
        });
    }

    public void RemoveWebsite(string url)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var website = Websites.FirstOrDefault(w => w.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
            if (website != null)
                Websites.Remove(website);
        });
    }

    public async Task RefreshAsync()
    {
        await CheckAllWebsitesAsync();
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _updateTimer.Dispose();
        _httpClient.Dispose();
        _isDisposed = true;

        GC.SuppressFinalize(this);
    }
}

public class WebsiteConfig
{
    public string Name { get; }
    public string Url { get; }

    public WebsiteConfig(string name, string url)
    {
        Name = name;
        Url = url;
    }
}

public class WebsiteStatus : System.ComponentModel.INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _url = string.Empty;
    private string _status = string.Empty;
    private string _responseTime = string.Empty;
    private string _statusColor = "#888888";
    private bool _isOnline;
    private int _httpStatusCode;
    private DateTime _lastCheck;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    public string Url
    {
        get => _url;
        set { _url = value; OnPropertyChanged(nameof(Url)); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

    public string ResponseTime
    {
        get => _responseTime;
        set { _responseTime = value; OnPropertyChanged(nameof(ResponseTime)); }
    }

    public string StatusColor
    {
        get => _statusColor;
        set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); }
    }

    public bool IsOnline
    {
        get => _isOnline;
        set { _isOnline = value; OnPropertyChanged(nameof(IsOnline)); }
    }

    public int HttpStatusCode
    {
        get => _httpStatusCode;
        set { _httpStatusCode = value; OnPropertyChanged(nameof(HttpStatusCode)); }
    }

    public DateTime LastCheck
    {
        get => _lastCheck;
        set { _lastCheck = value; OnPropertyChanged(nameof(LastCheck)); OnPropertyChanged(nameof(LastCheckText)); }
    }

    public string LastCheckText => LastCheck == default ? "-" : LastCheck.ToString("HH:mm:ss");

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
}
