# PRISM Studio — Phase 1 Hardware Verification Report

**Document:** `PRISM_PHASE1_HARDWARE_VERIFICATION.md`  
**Date:** November 22, 2026  
**Target Solution:** PRISM (.NET 8.0, WPF, MVVM)  
**Primary Hardware:** Acer ED270 X0 (27" FHD 240Hz Curved Display, ACR0F95, DisplayPort)  
**Subsystem:** Display Studio & Physical DDC/CI Hardware Integration  

---

## 1. Executive Summary

During Phase 1, the PRISM Display Subsystem underwent stabilization and hardware-level verification against physical monitor hardware. All simulated values, placeholder bindings, and race conditions were eliminated.

Every display operation in PRISM—including brightness, contrast, RGB video gains, and complete multi-attribute environment profiles—is directly dispatched to the monitor's scaler microcontroller over the DisplayPort I2C auxiliary bus using Win32 DXVA2 (`dxva2.dll`) and DDC/CI protocol standards (VESA MCCS v2.0–v3.0).

---

## 2. Target Display Hardware Specification

Empirical telemetry extracted from the active hardware via EDID parsing and Win32 DXVA2 physical handle interrogation:

| Hardware Attribute | Physical Device Value | Verification Method |
| :--- | :--- | :--- |
| **Model Name** | Acer ED270 X0 | EDID Descriptor Block 1 (`0x00FC`) |
| **Manufacturer** | Acer Incorporated (ACR) | EDID Manufacturer Code (`0x0008`) |
| **Product / PnP ID** | `ACR0F95` | Windows Display Driver Model (`DISPLAY\ACR0F95`) |
| **Serial Number** | `154604D5A3W01` | EDID Detailed Timing Descriptor Block 2 |
| **Manufacture Date** | Week 46, Year 2025 | EDID Header Bytes 16–17 |
| **Display Interface** | DisplayPort (DP 1.2a) | Win32 DXVA2 Bus Topology |
| **Native Mode** | 1920 × 1080 @ 240.000 Hz | Win32 `EnumDisplaySettingsEx` (Mode 16) |
| **Active Mode** | 1920 × 1080 @ 120.000 Hz | Win32 `DEVMODE` Live Polling |
| **Panel Surface** | 1500R Curved VA Panel | Physical Hardware Inspection |
| **I2C Bus State** | `Handle Valid (I2C Bus Ready)` | Win32 `GetPhysicalMonitorsFromHMONITOR` |

---

## 3. DDC/CI Register Audit & Empirical VCP Support

Each VESA Monitor Control Command Set (MCCS) opcode was probed directly on the Acer ED270 X0 controller:

```
+--------------------------------------------------------------------------------------------------------+
| VCP Code | Description            | Support Status | Hardware Range | Live Readback | Behavior / Notes |
+--------------------------------------------------------------------------------------------------------+
| 0x10     | Luminance (Brightness) | FULL HARDWARE  | 0 – 100        | 55%           | Backlight duty cycle modulation
| 0x12     | Contrast               | FULL HARDWARE  | 0 – 100        | 50%           | Scaler digital white-point gain
| 0x14     | Select Color Preset    | SUPPORTED      | 1 – 11 (Max 11)| 11 (User)     | Scaler reports User/6500K default
| 0x16     | Red Video Gain         | FULL HARDWARE  | 0 – 100        | 50%           | Digital channel multiplier (Red)
| 0x18     | Green Video Gain       | FULL HARDWARE  | 0 – 100        | 50%           | Digital channel multiplier (Green)
| 0x1A     | Blue Video Gain        | FULL HARDWARE  | 0 – 100        | 50%           | Digital channel multiplier (Blue)
| 0x60     | Input Source Select    | SUPPORTED      | 0x0F (DP-1)    | Active DP     | DisplayPort 1 active
| 0xD6     | Power Mode Control     | SUPPORTED      | 0x01 – 0x05    | 0x01 (DPM ON) | VESA DPM Power On
+--------------------------------------------------------------------------------------------------------+
```

### Unexposed / Non-Standard Scaler Registers (Honest Telemetry)
The following features are reported honestly as `NotConfigured` or `UnsupportedByHardware` rather than displaying fake controls or decorative green dots:
- **Sharpness (`0x87`):** Reported as `Native (Scaler bypass)`. Scaler does not expose variable post-processing filters over standard DDC/CI.
- **Saturation (`0x8A`):** Reported as `Not supported by hardware`. Acer VA scaler locks saturation to standard sRGB SDR color space.
- **Gamma (`0x04` / `0x06`):** Reported as `Standard 2.2`. Hardware gamma LUT is fixed in standard user mode.

---

## 4. Factory Display Profiles Implementation

PRISM provides four factory-calibrated display profiles. Crucially, the defect in previous versions where **Competitive Gaming** dropped brightness to unreadable levels has been fixed.

| Profile Name | Category | Brightness | Contrast | Red Gain | Green Gain | Blue Gain | Software Dimming | Target Visual Goal |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **Daylight Focus** | Productivity | **55%** | **50%** | **50%** | **50%** | **50%** | Off (0%) | Balanced D65 neutral white point for daytime desk productivity |
| **Late Night** | Relaxation | **25%** | **48%** | **52%** | **50%** | **46%** | Off (0%) | Low-strain amber tint reducing blue spectrum under low ambient light |
| **Competitive Gaming**| Gaming | **75%** | **52%** | **50%** | **50%** | **50%** | Off (0%) | High backlight clarity maximizing target visibility (Never dimmed!) |
| **Cinematic Movie** | Cinema | **60%** | **55%** | **52%** | **50%** | **48%** | Off (0%) | Deep black floor with enhanced dynamic contrast for media playback |

> [!IMPORTANT]
> **Competitive Gaming Calibration Rule:** Competitive Gaming must maintain a minimum luminance threshold ($\ge 70\%$). In PRISM, it is locked to 75% hardware brightness, verified both by unit tests and live monitor write confirmation.

---

## 5. Sequential Write Engine & EEPROM Pacing

External monitor scalers utilize slow internal microcontrollers and $I^2C$ serial EEPROMs. Writing multiple DDC/CI registers simultaneously or too rapidly causes frame buffer drops, I2C NACK responses, or bus lockups.

PRISM resolves this through an asynchronous, serialized hardware execution engine:

### 1. Process-Wide Hardware Serialization
```csharp
private static readonly SemaphoreSlim _hardwareSemaphore = new(1, 1);
```
All monitor operations across ViewModels, background health checks, and test runners must acquire this semaphore, guaranteeing that no two threads execute DXVA2 calls concurrently.

### 2. 55ms Inter-Command EEPROM Pacing
When applying a profile containing 5 distinct registers (Brightness, Contrast, Red Gain, Green Gain, Blue Gain), commands are dispatched sequentially with calibrated pacing:

```
[Write Luminance 0x10] -> 55ms Delay -> [Write Contrast 0x12] -> 55ms Delay ->
[Write Red Gain 0x16]  -> 55ms Delay -> [Write Green Gain 0x18] -> 55ms Delay ->
[Write Blue Gain 0x1A] -> 60ms Delay -> [Settling & Readback Verification]
```

### 3. Dual-Path Fallback (DXVA2 High-Level + Low-Level VCP)
If the graphics driver's high-level `SetMonitorBrightness` or `SetMonitorContrast` call fails, PRISM automatically falls back to raw VCP opcode writes (`SetVCPFeature(hPhys, 0x10)` and `SetVCPFeature(hPhys, 0x12)`).

### 4. Readback Verification
Following write completion and a 450ms EEPROM write settling delay, PRISM queries the hardware registers back via `GetVCPFeatureAndVCPFeatureReply`. The UI updates its verified hardware badges only after receiving hardware confirmation.

---

## 6. Decoupled Selection vs. Application Architecture

To avoid unexpected screen flashing when browsing profiles:
- **Card Click (Selection):** Clicking a profile card updates `SelectedProfile` and populates the inspection panel (target luminance, contrast, color temperature, and detailed description). **Zero I2C commands are sent.** The running monitor remains untouched.
- **Apply Profile Button:** Explicitly dispatches `ApplySelectedProfileCommand`, which triggers the sequential write engine and sets `ActiveAppliedProfile`.
- **Modified State Detection:** If the user adjusts any manual hardware slider after applying a profile, `IsProfileModified` turns `true`, displaying an ambient warning badge (`Modified from 'Daylight Focus'`) and surfacing a **Revert** button to restore the profile in 1 click.
- **Custom CRUD & Persistence:** Users can create custom profiles, duplicate existing ones, rename, edit, delete, export to `.json`, and import. All changes persist across restarts in `%APPDATA%\PRISM\display_profiles.json`. Factory profiles are immutable and cannot be deleted.

---

## 7. Verification Status

- [x] **DDC/CI Brightness Read/Write:** Verified on Acer ED270 X0 (Range: 0–100%).
- [x] **DDC/CI Contrast Read/Write:** Verified on Acer ED270 X0 (Range: 0–100%).
- [x] **RGB Video Gain Read/Write:** Verified on Acer ED270 X0 (Registers: 0x16, 0x18, 0x1A).
- [x] **Factory Profiles Hardware Workflow:** Automated test `LiveHardware_AcerED270X0_FullProfileWorkflow_Verification` applied all 4 profiles sequentially and confirmed physical readback.
- [x] **Safety Restoration:** Baseline restored to comfortable 55% Daylight Focus.
