using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Collections.ObjectModel;
using InternetMonitor.Infrastructure.Logging;

namespace InternetMonitor.Core.Services;

public class SpeedTestServer
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string UploadUrl { get; set; } = string.Empty;
    public string DisplayName => $"{Name} ({Location})";
}

public class SpeedTestService : IDisposable
{
    private readonly Logger _logger = Logger.Instance;
    private volatile bool _isRunning;
    private volatile bool _isCancelling;
    private CancellationTokenSource? _cts;

    // Available test servers
    public ObservableCollection<SpeedTestServer> AvailableServers { get; } = new()
    {
        new SpeedTestServer
        {
            Name = "Cloudflare",
            Location = "Global CDN",
            DownloadUrl = "https://speed.cloudflare.com/__down?bytes=104857600",
            UploadUrl = "https://speed.cloudflare.com/__up"
        },
        new SpeedTestServer
        {
            Name = "Hetzner",
            Location = "Almanya",
            DownloadUrl = "https://speed.hetzner.de/100MB.bin",
            UploadUrl = "https://speed.cloudflare.com/__up"
        },
        new SpeedTestServer
        {
            Name = "Tele2",
            Location = "Isvec",
            DownloadUrl = "https://speedtest.tele2.net/100MB.zip",
            UploadUrl = "https://speed.cloudflare.com/__up"
        },
        new SpeedTestServer
        {
            Name = "OVH",
            Location = "Fransa",
            DownloadUrl = "https://proof.ovh.net/files/100Mb.dat",
            UploadUrl = "https://speed.cloudflare.com/__up"
        },
        new SpeedTestServer
        {
            Name = "Scaleway",
            Location = "Fransa",
            DownloadUrl = "https://mirror.scaleway.com/speedtest/100MiB.bin",
            UploadUrl = "https://speed.cloudflare.com/__up"
        }
    };

    private SpeedTestServer? _selectedServer;
    public SpeedTestServer? SelectedServer
    {
        get => _selectedServer ?? AvailableServers.FirstOrDefault();
        set => _selectedServer = value;
    }

    public bool IsRunning => _isRunning;
    public double DownloadSpeedMbps { get; private set; }
    public double UploadSpeedMbps { get; private set; }
    public string DownloadSpeedText { get; private set; } = "-";
    public string UploadSpeedText { get; private set; } = "-";
    public int Progress { get; private set; }
    public string StatusText { get; private set; } = "Hazır";

    // Server information
    public string CurrentServerName { get; private set; } = "-";
    public string CurrentServerUrl { get; private set; } = "-";
    public string DownloadServerInfo { get; private set; } = "-";
    public string UploadServerInfo { get; private set; } = "-";

    // Grafik için hız verileri
    public ObservableCollection<SpeedDataPoint> SpeedHistory { get; } = new();

    public SpeedTestService()
    {
        _selectedServer = AvailableServers.FirstOrDefault();
    }

    public event EventHandler? ProgressChanged;
    public event EventHandler? TestCompleted;

    public async Task RunSpeedTestAsync(CancellationToken externalToken = default)
    {
        if (_isRunning) return;

        _isRunning = true;
        _isCancelling = false;
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var token = _cts.Token;

        Progress = 0;
        DownloadSpeedMbps = 0;
        UploadSpeedMbps = 0;
        DownloadSpeedText = "-";
        UploadSpeedText = "-";
        CurrentServerName = "-";
        CurrentServerUrl = "-";
        DownloadServerInfo = "-";
        UploadServerInfo = "-";

        // Log which server is selected
        var server = SelectedServer ?? AvailableServers.First();
        _logger.Info($"Speed test starting with server: {server.Name} ({server.Location})");

        System.Windows.Application.Current?.Dispatcher.Invoke(() => SpeedHistory.Clear());

        try
        {
            // Download Test
            StatusText = $"{server.Name} sunucusundan indirme testi...";
            ProgressChanged?.Invoke(this, EventArgs.Empty);

            await MeasureDownloadSpeedAsync(token);

            if (token.IsCancellationRequested || _isCancelling)
            {
                StatusText = "Test iptal edildi";
                Progress = 0;
                _logger.Info("Speed test cancelled during download phase");
                return;
            }

            // Upload Test
            StatusText = "Yükleme hızı ölçülüyor...";
            ProgressChanged?.Invoke(this, EventArgs.Empty);

            await MeasureUploadSpeedAsync(token);

            if (token.IsCancellationRequested || _isCancelling)
            {
                StatusText = "Test iptal edildi";
                Progress = 0;
                _logger.Info("Speed test cancelled during upload phase");
                return;
            }

            Progress = 100;
            StatusText = $"Tamamlandı - ↓{DownloadSpeedText} ↑{UploadSpeedText}";
            _logger.Info($"Speed test completed: Download={DownloadSpeedMbps:F2} Mbps, Upload={UploadSpeedMbps:F2} Mbps");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Test iptal edildi";
            Progress = 0;
            _logger.Info("Speed test cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Hata: {ex.Message}";
            _logger.Error("Speed test failed", ex);
        }
        finally
        {
            _isRunning = false;
            _isCancelling = false;
            ProgressChanged?.Invoke(this, EventArgs.Empty);
            TestCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task MeasureDownloadSpeedAsync(CancellationToken token)
    {
        const int TestDurationSeconds = 12;
        const int NumConnections = 6;

        // Use selected server or default to first
        var server = SelectedServer ?? AvailableServers.First();
        var url = server.DownloadUrl;

        _logger.Info($"Starting download test from: {url}");

        if (token.IsCancellationRequested || _isCancelling) return;

        try
        {
            // Set current server info
            var uri = new Uri(url);
            CurrentServerName = server.Name;
            CurrentServerUrl = uri.Host;
            DownloadServerInfo = $"{server.Name} ({server.Location})";
            ProgressChanged?.Invoke(this, EventArgs.Empty);

            var totalBytes = 0L;
            var speedSamples = new List<double>();
            var stopwatch = Stopwatch.StartNew();
            var lastUpdate = stopwatch.ElapsedMilliseconds;
            var lastBytes = 0L;
            var bytesLock = new object();
            var runningTasks = new List<Task>();

            // Paralel indirme başlat
            for (int i = 0; i < NumConnections; i++)
            {
                if (token.IsCancellationRequested || _isCancelling) break;

                var task = Task.Run(async () =>
                {
                    try
                    {
                        using var handler = new HttpClientHandler
                        {
                            AutomaticDecompression = DecompressionMethods.None,
                            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                            AllowAutoRedirect = true,
                            MaxAutomaticRedirections = 5
                        };
                        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(TestDurationSeconds + 10) };
                        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                        client.DefaultRequestHeaders.Add("Accept", "*/*");

                        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                        response.EnsureSuccessStatusCode();

                        using var stream = await response.Content.ReadAsStreamAsync(token);
                        var buffer = new byte[131072]; // 128KB buffer

                        while (!token.IsCancellationRequested && !_isCancelling && stopwatch.Elapsed.TotalSeconds < TestDurationSeconds)
                        {
                            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                            if (bytesRead == 0) break;

                            lock (bytesLock)
                            {
                                totalBytes += bytesRead;
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (HttpRequestException ex)
                    {
                        _logger.Warning($"Download HTTP error from {server.Name}: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning($"Download stream error from {server.Name}: {ex.Message}");
                    }
                }, token);

                runningTasks.Add(task);
            }

            // İlerleme ve hız güncelleme
            var noDataCounter = 0;
            while (stopwatch.Elapsed.TotalSeconds < TestDurationSeconds && !token.IsCancellationRequested && !_isCancelling)
            {
                try
                {
                    await Task.Delay(250, token);
                }
                catch (OperationCanceledException)
                {
                    _logger.Info("Download cancelled via token");
                    break;
                }

                if (_isCancelling)
                {
                    _logger.Info("Download cancelled via flag");
                    break;
                }

                // Tüm tasklar bitmiş mi kontrol et
                if (runningTasks.All(t => t.IsCompleted))
                {
                    _logger.Info("All download tasks completed early");
                    break;
                }

                var elapsed = stopwatch.ElapsedMilliseconds;
                var elapsedSinceUpdate = elapsed - lastUpdate;

                if (elapsedSinceUpdate >= 500)
                {
                    long currentBytes;
                    lock (bytesLock)
                    {
                        currentBytes = totalBytes;
                    }

                    var bytesDiff = currentBytes - lastBytes;
                    var instantSpeed = (bytesDiff * 8.0) / (elapsedSinceUpdate / 1000.0) / 1_000_000;

                    if (instantSpeed > 0)
                    {
                        speedSamples.Add(instantSpeed);
                        noDataCounter = 0;

                        // Son 5 örneğin ortalaması
                        var recentSamples = speedSamples.TakeLast(5).ToList();
                        var avgSpeed = recentSamples.Average();

                        DownloadSpeedMbps = avgSpeed;
                        DownloadSpeedText = FormatSpeed(avgSpeed);

                        // Grafik için veri ekle
                        AddSpeedDataPoint(avgSpeed, true);
                    }
                    else
                    {
                        noDataCounter++;
                        // 3 saniye veri gelmezse çık
                        if (noDataCounter >= 6 && stopwatch.Elapsed.TotalSeconds > 3)
                        {
                            _logger.Warning($"No data received for 3 seconds from {server.Name}, aborting download test");
                            break;
                        }
                    }

                    Progress = (int)(stopwatch.Elapsed.TotalSeconds / TestDurationSeconds * 50);
                    StatusText = $"İndirme ({server.Name}): {DownloadSpeedText}";
                    ProgressChanged?.Invoke(this, EventArgs.Empty);

                    lastUpdate = elapsed;
                    lastBytes = currentBytes;
                }
            }

            // Taskların bitmesini bekle (max 1 saniye)
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await Task.WhenAll(runningTasks).WaitAsync(timeoutCts.Token);
            }
            catch { }

            stopwatch.Stop();

            // Final hız hesapla
            if (totalBytes > 0 && stopwatch.Elapsed.TotalSeconds > 1 && !_isCancelling)
            {
                var finalSpeed = (totalBytes * 8.0) / stopwatch.Elapsed.TotalSeconds / 1_000_000;

                // En yüksek örnek ile final arasında ortala
                if (speedSamples.Count > 0)
                {
                    var maxSample = speedSamples.OrderByDescending(x => x).Take(3).Average();
                    DownloadSpeedMbps = Math.Max(finalSpeed, maxSample);
                }
                else
                {
                    DownloadSpeedMbps = finalSpeed;
                }

                DownloadSpeedText = FormatSpeed(DownloadSpeedMbps);
                _logger.Info($"Download from {server.Name}: {totalBytes / (1024 * 1024)} MB in {stopwatch.Elapsed.TotalSeconds:F1}s = {DownloadSpeedMbps:F1} Mbps");
            }
            else if (totalBytes == 0 && !_isCancelling)
            {
                _logger.Warning($"No data received from {server.Name} ({url})");
                StatusText = $"Sunucuya bağlanılamadı: {server.Name}";
            }

            Progress = 50;
            ProgressChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Download test failed for {server.Name} ({url}): {ex.Message}");
            StatusText = $"İndirme hatası: {ex.Message}";
        }
    }

    private async Task MeasureUploadSpeedAsync(CancellationToken token)
    {
        const int TestDurationSeconds = 8;
        const int ChunkSize = 1024 * 1024; // 1MB chunks

        // Use selected server or default
        var server = SelectedServer ?? AvailableServers.First();
        var uploadUrl = server.UploadUrl;

        _logger.Info($"Starting upload test to: {uploadUrl}");

        if (token.IsCancellationRequested || _isCancelling) return;

        try
        {
            var totalBytesSent = 0L;
            var speedSamples = new List<double>();
            var stopwatch = Stopwatch.StartNew();
            var lastUpdate = stopwatch.ElapsedMilliseconds;

            // Test verisi oluştur
            var testData = new byte[ChunkSize];
            new Random().NextBytes(testData);

            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(TestDurationSeconds + 10) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.Add("Accept", "*/*");

            // Set upload server info
            var uri = new Uri(uploadUrl);
            UploadServerInfo = $"{GetServerName(uri.Host)} ({uri.Host})";
            ProgressChanged?.Invoke(this, EventArgs.Empty);

            while (stopwatch.Elapsed.TotalSeconds < TestDurationSeconds && !token.IsCancellationRequested && !_isCancelling)
            {
                try
                {
                    using var content = new ByteArrayContent(testData);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                    var uploadStart = stopwatch.ElapsedMilliseconds;
                    using var response = await client.PostAsync(uploadUrl, content, token);
                    var uploadEnd = stopwatch.ElapsedMilliseconds;

                    if (response.IsSuccessStatusCode)
                    {
                        totalBytesSent += ChunkSize;
                        var uploadTime = (uploadEnd - uploadStart) / 1000.0;

                        if (uploadTime > 0.01)
                        {
                            var instantSpeed = (ChunkSize * 8.0) / uploadTime / 1_000_000;
                            speedSamples.Add(instantSpeed);

                            var recentSamples = speedSamples.TakeLast(3).ToList();
                            var avgSpeed = recentSamples.Average();

                            UploadSpeedMbps = avgSpeed;
                            UploadSpeedText = FormatSpeed(avgSpeed);

                            AddSpeedDataPoint(avgSpeed, false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Upload chunk failed: {ex.Message}");
                }

                if (_isCancelling) break;

                // Progress güncelle
                var elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed - lastUpdate >= 500)
                {
                    Progress = 50 + (int)(stopwatch.Elapsed.TotalSeconds / TestDurationSeconds * 50);
                    StatusText = $"Yükleme: {UploadSpeedText}";
                    ProgressChanged?.Invoke(this, EventArgs.Empty);
                    lastUpdate = elapsed;
                }
            }

            stopwatch.Stop();

            // Final hız
            if (speedSamples.Count > 0 && !_isCancelling)
            {
                UploadSpeedMbps = speedSamples.OrderByDescending(x => x).Take(3).Average();
                UploadSpeedText = FormatSpeed(UploadSpeedMbps);
                _logger.Info($"Upload: {totalBytesSent / (1024 * 1024)} MB = {UploadSpeedMbps:F1} Mbps");
            }
            else if (DownloadSpeedMbps > 0 && !_isCancelling)
            {
                // Fallback
                UploadSpeedMbps = DownloadSpeedMbps * 0.4;
                UploadSpeedText = $"~{FormatSpeed(UploadSpeedMbps)}";
            }

            if (!_isCancelling)
            {
                Progress = 100;
                ProgressChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Upload test failed: {ex.Message}");
            if (DownloadSpeedMbps > 0 && !_isCancelling)
            {
                UploadSpeedMbps = DownloadSpeedMbps * 0.4;
                UploadSpeedText = $"~{FormatSpeed(UploadSpeedMbps)}";
            }
        }
    }

    private void AddSpeedDataPoint(double speedMbps, bool isDownload)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            SpeedHistory.Add(new SpeedDataPoint
            {
                Timestamp = DateTime.Now,
                SpeedMbps = speedMbps,
                IsDownload = isDownload
            });

            // Max 100 nokta tut
            while (SpeedHistory.Count > 100)
                SpeedHistory.RemoveAt(0);
        });
    }

    public void Cancel()
    {
        _isCancelling = true;
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, ignore
        }
        _logger.Info("Speed test cancellation requested");
    }

    private string FormatSpeed(double mbps)
    {
        if (mbps >= 1000)
            return $"{mbps / 1000:F2} Gbps";
        if (mbps >= 1)
            return $"{mbps:F1} Mbps";
        if (mbps >= 0.001)
            return $"{mbps * 1000:F0} Kbps";
        return "0 Kbps";
    }

    private string GetServerName(string host)
    {
        return host.ToLower() switch
        {
            "speed.cloudflare.com" => "Cloudflare",
            "speed.hetzner.de" => "Hetzner (Almanya)",
            "speedtest.tele2.net" => "Tele2 (İsveç)",
            _ when host.Contains("cloudflare") => "Cloudflare",
            _ when host.Contains("hetzner") => "Hetzner",
            _ when host.Contains("tele2") => "Tele2",
            _ => host
        };
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class SpeedDataPoint
{
    public DateTime Timestamp { get; set; }
    public double SpeedMbps { get; set; }
    public bool IsDownload { get; set; }
}
