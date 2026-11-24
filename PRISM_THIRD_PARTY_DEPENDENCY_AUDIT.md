# PRISM — Third-Party Dependency & Licensing Audit
## Legal, Security, and Architectural Review

**Document Version:** 2.2.0  
**Scope:** Dependencies, Licenses, Security Footprint, and Distribution Compliance

---

## 1. Direct Software Dependencies

| Component | Version | License | Maintenance Status | Dependencies | Security & Permissions | Commercial Viability |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **CommunityToolkit.Mvvm** | 8.4.2 | MIT License (Microsoft) | Actively maintained (.NET Foundation) | None (Roslyn compile-time source generator) | Zero elevation, zero network access, memory-safe | Approved for commercial redistribution |
| **Microsoft.NETCore.App** | 8.0.x | MIT License (Microsoft) | Actively supported (.NET LTS) | Standard .NET 8 Runtime | Microsoft standard security boundaries | Approved for commercial redistribution |

---

## 2. External Subsystem Integrations

### 2.1 Equalizer APO (Optional Audio Processing Subsystem)
- **Author:** Jonas Thedering
- **Version:** 1.3.2
- **License:** GNU General Public License v2.0 (GPLv2)
- **Integration Architecture:**
  - **Loose File Bridge:** PRISM does **not** link against Equalizer APO DLLs, does not statically link GPL code, and does not redistribute Equalizer APO binaries.
  - PRISM outputs a standalone configuration text file (`prism_dsp.txt`) inside `%APPDATA%\PRISM` or `%PROGRAMFILES%\EqualizerAPO\config`.
  - Equalizer APO reads this configuration dynamically via its built-in `Include` command.
  - This architecture cleanly maintains license independence under the FSF / GPL guidelines (inter-process communication via configuration files across separate programs).
- **User Privacy & Telemetry:** Equalizer APO is strictly offline and local-only; it contains no telemetry, cloud accounts, or advertising.

### 2.2 AutoEq Headphone Measurement Database
- **Author:** Jaakko Pasanen
- **License:** MIT License
- **Integration:**
  - Parametric EQ target curves (e.g. Harman OE 2018, Harman IE 2019) and biquad filter frequencies/gains/Q values for reference headphones.
  - Permissive MIT license allows embedding standard mathematical filter coefficients directly in source code.
  - Proper attribution is maintained in the application documentation and codebase.

---

## 3. Native Windows Operating System APIs (P/Invoke)

All native Windows platform functions invoked by PRISM are standard user-mode components delivered with Windows 10 and Windows 11:

1. **DXVA2 Monitor Configuration API (`dxva2.dll`):**
   - `GetNumberOfPhysicalMonitorsFromHMONITOR`, `GetPhysicalMonitorsFromHMONITOR`, `DestroyPhysicalMonitors`
   - `GetMonitorBrightness`, `SetMonitorBrightness`
   - `GetMonitorContrast`, `SetMonitorContrast`
   - `GetMonitorRedGreenOrBlueGain`, `SetMonitorRedGreenOrBlueGain`
   - `SetMonitorVCPFeature`, `GetVCPFeatureAndVCPFeatureReply`
2. **User32 Display Management (`user32.dll`):**
   - `EnumDisplayMonitors`, `GetMonitorInfo`
   - `EnumDisplaySettingsExW`, `ChangeDisplaySettingsExW`
   - `GetDisplayConfigBufferSizes`, `QueryDisplayConfig`, `DisplayConfigGetDeviceInfo`
   - `RegisterHotKey`, `UnregisterHotKey`
   - `GetForegroundWindow`, `GetWindowRect`, `GetClassName`
3. **GDI32 Color Management (`gdi32.dll`):**
   - `GetICMProfileW`, `GetDeviceGammaRamp`, `SetDeviceGammaRamp`
4. **Core Audio / WASAPI (`ole32.dll`, `mmdevapi.dll`):**
   - `IMMDeviceEnumerator`, `IMMDeviceCollection`, `IMMDevice`, `IAudioEndpointVolume`
   - `IAudioClient`, `IAudioCaptureClient` (Loopback mode for 32-band visualizer)
   - `ISpatialAudioClient` (Spatial Sound capability detection)

---

## 4. Permissions & Elevation Policy

- **Standard Runtime:** Runs strictly in **standard user mode (medium integrity)**. No administrator rights, UAC elevation, or driver installations are required for:
  - Display brightness, contrast, RGB gains (DDC/CI).
  - Software sub-zero dimming.
  - Windows resolution and refresh rate switching.
  - Audio device selection, volume control, channel balance, and visualizer.
  - System tray icon and global hotkeys.
- **Elevation Boundary:** Administrator elevation is only requested by the external third-party installer when registering Equalizer APO's driver hook (`Configurator.exe`), isolated completely from PRISM's runtime lifecycle.
