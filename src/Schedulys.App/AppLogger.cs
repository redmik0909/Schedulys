using System;
using System.IO;
using Sentry;

namespace Schedulys.App;

public static class AppLogger
{
    private static string _logPath    = "";
    private static bool   _sentryReady;
    private static readonly object _lock = new();

    public static string LogPath => _logPath;

    public static void Init(string appDataDir)
    {
        try
        {
            var logsDir = Path.Combine(appDataDir, "logs");
            Directory.CreateDirectory(logsDir);
            _logPath = Path.Combine(logsDir, $"app_{DateTime.Now:yyyy-MM-dd}.log");
            PurgeOldLogs(logsDir, keepDays: 30);
        }
        catch { }
    }

    public static void InitSentry() => _sentryReady = true;

    public static void Info(string category, string message)
        => Write("INFO ", category, message);

    public static void Warn(string category, string message)
        => Write("WARN ", category, message);

    public static void Error(string category, string message, Exception? ex = null)
    {
        Write("ERROR", category, message, ex);
        ForwardToSentry(category, message, ex);
    }

    // ── Privé ────────────────────────────────────────────────────────────────

    private static void Write(string level, string category, string message, Exception? ex = null)
    {
        if (string.IsNullOrEmpty(_logPath)) return;
        try
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] [{category,-12}] {message}";
            if (ex is not null)
                line += $"\n               {ex.GetType().Name}: {ex.Message}" +
                        $"\n               Stack: {ex.StackTrace?.Replace("\n", "\n               ")}";

            lock (_lock)
                File.AppendAllText(_logPath, line + "\n");
        }
        catch { }
    }

    private static void ForwardToSentry(string category, string message, Exception? ex)
    {
        if (!_sentryReady) return;
        try
        {
            if (ex is not null)
            {
                SentrySdk.CaptureException(ex, scope =>
                {
                    scope.SetTag("category", category);
                    scope.SetExtra("message", message);
                });
            }
            else
            {
                SentrySdk.CaptureMessage($"[{category}] {message}", SentryLevel.Error);
            }
        }
        catch { }
    }

    private static void PurgeOldLogs(string logsDir, int keepDays)
    {
        var cutoff = DateTime.Now.AddDays(-keepDays);
        foreach (var file in Directory.GetFiles(logsDir, "app_*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
            catch { }
        }
    }
}
