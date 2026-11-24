# PRISM UI/UX Visual Specification & Screen Mockups
**Document Version:** 2.0.0  
**Phase:** Phase 2 Complete Redesign Specification  
**Author:** Lead UI/UX Systems Architect  
**Date:** November 20, 2026  

---

## 1. Architectural Layout & Window Frame

PRISM utilizes a unified, responsive window architecture (minimum resolution 1180x760, default 1240x800, resizable).

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ [❖] P R I S M                                                                         —   □   ✕        │
├──────────────────┬─────────────────────────────────────────────────────────────────────────────────────┤
│                  │                                                                                     │
│  [🏠] Home       │   ACTIVE CONTENT VIEWPORT                                                           │
│                  │   - Screen-specific header and context                                              │
│  [🖥️] Display    │   - Primary interactive cards and fader decks                                       │
│                  │   - Responsive grid and grouped modules                                             │
│  [📊] Audio      │                                                                                     │
│                  │                                                                                     │
│  [❖] Environment │                                                                                     │
│                  │                                                                                     │
│  [⚙️] Settings   │                                                                                     │
│                  │                                                                                     │
│                  ├─────────────────────────────────────────────────────────────────────────────────────┤
│  [●] System Ready│   BOTTOM QUICK CONTROL DOCK (Persistent Across Views)                               │
│      Acer + Audio│   [🖥️ Brightness ═══════●══ 72%]  │  [🔊 Volume ════════●═ 58%]  [🔇] [🌙] [↗]      │
└──────────────────┴─────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Screen 1: Home (Central Command Studio)

### 2.1 Wireframe Mockup
*Reference Inspiration:* `media_1795115367494_b7a1488d.jpg`

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│  WELCOME TO PRISM                                           ┌────────────────────────────────────────┐ │
│  Display & Audio,                                           │ 💓 System Status                       │ │
│  Perfectly in Sync.                                         │                                        │ │
│  A unified studio to control your screen, sound and         │  🖥️ Monitor: Acer ED270 X0 [Connected] │ │
│  workspace experience.                                      │  🎧 Output: Cosmic Byte    [Active]    │ │
│                                                             │  🎛️ DSP Engine: Eq APO     [Not Inst]  │ │
│  [ 🖥️ Open Display Studio  > ]   [ 🎚️ Open Audio Studio  > ] │ └────────────────────────────────────────┘ │
│                                                                                                        │
│  ❖ Environments                                                               [ Manage Environments -> ]│
│  One click to switch your display and audio setup.                                                     │
│                                                                                                        │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐               │
│  │ ☀️ Daylight Focus │  │ 🌙 Late Night    │  │ 🎮 Competitive   │  │ 🎬 Cinematic     │               │
│  │ Balanced & clear │  │ Low-light comfort│  │ High contrast    │  │ Rich visuals     │               │
│  │ for productivity │  │ for dark rooms   │  │ and footsteps    │  │ & detailed sound │               │
│  │ [● Active]       │  │ [Switch]         │  │ [Switch]         │  │ [Switch]         │               │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘  └──────────────────┘               │
│                                                                                                        │
│ ┌────────────────────────────────────────────────────────────────────────────────────────────────────┐ │
│ │ 🖥️ Brightness ════════════●═══ 72%   │   🔊 Volume ══════════════● 58%      [🔇 Mute]  [🌙 Sub-Zero]  │ │
│ └────────────────────────────────────────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Data Bindings & Interactions
- **Hero Shortcuts:** Click navigates directly to `DisplayStudioView` and `AudioStudioView`.
- **System Status Card:**
  - `MonitorName`: Bound to `DisplayStudioViewModel.ConnectedMonitor.DisplayName` ("Acer ED270 X0").
  - `AudioEndpoint`: Bound to `AudioStudioViewModel.SelectedDevice.FriendlyName` ("Cosmic Byte Proteus").
  - `EqualizerApoBadge`: Displays "Not Installed" with slate badge, truthfully reflecting host status.
- **Environment Cards:** Clicking any card invokes `ProfileStorageService.ApplyEnvironmentAsync(env)`.
- **Bottom Dock:**
  - Brightness slider bound two-way to `DisplayStudioViewModel.Brightness`.
  - Volume slider bound two-way to `AudioStudioViewModel.MasterVolume`.
  - Night Mode button toggles `DisplayStudioViewModel.IsSubZeroDimEnabled`.

---

## 3. Screen 2: Display Studio (Hardware Monitor Workspace)

### 3.1 Wireframe Mockup

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│  DISPLAY STUDIO                                                                                        │
│  Precision hardware control over physical monitor registers and software dimming.                      │
│                                                                                                        │
│  ┌───────────────────────────────────────────────────────────────────────────────────────────────────┐ │
│  │ 🖥️ Acer ED270 X0 (Curved 27")  •  DisplayPort-1  •  240 Hz  •  VA Panel      [ DDC/CI Verified ● ]│ │
│  └───────────────────────────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                                        │
│  ┌─────────────────────────────────────────────────────┐  ┌──────────────────────────────────────────┐ │
│  │ HARDWARE DDC/CI REGISTERS                           │  │ SUB-ZERO SOFTWARE DIMMER                 │ │
│  │                                                     │  │                                          │ │
│  │ Luminance (VCP 0x10)                                │  │ Deep Software Dimming: [ Active Toggle ] │ │
│  │ ☀️ ══════════════════════════════●══════ 75%        │  │                                          │ │
│  │                                                     │  │ Software Attenuation:                    │ │
│  │ Contrast (VCP 0x12)                                 │  │ 🌙 ════════════●══════════════════ 25%   │ │
│  │ 🌓 ════════════════════●════════════════ 50%        │  │                                          │ │
│  │                                                     │  │ Note: Sub-Zero engages below hardware 0% │ │
│  │ RGB Color Gains (VCP 0x16, 0x18, 0x1A)              │  │ to protect eyesight in pitch-dark rooms. │ │
│  │ Red   🔴 ═════════════════●═════════════ 50         └──────────────────────────────────────────┘ │
│  │ Green 🟢 ═════════════════●═════════════ 50         ┌──────────────────────────────────────────┐ │
│  │ Blue  🔵 ═════════════════●═════════════ 50         │ DISPLAY PROFILES                         │ │
│  │                                                     │  [Standard 6500K]   [Warm Sepia]         │ │
│  │ [ Reset Color Balance (50/50/50) ]                  │  [Studio Flat]      [Esports High-Vis]   │ │
│  └─────────────────────────────────────────────────────┘  └──────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Visual & Behavioral Rules
- **Explicit Domain Tagging:** Hardware DDC/CI controls are visually distinguished with a cyan badge (`HARDWARE DDC/CI`), while Sub-Zero is tagged with a lavender badge (`SOFTWARE LAYER`).
- **Real-Time Readback Confirmation:** When a slider write settles after the 50ms debouncer, a subtle green tick briefly appears beside the value label indicating hardware confirmation.

---

## 4. Screen 3: Audio Studio (Professional Acoustics Suite)

### 4.1 Wireframe Mockup
*Reference Inspiration:* `media_1795115435482_2ad18d8b.png`

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│  AUDIO STUDIO                                                                                          │
│  System-wide audio processing, parametric equalization and real-time spectrum analysis.                │
│                                                                                                        │
│  ┌───────────────────────────────────────────────────────────────────────────────────────────────────┐ │
│  │ ℹ️ Equalizer APO is not installed on this system.                                                 │ │
│  │ Core Audio volume, mute and spectrum capture are active. DSP filtering requires Equalizer APO.    │ │
│  │ [ Download Equalizer APO 1.3.2 ]                                                                  │ │
│  └───────────────────────────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                                        │
│  ┌───────────────────────────────────────────────────────────────────────────────────────────────────┐ │
│  │ REAL-TIME WASAPI LOOPBACK SPECTRUM ANALYZER                                                       │ │
│  │ ┌───────────────────────────────────────────────────────────────────────────────────────────────┐ │ │
│  │ │  ▂ ▄ ▅ █ ▇ ▅ ▄ ▃ ▂ ▂ ▃ ▄ ▄ ▃ ▂                                      Peak: -12.4 dB [ VU OK ● ]│ │ │
│  │ └───────────────────────────────────────────────────────────────────────────────────────────────┘ │ │
│  │  31  63  125  250  500  1k  2k  4k  8k  16k Hz                                                    │ │
│  └───────────────────────────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                                        │
│  ┌───────────────────────────────────────────────────────────────────────────────────────────────────┐ │
│  │ 10-BAND PARAMETRIC GRAPHIC EQUALIZER               [ Bypass: OFF ]   Preamp Headroom: -3.0 dB     │ │
│  │                                                                                                   │ │
│  │  +12dB ───┬───┬───┬───┬───┬───┬───┬───┬───┬───                                                    │ │
│  │           │   │   │   │   │   │   │   │   │   │                                                   │ │
│  │   0dB ───●───●───●───●───●───●───●───●───●───●─── (Unity Gain)                                    │ │
│  │           │   │   │   │   │   │   │   │   │   │                                                   │ │
│  │  -12dB ───┴───┴───┴───┴───┴───┴───┴───┴───┴───                                                    │ │
│  │         31  63  125 250 500  1k  2k  4k  8k  16k                                                  │ │
│  │        +2dB +1dB  0dB  0dB  0dB  0dB +1dB +2dB +3dB +4dB                                           │ │
│  │                                                                                                   │ │
│  │ Presets: [ Flat Neutral ]  [ Bass Boost ]  [ Vocal Clarity ]  [ Movie Warmth ]  [ Gaming Footstep ]│ │
│  └───────────────────────────────────────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 4.2 Visual & Behavioral Rules
- **Truthful APO Banner:** If Equalizer APO is not installed, the banner clearly states the condition and provides a direct download link. Sliders remain interactive for preset crafting, with an explicit tooltip explaining that system-wide injection is paused until APO is installed.
- **Logarithmic Frequency Scale:** 10 vertical faders placed at exact octave intervals (31Hz to 16kHz) with smooth thumb grab handles and visible 0 dB unity line.

---

## 5. Screen 4: Environments (Coordinated Workspace Modes)

### 5.1 Wireframe Mockup

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│  ENVIRONMENTS                                                                                          │
│  Harmonize your display calibration and acoustic profile into one coordinated preset.                  │
│                                                                                                        │
│  ┌───────────────────────────────┐ ┌───────────────────────────────┐ ┌───────────────────────────────┐ │
│  │ ☀️ DAYLIGHT FOCUS             │ │ 🌙 LATE NIGHT                 │ │ 🎮 COMPETITIVE GAMING         │ │
│  │ [ ACTIVE PRESET ● ]           │ │                               │ │                               │ │
│  │                               │ │                               │ │                               │ │
│  │ Display: 75% • Contrast 50%   │ │ Display: 20% • Sub-Zero 15%   │ │ Display: 100% • Contrast 65%  │ │
│  │ Color: 6500K Neutral (50/50)  │ │ Color: Warm Sepia (58/48/36)  │ │ Color: Cool Vivid (46/50/54)  │ │
│  │ Audio: 45% Vol • Clarity EQ   │ │ Audio: 25% Vol • Night EQ     │ │ Audio: 70% Vol • Footstep EQ  │ │
│  │                               │ │                               │ │                               │ │
│  │ [ ✓ Current Environment ]     │ │ [ Apply Environment ]         │ │ [ Apply Environment ]         │ │
│  └───────────────────────────────┘ └───────────────────────────────┘ └───────────────────────────────┘ │
│                                                                                                        │
│  ┌───────────────────────────────┐ ┌───────────────────────────────┐ ┌───────────────────────────────┐ │
│  │ 🎬 CINEMATIC                  │ │ 🎧 STUDIO REFERENCE           │ │ 📖 READING WARMTH             │ │
│  │                               │ │                               │ │                               │ │
│  │ Display: 30% • Contrast 55%   │ │ Display: 50% • Contrast 50%   │ │ Display: 0% • Sub-Zero 40%    │ │
│  │ Color: Warm Film (52/48/44)   │ │ Color: Flat 50/50/50 Pure     │ │ Color: Deep Warm (55/45/38)   │ │
│  │ Audio: 60% Vol • Movie EQ     │ │ Audio: 50% Vol • Flat Bypass  │ │ Audio: 30% Vol • Acoustic EQ  │ │
│  │                               │ │                               │ │                               │ │
│  │ [ Apply Environment ]         │ │ [ Apply Environment ]         │ │ [ Apply Environment ]         │ │
│  └───────────────────────────────┘ └───────────────────────────────┘ └───────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 5.2 Visual & Behavioral Rules
- **Responsive 3x2 Grid:** Displays all 6 primary presets with distinct ambient color glows.
- **Immediate Feedback Toast:** Applying a preset triggers a smooth slide-in notification confirming calibrated display settings and audio routing.

---

## 6. Screen 5: Settings (Modular Preferences & Diagnostics)

### 6.1 Wireframe Mockup

```
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│  SETTINGS                                                                                              │
│                                                                                                        │
│  ┌────────────────────┬──────────────────────────────────────────────────────────────────────────────┐ │
│  │ [⚙️] General       │ GENERAL & WINDOW BEHAVIOR                                                    │ │
│  │ [🖥️] Display & I2C │                                                                              │ │
│  │ [📊] Audio & DSP   │ Launch PRISM on Windows Startup                                              │ │
│  │ [⌨️] Hotkey & Flyout│ Automatically starts PRISM in the system tray on user logon. [ ON  (Toggle) ]│ │
│  │ [📋] Diagnostics   │                                                                              │ │
│  │ [ℹ️] About PRISM    │ Minimize to System Tray on Close                                             │ │
│  │                    │ Keep PRISM active in notification area when closing window.  [ ON  (Toggle) ]│ │
│  │                    │                                                                              │ │
│  │                    │ Global Quick Flyout Shortcut                                                 │ │
│  │                    │ Press from any application to invoke instant controls:       [ Win + Alt + P ]│ │
│  │                    │                                                                              │ │
│  │                    │ HARDWARE BUS & PACING CONFIGURATION                                          │ │
│  │                    │ DDC/CI I2C Bus Delay: 50 ms  (Optimized for Acer ED270 X0)                   │ │
│  │                    │ Debounce Interval:    50 ms                                                  │ │
│  └────────────────────┴──────────────────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 7. Screen 6: Floating Quick Controls (`Win + Alt + P`)

### 7.1 Wireframe Mockup
*Reference Inspiration:* `media_1795115367494_b7a1488d.jpg` bottom bar & `media_1795115433980_e275944d.jpg`

```
┌──────────────────────────────────────────────────┐
│  [❖] PRISM QUICK CONTROLS             [ ✕ Close ]│
├──────────────────────────────────────────────────┤
│                                                  │
│  🖥️ Display Brightness                 75%        │
│  [ ☀️ ════════════════════════════●═══ ]         │
│  [x] Sub-Zero Software Dimmer Active             │
│                                                  │
│  🔊 Master Audio                       58%        │
│  [ 🔈 ════════════════════●═══════════ ]         │
│  [ Mute Toggle: OFF ]                            │
│                                                  │
│  🎧 Output: [ Cosmic Byte Proteus (HD Audio) v ] │
│                                                  │
│  ❖ Environments Quick Switch                     │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐          │
│  │ Daylight │ │ Night    │ │ Gaming   │          │
│  └──────────┘ └──────────┘ └──────────┘          │
│                                                  │
├──────────────────────────────────────────────────┤
│  [ 🖥️ Open Full PRISM Studio  > ]                │
└──────────────────────────────────────────────────┘
```

### 7.2 Visual & Behavioral Rules
- **Form Factor:** 380px wide x 520px high acrylic window with 20px corner radius.
- **Auto-Dismiss:** Closes instantly on loss of keyboard/mouse focus (`Deactivated` event).
- **Smooth Animation:** Slides 16px upward with a 200ms cubic ease on appearance.
