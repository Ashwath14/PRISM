# PRISM Windows Uninstaller Script
# Removes PRISM from %LOCALAPPDATA%\Programs\PRISM and cleans up shortcuts

param(
    [switch]$PurgeUserData
)

$ErrorActionPreference = "SilentlyContinue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " PRISM Display & Audio Studio — Uninstaller" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Terminate running PRISM process
$running = Get-Process -Name "PRISM" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[*] Terminating active PRISM processes..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# 2. Remove Shortcuts
$startMenuLnk = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\PRISM.lnk"
if (Test-Path $startMenuLnk) {
    Write-Host "[*] Removing Start Menu shortcut..." -ForegroundColor Cyan
    Remove-Item -Path $startMenuLnk -Force
}

$desktopLnk = "$([Environment]::GetFolderPath('Desktop'))\PRISM.lnk"
if (Test-Path $desktopLnk) {
    Write-Host "[*] Removing Desktop shortcut..." -ForegroundColor Cyan
    Remove-Item -Path $desktopLnk -Force
}

# 3. Remove Windows Registry Uninstall Entry
$regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PRISM"
if (Test-Path $regKey) {
    Write-Host "[*] Removing registry registration..." -ForegroundColor Cyan
    Remove-Item -Path $regKey -Recurse -Force
}

# 4. Remove User Data if explicitly requested
$userDataDir = "$env:LOCALAPPDATA\PRISM"
if ($PurgeUserData -and (Test-Path $userDataDir)) {
    Write-Host "[*] Purging user profiles and configuration at $userDataDir..." -ForegroundColor Yellow
    Remove-Item -Path $userDataDir -Recurse -Force
} else {
    Write-Host "[i] User profiles and environment settings preserved at $userDataDir" -ForegroundColor Gray
}

# 5. Remove Program Files Directory
$installDir = "$env:LOCALAPPDATA\Programs\PRISM"
if (Test-Path $installDir) {
    Write-Host "[*] Removing application files at $installDir..." -ForegroundColor Cyan
    Remove-Item -Path $installDir -Recurse -Force
}

Write-Host ""
Write-Host "[+] PRISM has been successfully uninstalled from your PC." -ForegroundColor Green
Write-Host ""
