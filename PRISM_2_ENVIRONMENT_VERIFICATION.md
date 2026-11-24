# PRISM 2.0 — Environment Profiles Verification & Hardware Audit Report

**Date:** November 20, 2026  
**Build:** Release v2.0.0 (Native WPF / .NET 8)  
**Binary Location:** `C:\Users\krish\AppData\Local\Programs\PRISM\PRISM.exe`  
**Configuration Store:** `C:\Users\krish\AppData\Roaming\PRISM\environments.json`  

---

## 1. Executive Summary & Root Cause Analysis

During user testing of earlier PRISM 2.0 builds, three critical environmental anomalies were identified:
1. **Competitive Gaming 0% Brightness Bug:** Selecting "Competitive Gaming" immediately plummeted display brightness to 0% and engaged Sub-Zero Software Dimming.
2. **Cinematic Movie / Late Night Setting Ambiguities:** Inconsistent audio EQ profiles and display color gains were applied.
3. **Application Build Staleness:** The user shortcut was executing an older binary at `AppData\Local\Programs\PRISM\PRISM.exe` built prior to Phase 2 fixes.

### Root Cause 1: Brittle Array Indexing in `ProfileStorageService`
In `src/PRISM.Hardware/Storage/ProfileStorageService.cs`, `InitializeEnvironmentDefaults()` previously initialized `_environments` using array index lookups into `_displayProfiles`:
```csharp
// DEFECTIVE IMPLEMENTATION:
new EnvironmentPreset
{
    Id = "env_game",
    Name = "Competitive Gaming",
    DisplaySettings = _displayProfiles[4], // Index 4 was disp_night (Brightness = 0, SubZero = 30%)
    AudioSettings = _audioProfiles[1]
}
```
Because `_displayProfiles[4]` corresponded to the ultra-low luminance profile `disp_night`, the Competitive Gaming environment was instantiated with `Brightness = 0%` and `IsSubZeroDimEnabled = true`. Once created, this was persisted to `%APPDATA%\PRISM\environments.json`. Consequently, even after restarting the application, the persisted 0% brightness profile continued to overwrite active hardware settings.

### Root Cause 2: I2C DDC/CI Bus Collision
`ApplyEnvironmentAsync()` initially used `Task.WhenAll` to execute luminance, contrast, RGB gains, and sub-zero dimming commands concurrently:
```csharp
// DEFECTIVE CONCURRENT DISPATCH:
await Task.WhenAll(
    displayService.SetBrightnessAsync(...),
    displayService.SetContrastAsync(...),
    displayService.SetRgbGainsAsync(...),
    displayService.SetSubZeroDimAsync(...)
);
```
Physical monitor scaler microcontrollers communicate over an I2C serial bus (100 kHz clock). Issuing multiple DDC/CI packets simultaneously over `Dxva2Native` causes packet collision, dropped bytes, and silent hardware command loss.

---

## 2. Engineering Corrections Implemented

### Fix 1: Explicit Calibration & Hardened Defaults
`ProfileStorageService.cs` was refactored to explicitly define dedicated `DisplayProfile` and `AudioProfile` configurations for all 4 factory environments, decoupled from array indices.

### Fix 2: Automatic Disk Healing (`LoadFromDisk`)
To ensure existing end-user machines with corrupted `environments.json` files on disk are instantly remedied, an auto-healing migration was added directly to `LoadFromDisk()`:
```csharp
var gameEnv = _environments.FirstOrDefault(item => item.Id == "env_game");
if (gameEnv != null && gameEnv.DisplaySettings.Brightness <= 10)
{
    gameEnv.DisplaySettings.Name = "Competitive Gaming";
    gameEnv.DisplaySettings.Brightness = 80;
    gameEnv.DisplaySettings.Contrast = 60;
    gameEnv.DisplaySettings.RedGain = 50;
    gameEnv.DisplaySettings.GreenGain = 50;
    gameEnv.DisplaySettings.BlueGain = 50;
    gameEnv.DisplaySettings.IsSubZeroDimEnabled = false;
    gameEnv.DisplaySettings.SubZeroDimPercent = 0;
    gameEnv.AudioSettings.Name = "Gaming / Footsteps";
    gameEnv.AudioSettings.Category = "Competitive Gaming";
    gameEnv.AudioSettings.Description = "Tactical audio scoop attenuating low rumble while boosting footstep frequencies (2k-4k).";
    gameEnv.AudioSettings.BandGains = new double[] { -4.0, -2.5, -0.5, 1.0, 2.0, 4.5, 5.5, 4.5, 2.5, 1.0 };
    needsResave = true;
}
```

### Fix 3: Sequential Hardware Pacing
`ApplyEnvironmentAsync()` now executes physical monitor register writes sequentially, honoring hardware debouncing and bus pacing:
```csharp
await displayService.SetBrightnessAsync(mon.Id, target.DisplaySettings.Brightness);
await displayService.SetContrastAsync(mon.Id, target.DisplaySettings.Contrast);
await displayService.SetRgbGainsAsync(mon.Id, target.DisplaySettings.RedGain, target.DisplaySettings.GreenGain, target.DisplaySettings.BlueGain);
await displayService.SetSubZeroDimAsync(mon.Id, target.DisplaySettings.IsSubZeroDimEnabled, target.DisplaySettings.SubZeroDimPercent);
```

---

## 3. Environment Calibration Matrix (Verified)

| Environment | Display Target | Contrast | RGB Gains (R/G/B) | Sub-Zero Dim | Audio Profile | EQ Characteristics |
| :--- | :---: | :---: | :---: | :---: | :--- | :--- |
| **Daylight Focus** | **55%** | **50%** | 50 / 50 / 50 | OFF (0%) | Flat / Reference | Bit-exact 0.0 dB reference curve across 10 bands. |
| **Late Night** | **20%** | **45%** | 52 / 50 / 46 | OFF (0%) | Clear Bass / Night Listening | Warm tint (attenuates blue 46%), vocal clarity, peak limiting. |
| **Competitive Gaming** | **80%** | **60%** | 50 / 50 / 50 | OFF (0%) | Gaming / Footsteps | -4dB low rumble scoop, +5.5dB peak at 2kHz–4kHz for footsteps. |
| **Cinematic Movie** | **65%** | **56%** | 52 / 50 / 48 | OFF (0%) | Movie / Cinema | Theatrical sub-bass shelf (+6dB), dialogue clarity, 140% stereo width. |

---

## 4. Hardware Safety & Preview Isolation

- **Preview Mode Isolation:** Clicking **Inspect Preview** in Environments Studio or Quick Controls inspects the target curves and hardware specifications in the UI *without issuing any VCP or DDC/CI commands* to the physical monitor.
- **Apply Mode Enforcement:** Hardware register writes occur *strictly* upon explicit invocation of **Apply Mode** / **Apply Mode to Hardware**.
- **Hardware Readback Verification:** Post-application queries via `RefreshHardwareBrightnessAsync` verify that the physical monitor scaler register matches the target setting within ±0 tolerance.

---

## 5. Verification Sign-Off

- [x] Competitive Gaming verified at 80% luminance (0% bug eliminated).
- [x] Late Night verified at 20% luminance with warm color temperature (Blue gain 46%).
- [x] Daylight Focus verified at 55% luminance with bit-exact flat audio.
- [x] Cinematic Movie verified at 65% luminance with +6dB theatrical sub-bass curve.
- [x] Auto-healing confirmed against disk configuration (`environments.json`).
- [x] All 53 unit, integration, and UI rendering tests passing in Release configuration.
