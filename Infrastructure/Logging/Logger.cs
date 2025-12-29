using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace InternetMonitor.Infrastructure.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public class Logger
{
    private static Logger? _instance;
    private static readonly object _lock = new();

    private readonly string _logDirectory;
    private readonly ConcurrentQueue<string> _logQueue = new();
    private readonly System.Threading.Timer _flushTimer;
    private readonly object _fileLock = new();

    public static Logger Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new Logger();
                }
            }
            return _instance;
        }
    }

    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    private Logger()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InternetMonitor",
            "Logs"
        );

        Directory.CreateDirectory(_logDirectory);

        _flushTimer = new System.Threading.Timer(FlushLogs, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message) => Log(LogLevel.Error, message);
    public void Error(string message, Exception ex) => Log(LogLevel.Error, $"{message}: {ex.Message}\n{ex.StackTrace}");

    private void Log(LogLevel level, string message)
    {
        if (level < MinimumLevel) return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level.ToString().ToUpper()}] {message}";

        _logQueue.Enqueue(logEntry);

#if DEBUG
        System.Diagnostics.Debug.WriteLine(logEntry);
#endif
    }

    private void FlushLogs(object? state)
    {
        if (_logQueue.IsEmpty) return;

        var entries = new StringBuilder();
        while (_logQueue.TryDequeue(out var entry))
        {
            entries.AppendLine(entry);
        }

        if (entries.Length == 0) return;

        var logFileName = $"log_{DateTime.Now:yyyy-MM-dd}.txt";
        var logFilePath = Path.Combine(_logDirectory, logFileName);

        lock (_fileLock)
        {
            try
            {
                File.AppendAllText(logFilePath, entries.ToString());
            }
            catch
            {
                // Log yazma hatası - sessizce atla
            }
        }
    }

    public void Flush()
    {
        FlushLogs(null);
    }

    public void CleanOldLogs(int retentionDays)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-retentionDays);
            var logFiles = Directory.GetFiles(_logDirectory, "log_*.txt");

            foreach (var file in logFiles)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.CreationTime < cutoffDate)
                {
                    fileInfo.Delete();
                }
            }
        }
        catch (Exception ex)
        {
            Error("Eski log dosyaları temizlenemedi", ex);
        }
    }
}
