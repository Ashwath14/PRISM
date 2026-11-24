# PRISM: UI / UX Design System & Visual Specification
**Aero Glass 2.0: Nordic Frost & Deep Navy Aesthetic**

---

## 1. Visual Language & Design Philosophy

PRISM’s visual identity blends the tactile, frosted translucency of **Apple’s macOS/iOS widgets** with the high-performance architectural precision of **Windows 11 Mica and DirectComposition**.

It rejects the noisy, hyper-saturated gamer aesthetics (harsh neon borders, gratuitous hex grids, aggressive angular shapes) as well as the lifeless, flat corporate dashboard look. Instead, PRISM embraces an elegant **"Glassy Blue"** material design:
* **Tactile Translucency:** Interfaces feel like machined obsidian glass panes floating over deep space.
* **Quiet Luxury:** Colors are deep, soothing, and restrained. Accents only illuminate interactive nodes or critical feedback.
* **Typographic Hierarchy:** Clean, legibly spaced typography ensures every number, decibel value, and frequency marker is immediately readable.
* **Restrained Motion:** Animations are physics-inspired (Apple-style spring dampening), never sluggish, and always convey physical mass.

---

## 2. Color Palette & Material Token Specification

```
+---------------------------------------------------------------------------------------+
|  CANVAS / BASE LAYERS                                                                |
|  - Midnight Obsidian (Main Window Base):   #0B111E  (rgb: 11, 17, 30)                |
|  - Deep Space Navy (Sidebar & Panels):     #0F172A  (rgb: 15, 23, 42)                |
|  - Elevated Slate (Card Backgrounds):      #1E293B  (rgb: 30, 41, 59) at 70% opacity  |
+---------------------------------------------------------------------------------------+
|  GLASS & TRANSLUCENCY TOKENS                                                          |
|  - Frosted Glass Surface:                  rgba(15, 23, 42, 0.65) with 20px blur      |
|  - Glass Card Surface:                     rgba(30, 41, 59, 0.55) with 12px blur      |
|  - Glass Inner Highlight (Specular Top):   rgba(255, 255, 255, 0.08) (1px border)    |
|  - Glass Drop Shadow (Depth Ambient):      rgba(0, 0, 0, 0.40) (blur: 24px, y: 8px)  |
+---------------------------------------------------------------------------------------+
|  BRAND ACCENTS & ILLUMINATION                                                         |
|  - Electric Cyan (Primary Accent & Active):#00B4D8  (rgb: 0, 180, 216)               |
|  - Sapphire Blue (Interactive Hover):      #0077B6  (rgb: 0, 119, 182)               |
|  - Icy White-Blue (Glow Highlights):       #E0F2FE  (rgb: 224, 242, 254)             |
|  - Deep Cobalt (Track Inactive):           #1E3A8A  (rgb: 30, 58, 138) at 40%        |
+---------------------------------------------------------------------------------------+
|  TYPOGRAPHY & CONTENT                                                                 |
|  - Text Primary (High Contrast):           #F8FAFC  (Pure Ice White - 98% opacity)   |
|  - Text Secondary (Labels, Subtitles):     #94A3B8  (Muted Blue-Silver - 70% opacity)|
|  - Text Tertiary (Units, Disabled Hints):  #64748B  (Deep Slate - 45% opacity)       |
+---------------------------------------------------------------------------------------+
|  FUNCTIONAL / STATUS SEMANTICS                                                        |
|  - Hardware Connected (Emerald):           #10B981  (rgb: 16, 185, 129)              |
|  - Warning / Software Fallback (Amber):    #F59E0B  (rgb: 245, 158, 11)              |
|  - Disconnected / Unsupported (Ruby):      #EF4444  (rgb: 239, 68, 68)               |
+---------------------------------------------------------------------------------------+
```

---

## 3. Typography & Micro-Layout Rules

* **Primary Font Family:** `Segoe UI Variable Display` (Windows 11 Native) / `Aptos Display` with fallback to `Segoe UI`.
* **Typographic Scale:**
  * **Window Title / Header:** 20pt / Semi-Bold / Letter Spacing: `-0.3px`
  * **Section Headings:** 15pt / Semi-Bold / Letter Spacing: `-0.1px`
  * **Card Titles & Metric Labels:** 12pt / Medium / Letter Spacing: `0.0px`
  * **Standard Body & Values:** 10pt / Regular / Letter Spacing: `0.2px`
  * **Frequency & Decibel Badges:** 8.5pt / Semi-Bold / Monospace Numbers (`Segoe UI Variable Text`)
* **Corner Radius Standards:**
  * Application Window: `12px` (Standard Windows 11 rounded window chrome)
  * Large Cards / Studio Panels: `10px`
  * Buttons, Sliders, & Chips: `6px`
  * Floating Quick Flyout: `12px`

---

## 4. Component Design & Tactile Micro-Interactions

### 4.1 Precision Sliders (Display Brightness & EQ Sliders)
* **Slider Track:** 4px thickness, rounded ends. Inactive track is translucent Deep Cobalt; active fill is a subtle gradient from Sapphire Blue (`#0077B6`) to Electric Cyan (`#00B4D8`).
* **Slider Thumb:** 16px circular glass disc with a 1px specular white border (`rgba(255, 255, 255, 0.4)`) and an internal electric cyan dot.
* **Hover State:** Thumb smoothly scales from 16px to 18px with a soft 8px electric cyan glow (`rgba(0, 180, 216, 0.35)`).
* **Numerical Readout:** Always displays exact physical integer values (e.g. `44%`, `+3.5 dB`, `5000K`) floating adjacent to the control.

### 4.2 Interactive 10-Band EQ & Curve Canvas
* A continuous smooth cubic bezier spline connects the 10 frequency band nodes (32 Hz, 64 Hz, 125 Hz, 250 Hz, 500 Hz, 1 kHz, 2 kHz, 4 kHz, 8 kHz, 16 kHz).
* The area under the EQ spline is filled with a vertical gradient fading from `rgba(0, 180, 216, 0.15)` down to transparent `rgba(0, 180, 216, 0.0)`.
* Draggable nodes illuminate with an icy blue halo when hovered or dragged.

### 4.3 Real-Time 60 FPS Audio Spectrum Canvas
* 32 logarithmic frequency bars rendering at the bottom of Audio Studio.
* Uses dual-tone bars: Electric Cyan body with an Icy-White floating peak cap that holds for 150ms and falls with gravity physics.
* Silky smooth, jitter-free rendering handled via WPF `CompositionTarget.Rendering`.

---

## 5. Screen Wireframes & UI Hierarchy

### 5.1 Main Studio Navigation & Layout Structure

```
+----------------------------------------------------------------------------------------------------------+
|  [P] PRISM                                                                 _  [ ]  X                     |
+---------------------+------------------------------------------------------------------------------------+
|  STUDIO SUITE       |  DISPLAY STUDIO                                          [ Acer ED270 X0 (DP-1) v ]|
|                     |                                                                                    |
|  [x] Display Studio |  +----------------------------------------------------+  +-----------------------+ |
|  [ ] Audio Studio   |  | LUMINANCE & CONTRAST                               |  | COLOR TEMPERATURE     | |
|  [ ] Environments   |  |                                                    |  |                       | |
|                     |  | Hardware Brightness                     44%        |  | (o) Cool (6500K)      | |
|  UTILITIES          |  | [===O--------------------------------------------] |  | ( ) Neutral (5000K)   | |
|                     |  |                                                    |  | ( ) Warm (4500K)      | |
|  [ ] Quick Flyout   |  | Contrast                                50%        |  | ( ) Custom RGB Gains  | |
|  [ ] Diagnostics    |  | [==========O-------------------------------------] |  +-----------------------+ |
|  [ ] Settings       |  |                                                    |                            |
|                     |  | [x] Sub-Zero Night Dimming (Software Extension)    |  +-----------------------+ |
|                     |  | Level: Off (-0%)                                   |  | INPUT SOURCE          | |
|                     |  +----------------------------------------------------+  |                       | |
|                     |                                                          | [DisplayPort 1      v]| |
|                     |  +----------------------------------------------------+  |                       | |
|                     |  | RGB HARDWARE CHANNELS                              |  | [ Switch Port ]       | |
|                     |  | Red Gain   [========O-----------------] 50%        |  +-----------------------+ |
|                     |  | Green Gain [========O-----------------] 50%        |                            |
|                     |  | Blue Gain  [========O-----------------] 50%        |  +-----------------------+ |
|                     |  +----------------------------------------------------+  | QUICK PRESETS         | |
|                     |                                                          | [ Daylight ] [ Code ] | |
|                     |  [!] Acer ED270 X0: Full DDC/CI Hardware Active          | [ Late Night ] [ Game]| |
+---------------------+----------------------------------------------------------+-----------------------+-+
|  Hardware Bus: I2C (100kHz) | DDC/CI VCP: OK | DWM Overlay: Ready | Idle CPU: 0.0% | RAM: 34 MB          |
+----------------------------------------------------------------------------------------------------------+
```

---

### 5.2 Audio Studio Screen Layout

```
+----------------------------------------------------------------------------------------------------------+
|  [P] PRISM                                                                 _  [ ]  X                     |
+---------------------+------------------------------------------------------------------------------------+
|  STUDIO SUITE       |  AUDIO STUDIO                            Output: [ Cosmic Byte Proteus God Mode v ]|
|                     |                                                  [x] Fast Toggle to [Headphones]   |
|  [ ] Display Studio |  +-------------------------------------------------------------------------------+ |
|  [x] Audio Studio   |  | 10-BAND PARAMETRIC GRAPHIC EQUALIZER                              Flat [Reset]| |
|  [ ] Environments   |  |   +12dB                                                                       | |
|                     |  |          .---.                                            .---.               | |
|  UTILITIES          |  |   0dB --/     \------------------------------------------/     \-------       | |
|                     |  |   -12dB                                                                       | |
|  [ ] Quick Flyout   |  |         32   64  125  250  500   1k   2k   4k   8k  16k                     | |
|  [ ] Diagnostics    |  |        [+3] [ 0] [-2] [ 0] [ 0] [+1] [+4] [+2] [ 0] [+5] dB                  | |
|  [ ] Settings       |  +-------------------------------------------------------------------------------+ |
|                     |                                                                                    |
|                     |  +------------------------------+ +--------------------+ +-----------------------+ |
|                     |  | REAL-TIME 60 FPS SPECTRUM    | | SPATIAL & WIDENING | | COMPRESSOR / LIMITER  | |
|                     |  | ||| |                        | |                    | |                       | |
|                     |  | ||||||| || |                 | | Stereo Width: 135% | | Threshold:   -14 dB   | |
|                     |  | |||||||||||||||||||||| ||    | | [===O------------] | | Ratio:       3.2 : 1  | |
|                     |  | Low   Mid        High        | | [x] Dialog Clarity | | [x] Night Normalizer  | |
|                     |  +------------------------------+ +--------------------+ +-----------------------+ |
|                     |                                                                                    |
|                     |  Master Audio Volume: 78%  [=======================O-------------]  [x] Mute      |
+---------------------+------------------------------------------------------------------------------------+
|  WASAPI Loopback: Active (48kHz 24-bit) | Buffer Latency: 8ms | DSP Engine: SIMD AVX2 | RAM: 38 MB       |
+----------------------------------------------------------------------------------------------------------+
```

---

### 5.3 Compact Floating Quick Flyout (`Win + Alt + P`)

Designed to float seamlessly near the Windows 11 Taskbar system tray or center-screen:

```
+---------------------------------------------+
|  PRISM QUICK CONTROL               [x] Close|
+---------------------------------------------+
|  DISPLAY: Acer ED270 X0                     |
|  Brightness: 44%                            |
|  [==================O---------------------] |
|  [x] Sub-Zero Night Dimmer                  |
+---------------------------------------------+
|  AUDIO: Cosmic Byte Proteus                 |
|  Volume: 78%                                |
|  [===========================O------------] |
|  [ Headphone Profile ]  [ Speaker Profile ]  |
+---------------------------------------------+
|  QUICK PRESET:                              |
|  ( Late Night )   ( Gaming )   ( Reference )|
+---------------------------------------------+
```

---

### 5.4 Hardware Diagnostics & Capability Inspector

PRISM includes an honest, transparent diagnostic pane that shows the exact hardware reality of the user's setup:

```
+----------------------------------------------------------------------------------------------------------+
|  HARDWARE CAPABILITIES & DIAGNOSTICS                                                                     |
+----------------------------------------------------------------------------------------------------------+
|  DISPLAY 1: ACR ED270 X0                                                                                 |
|  * Device ID:       DISPLAY\ACR0F95\5&1d0456d1&0&UID260_0                                                |
|  * Connection:      DisplayPort (AMD Radeon RX 580 2048SP)                                               |
|  * High-Level Caps: FAILED (0xC0262587 - Scaler Caps String Non-Standard; Safely Handled)                |
|  * VCP 0x10 (Luminance / Brightness):  [SUPPORTED] Range 0 - 100 (Current: 44)                            |
|  * VCP 0x12 (Contrast):                [SUPPORTED] Range 0 - 100 (Current: 50)                            |
|  * VCP 0x14 (Color Preset):            [SUPPORTED] Range 0 - 11  (Current: 5)                             |
|  * VCP 0x16/18/1A (RGB Video Gain):    [SUPPORTED] Red: 50, Green: 50, Blue: 50                          |
|  * VCP 0x60 (Input Source Select):     [SUPPORTED] Value: 0x11 (DP-1)                                    |
|  * VCP 0xAA (Screen Orientation):      [UNSUPPORTED] Scaler rejected command                             |
|  * VCP 0xD6 (Power Mode):              [SUPPORTED] Active                                                |
+----------------------------------------------------------------------------------------------------------+
|  ACTIVE AUDIO ENDPOINTS:                                                                                 |
|  1. Cosmic Byte Proteus God Mode (High Definition Audio Device) -> Default Multimedia & Communications   |
|     * Format: 2 Channels, 48000 Hz, 24-bit Float PCM                                                    |
|     * WASAPI Loopback Capture: Active (8.3ms buffer)                                                     |
|  2. Headphones (High Definition Audio Device) -> Available                                               |
+----------------------------------------------------------------------------------------------------------+
```
