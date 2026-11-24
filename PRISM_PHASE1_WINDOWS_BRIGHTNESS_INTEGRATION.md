# PRISM Studio — Phase 1 Windows Brightness Integration Report

**Document:** `PRISM_PHASE1_WINDOWS_BRIGHTNESS_INTEGRATION.md`  
**Date:** November 22, 2026  
**Target Architecture:** Windows 10/11 x64, WDDM 3.x, ACPI 6.0, VESA DDC/CI  
**Subsystem:** Windows Native OS Synchronization & Capability Discovery  

---

## 1. Objective & Technical Scope

A key objective of Phase 1 was to investigate native Windows brightness synchronization (such as Windows Quick Settings `Win+A` and Windows Mobility Center) and establish whether PRISM could synchronize with the OS slider without compromising hardware stability.

This document details the architectural findings, OS API investigations, physical bus topology probes, and PRISM's honest capability detection service.

---

## 2. Windows Brightness APIs & Architectural Hierarchy

Windows provides three distinct interfaces for display brightness control, each targeting different hardware layers:

```
                                  [ Windows OS Layer ]
                                           │
         ┌─────────────────────────────────┼─────────────────────────────────┐
         ▼                                 ▼                                 ▼
   [ WinRT API ]                    [ WMI Subsystem ]                 [ Win32 DXVA2 ]
BrightnessOverride               WmiMonitorBrightness             Low-Level Physical Monitor
 (UWP / Windows Apps)          WmiMonitorBrightnessMethods         (dxva2.dll / User32.dll)
         │                                 │                                 │
         │ (Requires ACPI)                 │ (Requires ACPI)                 │ (Physical I2C)
         ▼                                 ▼                                 ▼
┌──────────────────┐             ┌──────────────────┐             ┌──────────────────────┐
│  Internal Panel  │             │  Internal Panel  │             │   External Monitor   │
│ (eDP / LVDS / MIPI)│           │ (eDP / LVDS / MIPI)│           │ (DisplayPort / HDMI) │
└──────────────────┘             └──────────────────┘             └──────────────────────┘
```

### 1. WMI (`root\wmi` namespace)
- **Classes:** `WmiMonitorBrightness` (query current level) and `WmiMonitorBrightnessMethods` (`WmiSetBrightness` method).
- **Driver Requirement:** Only exposed by graphics drivers implementing the WDDM Internal Backlight Interface (`DXGKDDI_SET_DISPLAY_BACKLIGHT` or ACPI `_BCM`/`_BQC` methods).
- **Target Devices:** Laptops, tablets, 2-in-1 devices, All-in-One integrated screens.

### 2. WinRT Display API (`Windows.Graphics.Display`)
- **Classes:** `BrightnessOverride` and `DisplayInformation`.
- **Limitation:** Internally proxies down to the same ACPI/WDDM internal backlight stack. If WMI returns 0 instances, WinRT throws `Platform::COMException` or reports `IsSupported = false`.

### 3. Win32 Low-Level DXVA2 (`dxva2.dll`)
- **APIs:** `GetMonitorBrightness`, `SetMonitorBrightness`, `GetVCPFeatureAndVCPFeatureReply`, `SetVCPFeature`.
- **Target Devices:** External desktop monitors connected via DisplayPort, HDMI, DVI, or VGA over the I2C serial line.

---

## 4. Empirical Hardware Topology Probe

During system startup, PRISM probes the host machine's physical display topology using WMI and DXVA2:

```powershell
Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorBrightness
# Result: 0 instances returned (ManagementException: NotFound)

Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorID
# InstanceName: DISPLAY\ACR0F95\5&1d0456d1&0&UID260_0
# UserFriendlyName: Acer ED270 X0
```

### Topological Findings:
1. **Host Form Factor:** Desktop Workstation / Dedicated GPU configuration.
2. **Total Displays Connected:** Exactly 1 physical display (`DISPLAY\ACR0F95`).
3. **Internal Displays:** **0** (no laptop eDP panel or ACPI-managed backlight).
4. **External Displays:** **1** (Acer ED270 X0 over physical DisplayPort).

---

## 4. Architectural Analysis: Why Windows Quick Settings Does Not Support External Monitors

Users frequently ask why the Windows 10/11 Quick Settings flyout (`Win+A`) or the Settings app (`System > Display > Brightness`) has no brightness slider for external desktop monitors.

The root cause is a fundamental Microsoft Windows Display Driver Model (WDDM) design choice:

1. **ACPI vs. I2C Separation:** Windows Quick Settings interacts exclusively with the ACPI firmware layer via the GPU driver. It expects hardware ACPI control methods (`_BCM` - Backlight Control Method, `_BQC` - Backlight Query Current).
2. **Timing & Latency Constraints:** ACPI writes execute in microseconds directly on the motherboard platform controller hub (PCH). In contrast, DDC/CI writes over external DisplayPort aux channels require $I^2C$ packetization, handshake acknowledgments, and up to 50–100ms per transaction.
3. **Microsoft Architectural Policy:** Microsoft deliberately omits external DDC/CI monitors from the OS Quick Settings brightness slider to avoid UI thread hangs and system-wide stalls caused by unresponsive external monitor scalers.
4. **Third-Party Virtual Drivers:** While third-party utilities sometimes create fake virtual monitor drivers (`vdev`) or ACPI hook shims to inject a slider into Windows Quick Settings, these virtual bus drivers frequently trigger GPU driver crashes, Blue Screens (BSODs), and DisplayPort link renegotiation drops.

---

## 5. PRISM's Capability Detection Engine

Rather than faking synchronization or installing fragile kernel-level virtual drivers, PRISM implements `IWindowsBrightnessService` and `WindowsBrightnessIntegrationService` to honestly assess OS capability:

```csharp
public class WindowsBrightnessCapability
{
    public bool IsInternalDisplay { get; set; }
    public bool IsQuickSettingsSyncSupported { get; set; }
    public WindowsBrightnessSupportType SupportType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string DeviceInstanceId { get; set; } = string.Empty;
    public int CurrentWindowsBrightness { get; set; } = -1;
    public string RecommendedInterface { get; set; } = string.Empty;
    public string TechnicalLimitationExplanation { get; set; } = string.Empty;
}
```

### Telemetry Produced for Acer ED270 X0:

```json
{
  "DisplayName": "Acer ED270 X0 (DisplayPort)",
  "DeviceInstanceId": "DISPLAY\\ACR0F95",
  "IsInternalDisplay": false,
  "IsQuickSettingsSyncSupported": false,
  "SupportType": "UnsupportedExternalDdcCi",
  "CurrentWindowsBrightness": -1,
  "RecommendedInterface": "Hardware DDC/CI (I2C VCP Opcode 0x10)",
  "TechnicalLimitationExplanation": "External DisplayPort monitors communicate via physical DDC/CI (I2C VCP opcode 0x10). Windows Quick Settings natively requires ACPI WDDM internal backlight support (WmiMonitorBrightness), which Microsoft does not expose for external desktop monitors. PRISM controls hardware luminance directly via DXVA2."
}
```

---

## 6. PRISM Quick Flyout: The Native Desktop Solution

Because Windows cannot natively control external monitor brightness from Quick Settings, PRISM provides its own native, instantaneous desktop Quick Flyout:

- **Global Hotkey:** `Win + Alt + P` (or single-click on the system tray icon).
- **Physical Luminance Control:** True hardware luminance modulation via VCP `0x10`.
- **Master Audio Volume:** WASAPI hardware endpoint attenuation.
- **One-Tap Environments:** Instant access to Daylight Focus, Late Night, Competitive Gaming, and Cinematic Movie.
- **Microsecond Response:** Native WPF with Win32 message loop—zero Electron overhead and zero driver hooking.

---

## 7. Verification Status

- [x] **WMI Subsystem Probe:** Executed cleanly; mapped `ManagementStatus.NotFound` to `UnsupportedExternalDdcCi`.
- [x] **Honest Capability Reporting:** Automated tests `WindowsBrightnessService_ProbesTopology_HonestReportingForExternalMonitor` and `WindowsBrightnessService_SetBrightness_FailsSafelyWhenUnsupported` passed.
- [x] **No False Synchronization:** PRISM UI honestly displays `External DisplayPort • Controlled via Physical DDC/CI (0x10)` and does not claim OS sync is active.
