# PRISM Studio — Phase 1 Complete Project Audit

**Document:** `PRISM_PHASE1_FUNCTIONALITY_AUDIT.md`  
**Date:** November 22, 2026  
**Target Solution:** PRISM (.NET 8.0 Windows x64, WPF, MVVM)  
**Hardware Baseline:** Acer ED270 X0 (DisplayPort, ACR0F95), Cosmic Byte Proteus (WASAPI 48kHz Stereo)

---

## 1. Architectural Overview

PRISM is structured across four primary projects adhering to Clean Architecture principles:

```
PRISM.sln
├── src/
│   ├── PRISM.Core/            # Domain models, Result<T> monads, contracts, interfaces
│   ├── PRISM.Hardware/        # DDC/CI, CoreAudio WASAPI, Equalizer APO, Windows hooks
│   └── PRISM.App/             # Native WPF UI, MVVM ViewModels, Midnight Graphite styles
└── tests/
    └── PRISM.Tests/           # Unit, integration, and live hardware test suites
```

### Core Subsystems & Responsibilities

| Subsystem | Primary Implementation | Contract / Interface | Hardware / OS Target |
| :--- | :--- | :--- | :--- |
| **Display Control** | `DdcCiMonitorService.cs` | `IDisplayService` | Win32 DXVA2 / I2C Bus (`dxva2.dll`) |
| **Display Resolutions** | `DisplayResolutionService.cs` | `IDisplayResolutionService` | Win32 `EnumDisplaySettingsEx` / `ChangeDisplaySettingsEx` |
| **Color & SDR Telemetry**| `DisplayAdvancedColorService.cs` | `IDisplayAdvancedColorService`| DXGI Output6 / Windows Advanced Color |
| **Audio Core & Endpoints**| `CoreAudioDeviceService.cs` | `IAudioService` | Windows Core Audio MMDeviceAPI / WASAPI |
| **Audio DSP Audition** | `PrismAudioEngine.cs` | Built-in Real-time DSP | WinMM `waveOut` / 10-Band Biquad Engine |
| **System-wide Audio DSP**| `EqualizerApoBridgeService.cs`| `IEqualizerApoBridge` | Equalizer APO Config Engine (`prism_dsp.txt`) |
| **Spatial Audio** | `SpatialAudioService.cs` | `ISpatialAudioService` | Windows Sonic / Dolby Atmos MMDevice Props |
| **Audio Visualizer** | `WasapiLoopbackVisualizer.cs` | FFT Spectrum Visualizer | WASAPI Loopback Capture + 32-band FFT |
| **Profile & Settings** | `ProfileStorageService.cs` | `IProfileService`, `IEnvironmentService` | JSON Persistence (`%APPDATA%/PRISM`) |
| **Shell Integration** | `GlobalHotkeyService.cs`, `WindowsStartupService.cs`, `SingleInstanceService.cs` | Native Windows Hooks | Win32 RegisterHotKey, Named Pipes, HKCU Run |

---

## 2. In-Depth Subsystem Audit & Identified Issues

### A. Display Subsystem (`DdcCiMonitorService`)
- **Strengths:** 
  - Working DDC/CI communication for Luminance (`0x10`), Contrast (`0x12`), and RGB Gains (`0x16`/`0x18`/`0x1A`).
  - Acer ED270 X0 detected and responsive over DisplayPort.
  - Sequential write pipeline with 55ms inter-command pacing and readback verification.
- **Identified Deficiencies & Vulnerabilities:**
  1. *Hardware Semaphore Scope (Resolved in previous turn):* Semaphore was an instance member rather than static, risking I2C collision when multiple services probed the bus. Now static process-wide.
  2. *Color Preset 0x14:* Monitor reports standard 6500K default. When switching between color presets, some scalers reset custom RGB gains without notifying the OS. Needs explicit handling in Display Studio.
  3. *Error Reporting Fidelity:* In some edge cases, an unsuccessful I2C reply was logged to debug output instead of bubbling an explicit `Result.Failure` to the UI status indicator.

### B. Windows Native Brightness Integration (New Requirement)
- **Current State:** 
  - PRISM solely controls display brightness via Win32 DXVA2 (`SetMonitorBrightness`).
  - No capability detection exists to check whether Windows Quick Settings / WMI exposes native brightness control.
- **Hardware Investigation Findings:**
  - Acer ED270 X0 is an **external desktop monitor** connected via DisplayPort to a dedicated GPU.
  - Windows WMI `WmiMonitorBrightness` and `WmiMonitorBrightnessMethods` return **zero instances**.
  - Windows Quick Settings brightness slider is architecturally restricted by Microsoft to internal laptop panels (`WDDM_MONITOR_DEVICE_TYPE_INTEGRATED` via ACPI `_BCM`/`_BQC`).
  - Windows does **not** expose a native Quick Settings brightness slider for external desktop DisplayPort monitors.
- **Action Plan:**
  - Implement `WindowsBrightnessIntegrationService` with genuine capability detection.
  - Honestly report `Unavailable / UnsupportedByHardware` for the external Acer monitor.
  - Document the exact Windows architectural limitation preventing external monitor Quick Settings synchronization.
  - Never fake synchronization or inject mock controls into Windows Quick Settings.

### C. Audio Subsystem & Equalizer APO Verification
- **Current State:**
  - `CoreAudioDeviceService` enumerates WASAPI render devices correctly.
  - Master volume, mute, and channel balance attenuate audio reliably.
  - `EqualizerApoBridgeService` generates syntactically valid `prism_dsp.txt` configurations and updates `config.txt`.
- **Identified False-Positive Status:**
  - `CheckStatus()` previously claimed `IsConfiguredForActiveDevice = true` solely because `C:\Program Files\EqualizerAPO\config\config.txt` existed!
  - **Live Registry Probe Revealed:** In `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{guid}\FxProperties`, no audio endpoint currently has Equalizer APO GUID `{EACD2258-FC30-4D54-B746-4EE238E66858}` registered.
  - While Equalizer APO 1.4.2 is installed, the user has not checked the active playback device ("cosmic byte proteus god mode") in Equalizer APO `Configurator.exe`.
  - **Required Fix:** Detect actual endpoint registry hooking. Honestly report status as `Limited` with clear user guidance to run `Configurator.exe`. Do not claim system audio is currently being altered by EQ presets.

### D. Environments Subsystem
- **Current State:**
  - Four curated environments: Daylight Focus, Late Night, Competitive Gaming, Cinematic Movie.
  - Coordinated display profile and audio preset switching.
- **Identified Vulnerabilities:**
  - Preview mode must be strictly isolated: inspecting or previewing an environment must not mutate hardware registers or write to disk.
  - Must ensure fallback safety if an audio preset or display profile referenced by an environment is deleted or modified.

### E. Health Check & Diagnostics
- **Current State:**
  - Basic textual diagnostic dumps exist in `SettingsViewModel` and `DisplayStudioViewModel`.
- **Identified Gap:**
  - Section 7 requires a genuine structured Health Check reporting real component states:
    - Display connection
    - DDC/CI communication
    - Brightness read/write
    - Contrast read/write
    - RGB read/write
    - Windows brightness integration availability
    - Active audio endpoint
    - Audio volume control
    - Equalizer APO installation & endpoint activation
    - DSP processing status
    - Global shortcut registration
    - Profile storage health
    - Startup & tray status
  - Categorized statuses (`Healthy`, `Limited`, `Unavailable`, `Error`, `Not Tested`).
  - Real probes with zero decorative or hardcoded green dots.

---

## 3. Stabilization & Implementation Roadmap

```
Phase 1 Execution Plan
├── 1. Windows Brightness Capability Service
│   ├── Query WmiMonitorBrightness / WmiMonitorBrightnessMethods
│   ├── Enumerate internal vs external displays
│   └── Honest reporting: external DP monitors use DDC/CI; Windows Quick Settings unsupported
├── 2. Audio Pipeline & True Endpoint APO Hook Detection
│   ├── Inspect MMDevices FxProperties for Equalizer APO GUID
│   └── Report honest status (Installed vs Hooked vs Inactive)
├── 3. Health Check Engine (Section 7)
│   ├── Implement IHealthCheckService & DiagnosticsProbe
│   ├── 13 real component checks with Healthy / Limited / Unavailable / Error
│   └── Wire into Settings & Diagnostics UI + Copy Diagnostic Report
├── 4. Automated Tests
│   ├── Windows brightness capability tests
│   ├── Audio endpoint APO hook tests
│   ├── Health check unit tests
│   └── Verification that monitor baseline is preserved
└── 5. Final Deliverables & Release Build
    ├── PRISM_PHASE1_HARDWARE_VERIFICATION.md
    ├── PRISM_PHASE1_WINDOWS_BRIGHTNESS_INTEGRATION.md
    ├── PRISM_PHASE1_AUDIO_VERIFICATION.md
    ├── PRISM_PHASE1_TEST_RESULTS.md
    └── Release Publish & Installation
```
