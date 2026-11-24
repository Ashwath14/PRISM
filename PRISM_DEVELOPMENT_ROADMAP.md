# PRISM: Phased Development Roadmap & Execution Plan
**From Hardware Verification to Production Desktop Application**

---

## 1. Roadmap Overview & Phasing Strategy

To ensure zero fake features, rock-solid stability, and continuous validation, PRISM’s engineering is structured into six sequential phases. Each phase culminates in measurable, testable deliverables before subsequent layers are built.

```
+-------------------------------------------------------------------------+
|                              ROADMAP PHASES                             |
+-------------------------------------------------------------------------+
| Phase 1: Hardware Foundations & Core Subsystems                         |
| Phase 2: Glassy Blue UI Shell & Vector Component Design System          |
| Phase 3: Display Studio: DDC/CI Hardware & Sub-Zero Dimming             |
| Phase 4: Audio Studio: Real-Time DSP, 60 FPS Spectrum & APO Bridge      |
| Phase 5: Quick Flyout (Win+Alt+P), Hotkeys & Automatic Automation       |
| Phase 6: Performance Hardening, Zero-Privilege Tray & Deployment Packaging|
+-------------------------------------------------------------------------+
```

---

## 2. Phase-by-Phase Breakdown

### Phase 1: Hardware Foundations & Core Subsystems
**Primary Goal:** Construct the low-level, unmanaged Win32 interop libraries and verify direct communication with monitor microcontrollers and Core Audio endpoints.

* **Key Deliverables:**
  * **Win32 P/Invoke Layer:** Implement robust bindings for `dxva2.dll` (`GetPhysicalMonitorsFromHMONITOR`, `GetVCPFeatureAndVCPFeatureReply`, `SetVCPFeature`), `user32.dll` (`EnumDisplayMonitors`, `MonitorFromWindow`), and `ole32.dll`.
  * **Resilient VCP Prober:** Implement the multi-stage capability prober that bypasses the known Windows `GetMonitorCapabilities` bug (error `0xC0262587`) and queries individual VCP registers (`0x10`, `0x12`, `0x16`, `0x18`, `0x1A`, `0x60`, `0xD6`).
  * **Asynchronous I2C Write Queue:** Create a thread-safe `System.Threading.Channels` worker with 50ms command rate-limiting and 40ms inter-command wire pacing.
  * **Core Audio Enumerator:** Implement COM wrappers for `IMMDeviceEnumerator`, `IMMDeviceCollection`, and `IAudioEndpointVolume` to detect active playback endpoints, volume levels, and mute status.
  * **Persistence Scaffolding:** Create the atomic JSON file storage engine (`profiles.json`, `hardware_cache.json`).
* **Validation Criteria:**
  * Unit tests successfully read and set brightness on the Acer ED270 X0 from 0 to 100 with zero I2C bus lockups.
  * Audio enumeration accurately identifies connected devices (`Cosmic Byte Proteus`, `Headphones`).

---

### Phase 2: Glassy Blue UI Shell & Vector Component Design System
**Primary Goal:** Build the presentation shell adhering to the "Aero Glass 2.0" visual specification with fluid responsiveness.

* **Key Deliverables:**
  * **Windows 11 Mica/Acrylic Glass Chrome:** Implement `DwmSetWindowAttribute` window procedure hooks to engage native Windows 11 desktop refraction.
  * **Custom XAML Control Templates:**
    * Precision Glass Slider with electric cyan gradient fill, glow hover states, and smooth drag mechanics.
    * Draggable Segmented Toggle Switches (Headphones vs Speakers).
    * Custom Dropdown Comboboxes with frosted glass popups.
  * **Responsive Main Window Layout:** Implement the two-pane architecture (Sidebar Navigation + Studio Content Area) with fluid resizing and minimum window bounds (980 x 640).
  * **MVVM Framework Integration:** Configure `CommunityToolkit.Mvvm` with dependency injection, messaging, and observable viewmodels.
* **Validation Criteria:**
  * UI renders at a constant 60 FPS with zero layout stutter.
  * Memory footprint of the UI shell remains below 35 MB working set.

---

### Phase 3: Display Studio: DDC/CI Hardware & Sub-Zero Dimming
**Primary Goal:** Deliver the complete display control suite with multi-monitor support and hybrid software dimming.

* **Key Deliverables:**
  * **Display Studio View & ViewModel:**
    * Dynamic hardware-probed controls (only displaying RGB controls if VCP `0x16/18/1A` are supported).
    * Dual-range Slider: 0–100% controls physical backlight; <0% activates Sub-Zero overlay dimming.
    * Input Source switching dropdown (DisplayPort, HDMI-1, HDMI-2 via VCP `0x60`).
    * Display Presets: 1-click application of calibrated values (*Daylight 6500K*, *Reading 4500K*, *Cinema*, *Game 240Hz*).
  * **Sub-Zero Overlay Dimmer:**
    * Transparent, click-through, tool-window overlay (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE`) covering the target monitor coordinates.
    * Smooth alpha fade transitions without mouse cursor occlusion.
  * **Hardware Diagnostics View:** Dedicated screen displaying full VCP register dump, monitor serial numbers, and bus health status.
* **Validation Criteria:**
  * Acer ED270 X0 transitions smoothly between profiles in under 150ms.
  * Clicking through the Sub-Zero Dimmer allows 100% normal desktop and game interaction without capturing focus.

---

### Phase 4: Audio Studio: Real-Time DSP, 60 FPS Spectrum & APO Bridge
**Primary Goal:** Implement mathematically accurate audio signal processing, live audio spectrum analysis, and the system-wide APO bridge.

* **Key Deliverables:**
  * **WASAPI Event-Driven Loopback Streamer:**
    * Captures default output stream via `AUDCLNT_STREAMFLAGS_LOOPBACK` into a lock-free circular ring buffer.
  * **60 FPS Audio Spectrum Canvas:**
    * Hann-windowed 1024-point FFT running on a high-priority background worker.
    * 32-band logarithmic bar rendering with peak-hold ballistics.
  * **10-Band Graphic & Parametric EQ:**
    * Robert Bristow-Johnson biquad filter implementations with SIMD AVX2 acceleration (`System.Numerics.Vector<float>`).
    * Interactive spline curve editor with draggable control points.
  * **Acoustic Enhancement Modules:**
    * Mid-Side Stereo Widener with dialogue preservation.
    * Dynamic Range Compressor & Soft-Knee Limiter with adjustable threshold and release.
  * **System-Wide APO Bridge:**
    * Generates and updates Equalizer APO configuration files in real time.
    * Allows system-wide sound shaping across Spotify, Chrome, Discord, and games.
* **Validation Criteria:**
  * Spectrum analyzer operates at a silky 60 FPS with < 0.2% CPU overhead.
  * Audio DSP processing exhibits zero audible buffer underruns, clicks, or pops.

---

### Phase 5: Quick Flyout (Win+Alt+P), Hotkeys & Automatic Automation
**Primary Goal:** Provide instant desktop accessibility and intelligent profile switching.

* **Key Deliverables:**
  * **Compact Floating Quick-Control Flyout:**
    * Borderless acrylic popup displaying core sliders and switches.
    * Global keyboard shortcut listener (`RegisterHotKey` for `Win + Alt + P` and custom brightness increments `Ctrl + Alt + Up/Down`).
    * Auto-dismiss on loss of focus.
  * **Unified Environment Presets:**
    * Synchronized Display + Audio profile bundling (e.g. *Late Night* = 20% Brightness + 4500K Warmth + Headphone Night Compressor).
  * **Automation & Process Watcher:**
    * Win32 Event Hook (`SetWinEventHook` listening to `EVENT_SYSTEM_FOREGROUND`) to detect when specific games/applications gain focus and auto-trigger profiles.
    * Sunrise/Sunset solar angle calculator for scheduled day/night color temperature shifts.
* **Validation Criteria:**
  * Quick Flyout appears in < 60ms upon pressing `Win + Alt + P`.
  * Launching a configured application smoothly switches display and audio profiles within 200ms.

---

### Phase 6: Performance Hardening, Zero-Privilege Tray & Deployment Packaging
**Primary Goal:** Optimize memory, enforce zero-crash guarantees, and create a production installer.

* **Key Deliverables:**
  * **Working Set Memory Trimming:**
    * Implement `SetProcessWorkingSetSize` trim on minimize to system tray, reducing background RAM consumption to < 20 MB.
  * **Single Instance & IPC Pipe:**
    * Named Mutex `Global\PRISM_SingleInstance_Mutex` and Named Pipe server to handle secondary launches cleanly.
  * **Resilient Exception Handlers:**
    * Global AppDomain and Dispatcher exception catchers with localized error logging.
  * **Production Deployment:**
    * Clean InnoSetup / MSIX package with optional auto-start with Windows registry registration (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
    * Zero administrator rights required for standard operation (UAC elevation only if user opts into APO system-wide registry registration).
* **Validation Criteria:**
  * Continuous 48-hour background burn-in test shows zero memory leaks and zero CPU spikes.
  * Complete uninstallation removes 100% of files cleanly without leaving registry remnants.

---

## 3. Milestones & Target Timeline

```
+---------------------------------------------------------------------------------------+
| Milestone 1: "Hardware Verified"       -> Phase 1 Complete (I2C DDC/CI & CoreAudio)   |
| Milestone 2: "Aero Glass Prototype"   -> Phase 2 Complete (WPF Mica Shell & Controls)|
| Milestone 3: "Display Studio MVP"      -> Phase 3 Complete (Hardware + Sub-Zero Dim)  |
| Milestone 4: "Audio Studio MVP"        -> Phase 4 Complete (DSP + Spectrum + APO)     |
| Milestone 5: "PRISM v1.0 Release"      -> Phase 5 & 6 Complete (Full Flyout & Release)|
+---------------------------------------------------------------------------------------+
```
