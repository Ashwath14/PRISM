# PRISM: Product Vision & Strategic Direction
**A Premium Windows Display & Audio Control Studio**

---

## 1. Executive Summary

**PRISM** is a bespoke, high-performance Windows desktop application designed to bridge the chasm between physical hardware displays, low-level audio subsystems, and the everyday user experience.

Today's Windows ecosystem suffers from deep fragmentation:
* Adjusting monitor hardware parameters requires reaching awkwardly behind or under the screen to manipulate stiff, unintuitive physical OSD buttons.
* Software display controls are split between the Windows 11 Settings app, legacy Control Panel applets, and vendor-specific bloated control centers (AMD Software, NVIDIA Control Panel) that take seconds to launch and consume hundreds of megabytes of RAM.
* Sound enhancement is either locked behind proprietary OEM bloatware (Nahimic, Realtek Audio Console), abandoned utilities with dated aesthetics, or complex digital audio workstations that no everyday consumer wants to manage.

PRISM unites **Display Studio** and **Audio Studio** into a single, cohesive, Apple-grade utility. It is built upon strict engineering principles: zero artificial fluff, hardware-accurate capabilities detection, real mathematical DSP (digital signal processing), lightweight background resource consumption, and a translucent "glassy blue" visual language tailored for Windows 11.

---

## 2. Core Philosophy & Product Principles

PRISM is guided by ten non-negotiable architectural tenets:

1. **Hardware Honesty (Zero Fake Functionality):** 
   If a monitor's scaler does not support RGB color gain via DDC/CI, or if an audio endpoint does not support low-latency loopback, PRISM will never display a decorative slider that simulates an effect without technical reality. Unsupported features are detected dynamically, disabled cleanly, and accompanied by transparent technical diagnostics explaining *why* they are unavailable.
2. **Deterministic Control vs. Software Simulation:**
   PRISM rigorously distinguishes between true physical hardware control (e.g., backlight CCFL/WLED modulation via DDC/CI VCP code `0x10`) and software compensations (e.g., DWM alpha overlays or GPU gamma ramps). Both have their place, but the user is always informed of the mechanism in play.
3. **Pure Math Audio DSP:**
   Audio enhancements are not superficial gain boosts or placebo volume jumps. Equalizers use genuine second-order IIR biquad filtering with floating-point math; dynamic range compression utilizes true envelope peak/RMS detection with attack/release ballistics; spatial audio uses Head-Related Transfer Functions (HRTF) and mid-side phase manipulation.
4. **Sub-35MB Idle Footprint & Zero-Idle-CPU:**
   PRISM runs quietly in the system tray. When minimized, background threads sleep on event wait handles (`WaitForSingleObject`). Hotkey listeners and DDC/CI background workers consume effectively 0.0% CPU and negligible working set memory.
5. **No Telemetry, No Account, Offline-First:**
   PRISM requires no cloud login, makes no remote network calls, tracks zero user analytics, and operates completely offline. Settings and profiles are saved locally in clean, human-readable JSON files.
6. **Hardware Safety & I2C Bus Discipline:**
   Monitors communicate over I2C/DDC at a slow 100 kHz bus clock. Naive software that floods DDC/CI commands locks up monitor microcontrollers. PRISM implements an asynchronous write queue with command debouncing, pacing delays (40ms-50ms between packets), and retry safeguards.
7. **Apple-Grade Aesthetic, Windows-Native Performance:**
   PRISM fuses the translucent elegance, typography, and fluid micro-interactions of macOS/iOS widgets with the high-performance architectural depth of Windows Core Audio, DXVA2, and DirectX.
8. **Instant Accessibility:**
   Crucial adjustments (brightness, contrast, master audio profile, output switching) must be reachable within one keystroke or one click via a compact, floating quick-control flyout.
9. **Resilient Failure Modes:**
   If a monitor fails to reply over DDC/CI (e.g., waking from sleep, DisplayPort power-save drop), PRISM never crashes. It gracefully falls back, retries with exponential backoff, or offers software screen dimming seamlessly.
10. **Extensible Profile Architecture:**
    Display settings (brightness, contrast, color temperature) and audio presets (EQ curve, spatial widening, compressor threshold) can be coupled into unified "Environments" (e.g., *Late Night Coding*, *Competitive FPS*, *Cinematic HDR*, *Acoustic Focus*) triggered manually or automatically by foreground process rules.

---

## 3. The Target Audience & Everyday Use Cases

PRISM solves acute everyday friction for demanding PC users:

### Persona A: The Late-Night Developer & Writer
* **Pain Point:** At 1:00 AM, the monitor’s lowest hardware brightness (e.g., 250 cd/m² down to 80 cd/m²) is still blinding. Navigating the physical monitor menu in the dark to lower brightness and warm up color tones is infuriating. Furthermore, desktop speakers need immediate low-end reduction and night-mode compression to keep speech intelligible without waking others.
* **PRISM Solution:** A single global shortcut (`Ctrl+Alt+F1`) or automated schedule shifts the display to a deep hardware-dimmed + DWM software-shaded dark profile, warms the RGB balance, activates an audio night-mode compressor, and directs sound to headphones with spatial dialog clarity.

### Persona B: The Competitive Gamer & Content Consumer
* **Pain Point:** In-game shadows require elevated gamma and contrast to spot adversaries, while Discord voices need loudness normalization. When switching to YouTube or a movie, the user wants deep black levels, neutral color rendering, and rich stereo widening.
* **PRISM Solution:** Automatic process detection hooks into `SetWinEventHook`. When `game.exe` acquires foreground focus, PRISM silently commands the monitor via DDC/CI to switch to high-contrast game tuning and activates a gaming audio profile with bass boost and footstep frequency peaking. When returning to the desktop or browser, neutral reference presets restore instantly.

### Persona C: The Audiophile / Creative Pro with Multi-Endpoint Desktops
* **Pain Point:** Windows audio device switching is buried in the taskbar audio flyout. Equalizer APO or Peace are intimidating, visually archaic, and break after Windows cumulative updates.
* **PRISM Solution:** A clean, tactile visual switch between Studio Headphones and Desk Monitors, each paired with independent parametric EQ curves, calibrated volume levels, and a real-time 60 FPS spectrum analyzer.

---

## 4. Product Identity & Brand Direction

### Name: **PRISM**
* **Symbolism:** A prism takes a single beam of raw, unshaped light and refracts it into a structured, vivid spectrum. Similarly, PRISM takes the raw signals of your PC—display backlights, color channels, and acoustic frequencies—and shapes them into a calibrated, beautiful experience.
* **Brand Voice:** Professional, sleek, technical yet accessible, calm, precision-engineered.
* **Visual Signature:** Frost glass surfaces, deep midnight navy backgrounds, electric and cyan accents, and razor-sharp typographic hierarchy.

---

## 5. Scope of Application Modules

```
+-------------------------------------------------------------------------+
|                                PRISM                                    |
+------------------------------------+------------------------------------+
|           DISPLAY STUDIO           |            AUDIO STUDIO            |
+------------------------------------+------------------------------------+
| * Hardware DDC/CI Brightness       | * 10-Band Precision Graphic EQ     |
| * Hardware DDC/CI Contrast         | * Parametric EQ with Draggable Nodes|
| * Hardware RGB Gain Tuning         | * Dynamic Range Compressor/Limiter |
| * Input Source Switching           | * Mid-Side Stereo Widener          |
| * Smooth DWM Software Overlay Dim  | * Binaural HRTF Spatial Acoustic   |
| * Multi-Monitor Topology Manager   | * 60 FPS Fast Fourier Spectrum     |
| * Display Capability Diagnostics   | * Per-Device Audio Profiles        |
| * Automatic Day/Night Profiles     | * Headphone/Speaker Instant Toggle |
+------------------------------------+------------------------------------+
|                         UNIFIED ENGINE & SHELL                          |
+-------------------------------------------------------------------------+
| * Compact Floating Quick-Control Flyout (Win+Alt+P)                     |
| * Global Hotkey Router & Process Watcher Automation                     |
| * Glassy Blue Windows 11 Shell (Mica / Acrylic / DirectComposition)     |
| * JSON Profile Synchronization Engine & Zero-Privilege Tray Service     |
+-------------------------------------------------------------------------+
```

---

## 6. Long-Term Vision Beyond V1

While V1.0 focuses on rock-solid fundamentals for single/dual monitors (including proven hardware-verified control on displays like the Acer ED270 X0) and real-time audio shaping:
* **V2.0:** Ambient light sensor integration via USB micro-controllers, multi-monitor synchronous dimming, and integration with Windows HDR brightness metadata APIs.
* **V3.0:** Low-level virtual audio kernel driver pipeline, allowing transparent system-wide spatialization without relying on user-mode loopback redirection.
