using System;
using System.Diagnostics;
using System.IO;

namespace PRISM.App.Services;

public static class StartupLogger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PRISM", "Logs");
    private static readonly string PrimaryLogFile = Path.Combine(LogDir, "startup.log");
    private static readonly string SecondaryLogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PRISM", "startup_diagnostics.log");

    private static readonly object LockObj = new object();

    public static void InitEarlyLogging()
    {
        try
        {
            if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
        }
        catch { }
    }

    public static void Log(string message)
    {
        lock (LockObj)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [PID:{Environment.ProcessId}] {message}\n";
            try
            {
                if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
                File.AppendAllText(PrimaryLogFile, line);
            }
            catch { }

            try
            {
                var secDir = Path.GetDirectoryName(SecondaryLogFile);
                if (secDir != null && !Directory.Exists(secDir)) Directory.CreateDirectory(secDir);
                File.AppendAllText(SecondaryLogFile, line);
            }
            catch { }

            Debug.WriteLine(line);
        }
    }

    public static void LogException(string context, Exception ex)
    {
        lock (LockObj)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [PID:{Environment.ProcessId}] [EXCEPTION] {context}");
            sb.AppendLine($"  Type: {ex.GetType().FullName}");
            sb.AppendLine($"  Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                sb.AppendLine($"  InnerException: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
            }
            sb.AppendLine($"  StackTrace:\n{ex.StackTrace}");

            string text = sb.ToString();
            try
            {
                if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
                File.AppendAllText(PrimaryLogFile, text);
            }
            catch { }

            try
            {
                File.AppendAllText(SecondaryLogFile, text);
            }
            catch { }

            Debug.WriteLine(text);
        }
    }
}
