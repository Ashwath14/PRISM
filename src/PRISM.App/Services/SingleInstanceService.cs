using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PRISM.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string BaseMutexName = "Local\\PRISM_SingleInstance_Mutex";
    private static readonly string DefaultPipeName = $"PRISM_IPC_{Environment.UserName}_S{Process.GetCurrentProcess().SessionId}";

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
    private const int ASFW_ANY = -1;

    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private Thread? _serverThread;
    private volatile bool _disposed = false;
    private readonly Func<string[], bool> _onActivateFunc;
    private bool _isPrimaryInstance = false;

    public SingleInstanceService(Func<string[], bool> onActivateFunc, string? mutexName = null, string? pipeName = null)
    {
        _onActivateFunc = onActivateFunc ?? throw new ArgumentNullException(nameof(onActivateFunc));
        _mutexName = mutexName ?? BaseMutexName;
        _pipeName = pipeName ?? DefaultPipeName;
    }

    public SingleInstanceService(Action<string[]> onActivateAction, string? mutexName = null, string? pipeName = null)
        : this(args => { onActivateAction(args); return true; }, mutexName, pipeName)
    {
    }

    /// <summary>
    /// Attempts to register as the primary instance.
    /// If another instance is running and responsive, signals it to restore/activate and returns false.
    /// If another instance is a zombie/unresponsive, terminates it, claims ownership, and returns true.
    /// </summary>
    public bool Initialize(string[] args)
    {
        StartupLogger.Log($"SingleInstanceService: Checking for existing instance on pipe '{_pipeName}'...");

        // 1. Try to signal an active running instance via Named Pipe
        if (TrySignalExistingInstance(args))
        {
            StartupLogger.Log("SingleInstanceService: Existing instance was successfully signalled and restored. Exiting this secondary instance.");
            return false;
        }

        StartupLogger.Log("SingleInstanceService: No responsive instance detected on IPC pipe. Checking for orphaned/zombie processes...");

        // 2. No responsive instance answered the pipe. Clean up any orphaned or zombie PRISM processes.
        CleanupZombieProcesses();

        // 3. Acquire the single-instance mutex
        try
        {
            _mutex = new Mutex(true, _mutexName, out bool createdNew);
            StartupLogger.Log($"SingleInstanceService: Mutex check (createdNew={createdNew}).");
            if (!createdNew)
            {
                bool acquired = _mutex.WaitOne(1000, false);
                StartupLogger.Log($"SingleInstanceService: Mutex WaitOne result: acquired={acquired}.");
                if (!acquired)
                {
                    StartupLogger.Log("SingleInstanceService: Mutex held by terminated/stuck process. Forcing cleanup and retrying...");
                    CleanupZombieProcesses();
                    _mutex.Dispose();
                    _mutex = new Mutex(true, _mutexName, out _);
                }
            }
        }
        catch (AbandonedMutexException)
        {
            StartupLogger.Log("SingleInstanceService: AbandonedMutexException caught; claimed ownership of mutex.");
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"SingleInstanceService: [MUTEX WARNING] {ex.Message}. Proceeding as primary.");
        }

        _isPrimaryInstance = true;

        // 4. Start Named Pipe Server to listen for future activations
        StartPipeServer();

        StartupLogger.Log("SingleInstanceService: Initialized successfully as primary instance.");
        return true;
    }

    private bool TrySignalExistingInstance(string[] args)
    {
        try
        {
            // Allow target process to set foreground window
            try
            {
                AllowSetForegroundWindow(ASFW_ANY);
            }
            catch { }

            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.None);
            StartupLogger.Log($"SingleInstanceService: Attempting connection to pipe '{_pipeName}' (timeout=600ms)...");
            client.Connect(600);

            StartupLogger.Log("SingleInstanceService: Connected to existing instance pipe. Sending payload...");
            string payload = string.Join("|", args);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            byte[] lenBytes = BitConverter.GetBytes(bytes.Length);

            client.Write(lenBytes, 0, 4);
            client.Write(bytes, 0, bytes.Length);
            client.Flush();

            // Read response from server
            byte[] respLenBytes = new byte[4];
            int readBytes = 0;
            while (readBytes < 4)
            {
                int r = client.Read(respLenBytes, readBytes, 4 - readBytes);
                if (r <= 0) break;
                readBytes += r;
            }

            if (readBytes == 4)
            {
                int respLen = BitConverter.ToInt32(respLenBytes, 0);
                if (respLen > 0 && respLen < 1024)
                {
                    byte[] respPayload = new byte[respLen];
                    int totalRead = 0;
                    while (totalRead < respLen)
                    {
                        int r = client.Read(respPayload, totalRead, respLen - totalRead);
                        if (r <= 0) break;
                        totalRead += r;
                    }

                    string response = Encoding.UTF8.GetString(respPayload, 0, totalRead);
                    StartupLogger.Log($"SingleInstanceService: Received handshake response from primary: '{response}'");
                    if (response.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ensure foreground window permission right before exiting
                        try { AllowSetForegroundWindow(ASFW_ANY); } catch { }
                        return true;
                    }
                }
            }

            StartupLogger.Log("SingleInstanceService: Pipe server did not confirm successful window activation.");
            return false;
        }
        catch (TimeoutException)
        {
            StartupLogger.Log("SingleInstanceService: IPC pipe connection timed out. No responsive instance.");
            return false;
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"SingleInstanceService: IPC pipe error: {ex.Message}");
            return false;
        }
    }

    private void CleanupZombieProcesses()
    {
        try
        {
            int currentPid = Environment.ProcessId;
            int currentSessionId = Process.GetCurrentProcess().SessionId;

            var otherProcesses = Process.GetProcessesByName("PRISM")
                .Where(p => p.Id != currentPid && p.SessionId == currentSessionId)
                .ToList();

            if (otherProcesses.Count > 0)
            {
                StartupLogger.Log($"SingleInstanceService: Found {otherProcesses.Count} unresponsive/zombie PRISM process(es) in Session {currentSessionId}. Terminating...");
                foreach (var proc in otherProcesses)
                {
                    try
                    {
                        StartupLogger.Log($"Terminating zombie PID {proc.Id}...");
                        proc.Kill();
                        proc.WaitForExit(1000);
                        StartupLogger.Log($"Zombie PID {proc.Id} terminated successfully.");
                    }
                    catch (Exception ex)
                    {
                        StartupLogger.Log($"Error terminating zombie PID {proc.Id}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"SingleInstanceService: Process cleanup error: {ex.Message}");
        }
    }

    private void StartPipeServer()
    {
        _serverThread = new Thread(() =>
        {
            StartupLogger.Log($"SingleInstanceService: IPC Pipe server thread started on '{_pipeName}'.");

            while (!_disposed)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.None);

                    server.WaitForConnection();

                    if (_disposed) break;

                    StartupLogger.Log("SingleInstanceService: Incoming IPC connection received from secondary instance.");

                    byte[] lenBytes = new byte[4];
                    int readLen = 0;
                    while (readLen < 4)
                    {
                        int r = server.Read(lenBytes, readLen, 4 - readLen);
                        if (r <= 0) break;
                        readLen += r;
                    }

                    if (readLen == 4)
                    {
                        int len = BitConverter.ToInt32(lenBytes, 0);
                        if (len >= 0 && len < 65536)
                        {
                            byte[] payloadBytes = new byte[len];
                            int totalRead = 0;
                            while (totalRead < len)
                            {
                                int r = server.Read(payloadBytes, totalRead, len - totalRead);
                                if (r <= 0) break;
                                totalRead += r;
                            }

                            string payload = Encoding.UTF8.GetString(payloadBytes, 0, totalRead);
                            StartupLogger.Log($"SingleInstanceService: IPC payload received: '{payload}'");

                            string[] receivedArgs = string.IsNullOrEmpty(payload)
                                ? Array.Empty<string>()
                                : payload.Split('|');

                            bool activated = false;
                            try
                            {
                                activated = _onActivateFunc(receivedArgs);
                            }
                            catch (Exception ex)
                            {
                                StartupLogger.Log($"SingleInstanceService: Error in onActivateFunc: {ex}");
                            }

                            string response = activated ? $"OK|{Environment.ProcessId}" : "FAIL";
                            byte[] respBytes = Encoding.UTF8.GetBytes(response);
                            byte[] respLenBytes = BitConverter.GetBytes(respBytes.Length);

                            server.Write(respLenBytes, 0, 4);
                            server.Write(respBytes, 0, respBytes.Length);
                            server.Flush();
                            StartupLogger.Log($"SingleInstanceService: Sent handshake reply '{response}'.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!_disposed)
                    {
                        StartupLogger.Log($"SingleInstanceService: Pipe server loop error: {ex.Message}");
                        Thread.Sleep(200);
                    }
                }
                finally
                {
                    try
                    {
                        if (server != null)
                        {
                            if (server.IsConnected) server.Disconnect();
                            server.Dispose();
                        }
                    }
                    catch { }
                }
            }

            StartupLogger.Log("SingleInstanceService: IPC Pipe server thread terminated.");
        })
        {
            IsBackground = true,
            Name = "PRISM_IPC_ServerThread"
        };
        _serverThread.Start();
    }

    public void Dispose()
    {
        _disposed = true;

        // Connect dummy client to unblock WaitForConnection if waiting
        try
        {
            using var unblockClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            unblockClient.Connect(50);
        }
        catch { }

        if (_mutex != null && _isPrimaryInstance)
        {
            try { _mutex.ReleaseMutex(); } catch { }
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
