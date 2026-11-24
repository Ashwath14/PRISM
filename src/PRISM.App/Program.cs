using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PRISM.App.Services;

namespace PRISM.App;

public static class Program
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [STAThread]
    public static int Main(string[] args)
    {
        StartupLogger.InitEarlyLogging();
        StartupLogger.Log($"[PROGRAM MAIN ENTRY] PRISM started. PID={Environment.ProcessId}, SessionId={Process.GetCurrentProcess().SessionId}, Args='{string.Join(" ", args)}'");

        try
        {
            var app = new App();
            app.InitializeComponent();
            int exitCode = app.Run();
            StartupLogger.Log($"[PROGRAM MAIN EXIT] app.Run() completed with exit code: {exitCode}");
            return exitCode;
        }
        catch (Exception ex)
        {
            StartupLogger.LogException("Fatal unhandled exception in Program.Main", ex);
            try
            {
                string msg = $"PRISM Critical Startup Failure:\n\n{ex.GetType().FullName}: {ex.Message}\n\nStack:\n{ex.StackTrace}";
                MessageBox(IntPtr.Zero, msg, "PRISM Fatal Startup Error", 0x10); // MB_ICONERROR
            }
            catch { }
            return 1;
        }
    }
}
