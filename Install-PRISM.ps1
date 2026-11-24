# PRISM Windows Installation Script
# Installs PRISM to %LOCALAPPDATA%\Programs\PRISM with Start Menu & Desktop Shortcuts

param(
    [switch]$NoDesktopShortcut,
    [switch]$LaunchAfterInstall
)

$ErrorActionPreference = "Stop"

$installDir = "$env:LOCALAPPDATA\Programs\PRISM"
$sourceDir = "$PSScriptRoot\publish\PRISM"

if (-not (Test-Path $sourceDir)) {
    $sourceDir = "$PSScriptRoot\..\publish\PRISM"
}

if (-not (Test-Path "$sourceDir\PRISM.exe")) {
    Write-Error "Cannot locate PRISM publish artifacts. Please run dotnet publish first."
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " PRISM Display & Audio Studio — Windows Installer" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Stop any currently running PRISM process
$runningProcesses = Get-Process -Name "PRISM" -ErrorAction SilentlyContinue
if ($runningProcesses) {
    Write-Host "[*] Closing existing PRISM processes..." -ForegroundColor Yellow
    $runningProcesses | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# 2. Create Target Installation Directory
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# 3. Copy Application Files & Assets
Write-Host "[*] Installing PRISM binaries to $installDir..." -ForegroundColor Cyan
Copy-Item -Path "$sourceDir\*" -Destination $installDir -Recurse -Force

# Copy uninstaller script to installation dir
Copy-Item -Path "$PSScriptRoot\Uninstall-PRISM.ps1" -Destination "$installDir\Uninstall-PRISM.ps1" -Force -ErrorAction SilentlyContinue

# 4. Create Start Menu Shortcut
$startMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$startMenuLnk = "$startMenuDir\PRISM.lnk"
$wshell = New-Object -ComObject WScript.Shell

Write-Host "[*] Creating Start Menu shortcut at $startMenuLnk..." -ForegroundColor Cyan
$shortcut = $wshell.CreateShortcut($startMenuLnk)
$shortcut.TargetPath = "$installDir\PRISM.exe"
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = "PRISM — Premium Windows Display & Audio Studio"
$shortcut.IconLocation = "$installDir\Assets\PRISM.ico,0"
$shortcut.Save()

# 5. Create Desktop Shortcut across all active Desktop directories
if (-not $NoDesktopShortcut) {
    $desktopDirs = @(
        [Environment]::GetFolderPath("Desktop"),
        "$env:USERPROFILE\Desktop",
        "$env:USERPROFILE\OneDrive\Desktop"
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

    foreach ($dDir in $desktopDirs) {
        if (Test-Path $dDir) {
            $desktopLnk = "$dDir\PRISM.lnk"
            Write-Host "[*] Creating Desktop shortcut at $desktopLnk..." -ForegroundColor Cyan
            $dShortcut = $wshell.CreateShortcut($desktopLnk)
            $dShortcut.TargetPath = "$installDir\PRISM.exe"
            $dShortcut.WorkingDirectory = $installDir
            $dShortcut.Description = "PRISM — Premium Windows Display & Audio Studio"
            $dShortcut.IconLocation = "$installDir\Assets\PRISM.ico,0"
            $dShortcut.Save()
        }
    }
}

# 6. Register Windows Uninstall Metadata in Registry
$regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PRISM"
Write-Host "[*] Registering application in Windows Apps & Features..." -ForegroundColor Cyan
if (-not (Test-Path $regKey)) {
    New-Item -Path $regKey -Force | Out-Null
}

Set-ItemProperty -Path $regKey -Name "DisplayName" -Value "PRISM Display & Audio Studio"
Set-ItemProperty -Path $regKey -Name "DisplayVersion" -Value "2.0.0"
Set-ItemProperty -Path $regKey -Name "Publisher" -Value "PRISM Software"
Set-ItemProperty -Path $regKey -Name "DisplayIcon" -Value "$installDir\Assets\PRISM.ico"
Set-ItemProperty -Path $regKey -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $regKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$installDir\Uninstall-PRISM.ps1`""

Write-Host ""
Write-Host "[+] PRISM installation completed successfully!" -ForegroundColor Green
Write-Host "    Installed Executable: $installDir\PRISM.exe" -ForegroundColor White
Write-Host "    Start Menu Shortcut:  $startMenuLnk" -ForegroundColor White
Write-Host "    Desktop Shortcut:     $desktopLnk" -ForegroundColor White
Write-Host ""

if ($LaunchAfterInstall) {
    Write-Host "[*] Launching PRISM..." -ForegroundColor Cyan
    Start-Process -FilePath "$installDir\PRISM.exe" -WorkingDirectory $installDir
}
