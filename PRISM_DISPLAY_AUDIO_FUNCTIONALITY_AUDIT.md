# PRISM — Display & Audio Functionality Audit Matrix
**Phase:** 1 — Complete Functionality Investigation  
**Date:** November 20, 2026  
**Audited Systems:** Display Studio, Audio Studio, Windows GDI/CCD, Core Audio, WASAPI, Equalizer APO Bridge  

---

## 1. Executive Findings & Problem Root Causes

| Problem Area | Observed Symptom | Technical Root Cause | Proposed Solution |
| :--- | :--- | :--- | :--- |
| **Resolution Management** | Resolutions visible in dropdown, but cannot be selected or applied. | `DisplayStudioViewModel.cs` parsed resolution text with `SelectedResolution.Split(' ')[0].Split('x')`. Because `DisplayModeInfo.ResolutionText` formats resolutions using the Unicode multiplication symbol `×` (`\u00D7`) separated by spaces (e.g. `"1920 × 1080 (Native)"`), `Split(' ')[0]` returned `"1920"`, causing `parts.Length == 2` to fail silently. | Replace fragile string splitting with a strongly-typed `ResolutionOption` model; add robust regex parsing fallback (`@"(\d+)\s*[x×X]\s*(\d+)"`); pass exact `DeviceName` (`\\.\DISPLAY1`) to `ChangeDisplaySettingsEx`. |
| **Refresh-Rate Management** | Refresh rates cannot be selected or applied; monitor locked to default. | `PopulateRefreshRatesForSelectedResolution` failed on the same Unicode `×` string mismatch. Also, `AvailableRefreshRates.Clear()` triggered null/zero selection changes that desynchronized ComboBox bindings. Hardcoded 240Hz assumed while physical Acer ED270 X0 is active at 200Hz. | Populate genuine refresh rates associated with the selected resolution; maintain current rate when valid; support immediate testing with safety confirmation countdown (15s auto-revert); read back actual active rate via `EnumDisplaySettings`. |
| **Audio Presets & DSP Noticeability** | Presets did not produce noticeable auditory differences; unclear if DSP is working. | Equalizer APO is not installed on the host system (`C:\Program Files\EqualizerAPO` not found). The app generated config files to disk, but Windows Core Audio rendered raw audio without DSP. | Clearly distinguish: (1) In-App Real-Time DSP Audition Engine (using biquad IIR filters in-process so every preset is 100% audibly distinct during A/B testing), (2) System-Wide Equalizer APO status ("Not Installed"), with explicit guidance. |
| **A/B Comparison & Bypass** | Difficult to verify differences between original and processed audio. | Bypass button toggled a boolean that only altered the generated config file string rather than delivering immediate acoustic A/B comparison. | Implement an active A/B comparison bench with gain-matched loudness compensation and instant toggle between Original (Bit-Exact) and Processed (Filtered) audio. |
| **Audio Studio Clarity** | Controls lacked clear purpose; technical jargon confused users. | Sliders like "Q", "Preamp", "Auto-Preamp", "Shelf Slope" had no human-readable explanations or tooltips. | Add clear descriptive subtitles, frequency labels, intended listening experience descriptions for all 16 presets, and contextual tooltips. |
| **Scrolling & Page Layout** | Some sections could not be scrolled properly; mouse wheel jerking or swallowed. | `MainWindow.xaml` wrapped the `ContentControl` in a master `ScrollViewer`, while each child view (`DisplayStudioView.xaml`, `AudioStudioView.xaml`, etc.) ALSO contained an outer `ScrollViewer`. In WPF, nested ScrollViewers result in the inner seeing infinite height and mouse wheel events being swallowed. | Eliminate the duplicate outer ScrollViewer hierarchy; ensure proper `PreviewMouseWheel` bubbling so the entire viewport scrolls smoothly without boundary trapping. |
| **Text Blurriness & Contrast** | Small, blurry, or low-contrast text. | Labels used `FontSize="9"` and `FontSize="10"` with `TextTertiaryBrush` (`#64748B`), which provides insufficient contrast on dark backgrounds (`#0E121E`). | Upgrade typography to standard minimum 12px for body, 13px for labels, 11px for status tags; boost contrast using `TextSecondaryBrush` (`#CBD5E1`) and `TextPrimaryBrush` (`#F8FAFC`). |
| **Reset-to-Default Options** | No convenient reset options for individual sections. | Users had to manually drag every slider back to center or neutral. | Implement explicit **Reset to Default** buttons on Display (Luminance/Contrast, RGB Gains, Resolution) and Audio (Graphic EQ, Enhancements, Channel Balance) with confirmation safety. |

---

## 2. Interactive Controls Functionality Matrix

### A. Display Studio Controls

| Control Name | Type | Intended Function | Underlying API / Service | Current State | Verifiable Behavior | Root Cause / Fix |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Monitor Header** | Info Card | Displays connected monitor model, connection type, serial number. | `DdcCiMonitorService` / `EdidParser` | **Working** | Queries EDID block; displays "Acer ED270 X0" and S/N. | Operational. |
| **Read Hardware** | Button | Force re-reads hardware registers from monitor microcontroller. | `DdcCiMonitorService.RefreshAllHardwareValuesAsync` | **Working** | Queries VCP 0x10, 0x12, 0x16, 0x18, 0x1A over I2C. | Operational. |
| **Resolution Dropdown** | ComboBox | Allows user to select from supported resolutions. | `DisplayResolutionService` / `EnumDisplaySettingsEx` | **Broken** | UI visible, but selecting has no effect due to `Split('x')` vs `×`. | Fix string parsing to support `×` and strongly-typed resolution objects. |
| **Refresh Rate Dropdown** | ComboBox | Allows user to select supported Hz for chosen resolution. | `DisplayResolutionService` / `EnumDisplaySettingsEx` | **Broken** | Selection clears or gets stuck due to parsing failure. | Link directly to resolution's supported mode set; read back actual rate. |
| **Apply Mode** | Button | Applies chosen resolution and refresh rate to Windows display driver. | `DisplayResolutionService.ApplyModeAsync` | **Broken** | Silent failure because resolution string parsing returned 0x0. | Pass resolved width, height, Hz, and `DeviceName` to `ChangeDisplaySettingsEx`. |
| **Rollback Safety Banner** | Banner / Buttons | 15s countdown with "Keep Changes" or "Revert Now". | `DisplayResolutionService.ConfirmAppliedMode` / `RevertModeAsync` | **Partially Working** | Banner UI works, but triggered only if Apply succeeds. | Wire cleanly to real mode changes with interactive rollback timer. |
| **Reset Display Defaults** | Button | Restores monitor to native resolution (1920x1080 @ 200Hz) and neutral RGB. | `DisplayResolutionService.RevertModeAsync` + DDC/CI neutral | **Missing** | No single-click reset to default display configuration. | Add dedicated "Reset Display Settings" button with confirmation. |
| **Luminance Slider** | Slider (0–100) | Adjusts physical backlight intensity. | DDC/CI VCP Code 0x10 | **Working** | Writes to hardware; verified on Acer ED270 X0. | Operational. |
| **Contrast Slider** | Slider (0–100) | Adjusts hardware contrast ratio. | DDC/CI VCP Code 0x12 | **Working** | Writes to hardware; verified on Acer ED270 X0. | Operational. |
| **Red Gain Slider** | Slider (0–100) | Adjusts video gain for Red channel. | DDC/CI VCP Code 0x16 | **Working** | Writes to hardware; readback verified. | Operational. |
| **Green Gain Slider**| Slider (0–100) | Adjusts video gain for Green channel. | DDC/CI VCP Code 0x18 | **Working** | Writes to hardware; readback verified. | Operational. |
| **Blue Gain Slider** | Slider (0–100) | Adjusts video gain for Blue channel. | DDC/CI VCP Code 0x1A | **Working** | Writes to hardware; readback verified. | Operational. |
| **Reset RGB Button** | Button | Resets Red, Green, Blue gains to 50/50/50. | `DdcCiMonitorService.SetRgbGainsAsync` | **Working** | Reverts hardware gains to neutral baseline. | Operational. |
| **Sub-Zero Dimmer Toggle** | Toggle | Enables software overlay dimming below hardware 0%. | `SoftwareDimmerService` (Win32 transparent layered window) | **Working** | Dims screen smoothly without backlight flicker. | Operational. |
| **Sub-Zero Slider** | Slider (1–50) | Controls depth of software attenuation. | `SoftwareDimmerService.SetDimming` | **Working** | Opacity updates in real-time. | Operational. |
| **Display Profiles** | Card Grid | Applies preset combinations of brightness, contrast, RGB. | `DisplayStudioViewModel.ApplyProfile` | **Working** | Updates sliders and sends DDC/CI writes. | Operational. |
| **Save Monitor Prefs**| Button | Persists current display settings per monitor hardware key. | `ProfileStorageService.SaveMonitorPreferenceAsync` | **Working** | JSON stored in `%LOCALAPPDATA%\PRISM\profiles`. | Operational. |
| **Diagnostics Drawer** | Expander | Shows low-level VCP registers, I2C bus latency, and EDID. | View Binding | **Working** | Displays real-time wire latency and verified registers. | Operational. |

---

### B. Audio Studio Controls

| Control Name | Type | Intended Function | Underlying API / Service | Current State | Verifiable Behavior | Root Cause / Fix |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Master Volume** | Slider (0–100) | Adjusts Windows default playback endpoint volume. | `CoreAudioDeviceService.SetMasterVolume` / `IAudioEndpointVolume` | **Working** | Hardware volume updates immediately. | Operational. |
| **Mute Toggle** | Toggle Button | Mutes / unmutes active playback device. | `CoreAudioDeviceService.SetMute` | **Working** | Endpoint mute state changes immediately. | Operational. |
| **Channel Balance** | Slider (-1.0 to +1.0) | Adjusts Left / Right channel attenuation. | `CoreAudioDeviceService.SetChannelVolume` | **Working** | Adjusts channel levels in Windows audio engine. | Add convenient "Center Balance" reset button. |
| **10-Band EQ Sliders**| 10x Sliders | Adjusts frequency bands (31Hz–16kHz, ±12dB). | `EqualizerApoBridgeService` / In-App DSP | **Partially Working** | Sliders move, curve updates, config string generates, but no system audio change because Equalizer APO is not installed. | Add In-App Real-Time DSP Audition Player with real biquad filtering; indicate system APO status accurately. |
| **Reset EQ Button** | Button | Resets all 10 bands to 0.0 dB flat response. | ViewModel State | **Missing** | User must manually slide 10 sliders back to zero. | Add "Reset EQ (Flat)" button with instant reset. |
| **Preamp Slider** | Slider (-12 to +6dB)| Manual gain trim before EQ filter stage. | `EqualizerApoBridgeService` | **Partially Working** | Generates `Preamp:` directive. Unclear explanation. | Add clear label: "Controls input level to prevent digital clipping". |
| **Auto-Preamp** | Toggle | Calculates maximum positive boost and attenuates preamp to 0 dB peak. | `AudioStudioViewModel.SyncFromSliders` | **Working** | Calculates headroom dynamically. | Operational. |
| **Bass Enhancement** | Slider (-6 to +12dB) | Low-shelf acoustic boost below 100 Hz. | Biquad Low-Shelf Filter | **Partially Working** | Updates response curve; audible only if APO installed. | Integrate into In-App DSP player; add clear description. |
| **Treble Air** | Slider (-6 to +12dB) | High-shelf acoustic sparkle above 10 kHz. | Biquad High-Shelf Filter | **Partially Working** | Updates curve; audible only if APO installed. | Integrate into In-App DSP player; add clear description. |
| **Stereo Widening** | Toggle & Slider | Psychoacoustic soundstage widening (0–200%). | Mid-Side (M/S) Matrix Processing | **Partially Working** | Math defined; needs audible audition verification. | Connect to in-app player M/S matrix. |
| **Dynamics Compressor**| Toggle & Slider | Attenuates sudden loud peaks and lifts dialogue. | Dynamic Range Limiter | **Partially Working** | Config generated; needs audible audition verification. | Connect to in-app soft-knee limiter. |
| **Master DSP Bypass**| Toggle Button | Instant A/B comparison between Raw and Processed audio. | `AudioProfile.IsBypassed` | **Partially Working** | Toggles boolean; lacks loudness matching and test bench. | Implement dedicated A/B comparison bench with gain-matched bypass. |
| **Preset Library** | Tile Selector | 16 curated acoustic profiles (Bass, Vocal, Cinema, etc.). | `ProfileStorageService` / ViewModel | **Working (UI)** | Loads presets, updates sliders, updates curve. | Add rich intent descriptions for all 16 presets; test audibly. |
| **Real-Time Visualizer**| 32-Bar Canvas | Displays frequency spectrum of active audio playback. | `WasapiLoopbackVisualizer` | **Partially Working** | `SimulateWhenIdle = true` produced fake motion when silent. | Disable idle simulation; bind strictly to real WASAPI loopback samples; silence = flat. |
| **Equalizer APO Status**| Status Card | Reports APO installation and system hook status. | `EqualizerApoBridgeService.CheckStatus` | **Working** | Correctly identifies APO is NOT installed on this PC. | Upgrade status guidance to provide 1-click installer guidance. |

---

## 3. Action Plan for Phase 2 through Phase 13

1. **Phase 2 & 3 (Resolution & Refresh Rates):**
   - Fix string formatting and replace with strongly-typed `ResolutionOption`.
   - Use `EnumDisplaySettingsEx` targeting `\\.\DISPLAY1` directly.
   - Enumerate all genuine driver modes and filter invalid combinations.
   - Wire `ApplyResolutionCommand` to `ChangeDisplaySettingsEx` with `CDS_UPDATEREGISTRY`.
   - Provide interactive 15-second countdown with automatic rollback and instant confirm/revert.
   - Add "Reset Display Mode to Native" button.
2. **Phase 4 & 5 (Audible Audio Overhaul & A/B Comparison):**
   - Implement an In-App Real-Time DSP Audition Player inside PRISM with 10-band biquad IIR filter processing, bass/treble shelf, and stereo widening.
   - Build an A/B Comparison Bench with instant Raw vs Processed toggle and loudness compensation.
   - Clarify DSP status: distinguish between In-App Audition Active vs System-Wide APO Not Installed.
   - Curate and test 16 distinct presets with technical descriptions.
3. **Phase 6 & 7 (Enhancements & Device Profiles):**
   - Provide clear explanations for every audio slider (Bass, Treble, Preamp, Widening, Limiter).
   - Ensure per-device profile persistence.
4. **Phase 8 (Audio Visualizer):**
   - Disable idle simulation; connect real WASAPI loopback FFT data; handle silence cleanly.
5. **Phase 9 (Layout & Scrolling):**
   - Remove nested ScrollViewer collision in `MainWindow.xaml` and child views.
   - Ensure smooth `PreviewMouseWheel` bubbling.
6. **Phase 10 & 11 (Reset to Default & Typography):**
   - Add Reset buttons to every section (Display Resolution, DDC/CI, Graphic EQ, Enhancements).
   - Increase font sizes and contrast to eliminate blurry text.
7. **Phase 12 & 13 (Testing & Verification):**
   - Perform automated and real-world audio playback verification.
   - Generate all required verification reports.
