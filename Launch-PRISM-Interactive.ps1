# Helper script to launch PRISM onto the user's interactive Windows desktop (WinSta0\Default)
param(
    [string]$Arguments = "--home"
)

$csSource = @"
using System;
using System.Runtime.InteropServices;

namespace PRISM.Launcher
{
    public static class InteractiveProcess
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        public static int Start(string exePath, string workingDir, string arguments)
        {
            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = "WinSta0\\Default";

            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            string cmdLine = "\"" + exePath + "\" " + (arguments ?? "");

            // DETACHED_PROCESS = 0x00000008, CREATE_NEW_PROCESS_GROUP = 0x00000200
            uint flags = 0x00000008 | 0x00000200;

            bool success = CreateProcess(
                null,
                cmdLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                flags,
                IntPtr.Zero,
                workingDir,
                ref si,
                out pi);

            if (!success)
            {
                int err = Marshal.GetLastWin32Error();
                throw new Exception("CreateProcess failed with Win32 error code: " + err);
            }

            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return pi.dwProcessId;
        }
    }
}
"@

if (-not ([System.Management.Automation.PSTypeName]'PRISM.Launcher.InteractiveProcess').Type) {
    Add-Type -TypeDefinition $csSource -Language CSharp
}

$exePath = "$env:LOCALAPPDATA\Programs\PRISM\PRISM.exe"
$workingDir = "$env:LOCALAPPDATA\Programs\PRISM"

Write-Host "Launching PRISM into interactive desktop 'WinSta0\Default'..." -ForegroundColor Cyan
$pidResult = [PRISM.Launcher.InteractiveProcess]::Start($exePath, $workingDir, $Arguments)
Write-Host "Launched process with PID: $pidResult" -ForegroundColor Green

Start-Sleep -Seconds 3

$p = Get-Process -Id $pidResult -ErrorAction SilentlyContinue
if ($p) {
    Write-Host "Process State:" -ForegroundColor Cyan
    Write-Host "  PID:              $($p.Id)"
    Write-Host "  MainWindowHandle: 0x$($p.MainWindowHandle.ToString('X'))"
    Write-Host "  MainWindowTitle:  $($p.MainWindowTitle)"
    Write-Host "  Responding:       $($p.Responding)"
    Write-Host "  WorkingSet64:     $([math]::Round($p.WorkingSet64 / 1MB, 2)) MB"
} else {
    Write-Host "Process $pidResult not found in process table!" -ForegroundColor Red
}
