param(
    [string]$ViewName = "Audio",
    [string]$OutputFile = "PRISM_AudioStudio_Overhaul.png"
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$pinvoke = @"
using System;
using System.Runtime.InteropServices;

public class Win32Gui
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
"@
Add-Type -TypeDefinition $pinvoke -ErrorAction SilentlyContinue

# 1. Bring PRISM window to front
$prismProc = Get-Process -Name "PRISM" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($prismProc -and $prismProc.MainWindowHandle -ne [IntPtr]::Zero) {
    [Win32Gui]::ShowWindow($prismProc.MainWindowHandle, 9) # SW_RESTORE
    [Win32Gui]::SetForegroundWindow($prismProc.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 500
}

# 2. Navigate via IPC if requested
if ($ViewName -eq "Display") {
    & "$env:LOCALAPPDATA\Programs\PRISM\PRISM.exe" --display
    Start-Sleep -Milliseconds 700
} elseif ($ViewName -eq "Audio") {
    & "$env:LOCALAPPDATA\Programs\PRISM\PRISM.exe" --audio
    Start-Sleep -Milliseconds 700
} elseif ($ViewName -eq "Home") {
    & "$env:LOCALAPPDATA\Programs\PRISM\PRISM.exe" --home
    Start-Sleep -Milliseconds 700
}

# 3. Capture Window or Primary Screen
$rect = New-Object Win32Gui+RECT
if ($prismProc -and $prismProc.MainWindowHandle -ne [IntPtr]::Zero -and [Win32Gui]::GetWindowRect($prismProc.MainWindowHandle, [ref]$rect)) {
    $width = [Math]::Max(100, $rect.Right - $rect.Left)
    $height = [Math]::Max(100, $rect.Bottom - $rect.Top)
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $width, $height))
} else {
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
}

$bmp.Save($OutputFile, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bmp.Dispose()

Write-Host "[+] Captured screenshot to $OutputFile" -ForegroundColor Green
