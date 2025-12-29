using System.Diagnostics;
using System.IO;
using InternetMonitor.Infrastructure.Logging;

namespace InternetMonitor.Core.Services;

public class CacheCleanerService
{
    private readonly Logger _logger = Logger.Instance;

    public event EventHandler<string>? StatusChanged;

    public async Task<CleanResult> CleanAllCachesAsync()
    {
        var result = new CleanResult();

        try
        {
            // 1. DNS Cache temizle
            StatusChanged?.Invoke(this, "DNS önbelleği temizleniyor...");
            result.DnsCacheCleared = await FlushDnsCacheAsync();

            // 2. Windows Temp dosyaları
            StatusChanged?.Invoke(this, "Temp dosyaları temizleniyor...");
            result.TempFilesCleared = await CleanTempFilesAsync();

            // 3. Browser cache'leri (opsiyonel - güvenli olanlar)
            StatusChanged?.Invoke(this, "Tarayıcı önbellekleri temizleniyor...");
            result.BrowserCacheCleared = await CleanBrowserCachesAsync();

            // 4. Thumbnail cache
            StatusChanged?.Invoke(this, "Küçük resim önbelleği temizleniyor...");
            result.ThumbnailCacheCleared = await CleanThumbnailCacheAsync();

            StatusChanged?.Invoke(this, "Temizlik tamamlandı!");
            _logger.Info($"Cache cleaning completed: DNS={result.DnsCacheCleared}, Temp={result.TempFilesCleared}, Browser={result.BrowserCacheCleared}");
        }
        catch (Exception ex)
        {
            _logger.Error("Cache cleaning failed", ex);
            StatusChanged?.Invoke(this, $"Hata: {ex.Message}");
        }

        return result;
    }

    private async Task<bool> FlushDnsCacheAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/flushdns",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.Warning($"DNS flush failed: {ex.Message}");
            return false;
        }
    }

    private async Task<long> CleanTempFilesAsync()
    {
        long totalCleaned = 0;

        var tempPaths = new[]
        {
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            @"C:\Windows\Temp"
        };

        foreach (var tempPath in tempPaths)
        {
            try
            {
                if (!Directory.Exists(tempPath)) continue;

                var files = Directory.GetFiles(tempPath, "*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.LastAccessTime < DateTime.Now.AddDays(-1)) // 1 günden eski
                        {
                            var size = info.Length;
                            File.Delete(file);
                            totalCleaned += size;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        await Task.CompletedTask;
        return totalCleaned;
    }

    private async Task<long> CleanBrowserCachesAsync()
    {
        long totalCleaned = 0;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Chrome Cache
        var chromeCachePaths = new[]
        {
            Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache"),
            Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Code Cache"),
        };

        // Edge Cache
        var edgeCachePaths = new[]
        {
            Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache"),
            Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Code Cache"),
        };

        // Firefox Cache
        var firefoxPath = Path.Combine(localAppData, @"Mozilla\Firefox\Profiles");

        var allPaths = chromeCachePaths.Concat(edgeCachePaths).ToList();

        // Firefox profilleri
        if (Directory.Exists(firefoxPath))
        {
            try
            {
                var profiles = Directory.GetDirectories(firefoxPath);
                foreach (var profile in profiles)
                {
                    allPaths.Add(Path.Combine(profile, "cache2"));
                }
            }
            catch { }
        }

        foreach (var cachePath in allPaths)
        {
            try
            {
                if (!Directory.Exists(cachePath)) continue;

                var files = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var size = new FileInfo(file).Length;
                        File.Delete(file);
                        totalCleaned += size;
                    }
                    catch { }
                }
            }
            catch { }
        }

        await Task.CompletedTask;
        return totalCleaned;
    }

    private async Task<bool> CleanThumbnailCacheAsync()
    {
        try
        {
            var thumbCachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\Windows\Explorer");

            if (!Directory.Exists(thumbCachePath))
                return false;

            var thumbFiles = Directory.GetFiles(thumbCachePath, "thumbcache_*.db");
            foreach (var file in thumbFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch { }
            }

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Thumbnail cache clean failed: {ex.Message}");
            return false;
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F2} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F2} KB";
        return $"{bytes} B";
    }
}

public class CleanResult
{
    public bool DnsCacheCleared { get; set; }
    public long TempFilesCleared { get; set; }
    public long BrowserCacheCleared { get; set; }
    public bool ThumbnailCacheCleared { get; set; }

    public string Summary =>
        $"DNS: {(DnsCacheCleared ? "✓" : "✗")}, " +
        $"Temp: {CacheCleanerService.FormatBytes(TempFilesCleared)}, " +
        $"Tarayıcı: {CacheCleanerService.FormatBytes(BrowserCacheCleared)}";
}
