# PRISM — Known Limitations & Hardware Boundaries
**Document Version:** 1.0.0  
**Phase:** Phase 1 Final Audit  
**Date:** November 20, 2026  

---

## 1. Overview

This document provides transparent, in-depth technical documentation of all hardware constraints, firmware quirks, operating system boundaries, and architectural limitations discovered during the engineering and validation of PRISM Phase 1.

Understanding these boundaries ensures reliable real-world deployment and sets the technical agenda for future maintenance and Phase 2 enhancements.

---

## 2. Display & DDC/CI Hardware Boundaries

### 2.1 Acer ED270 X0 Firmware Anomaly: Broken Capabilities String
- **Technical Observation:** Calling the Win32 API `GetCapabilitiesStringLength` or `CapabilitiesRequestAndCapabilitiesReply` against the Acer ED270 X0 returns `FALSE` with error code `0x1F` (`ERROR_GEN_FAILURE`) or delivers a zero-byte truncated string.
- **Root Cause:** The monitor's embedded scaler firmware does not implement a compliant VESA MCCS capabilities string in its internal ROM.
- **Impact on Third-Party Tools:** Many generic open-source monitor utilities fail completely on this display or report "DDC/CI Not Supported".
- **PRISM Solution:** PRISM does not rely on the high-level capabilities string. Instead, it utilizes **direct register probing** (`GetVCPFeatureAndVCPFeatureReply`) across confirmed VESA VCP addresses (0x10, 0x12, 0x16, 0x18, 0x1A, 0x60). Hardware communication succeeded 100% using this approach.

### 2.2 I2C Wire Pacing & Scaler MCU Debounce
- **Technical Observation:** Sending rapid, sequential I2C writes over the DisplayPort AUX channel (for example, dragging a brightness slider rapidly without throttling) leads to dropped I2C frames, temporary bus lockup, or unresponsive scaler controls.
- **Root Cause:** The Acer ED270 X0 scaler microcontroller operates at a low clock rate and requires at least 40-50ms to process a register write and commit it to internal PWM registers.
- **PRISM Solution:** PRISM enforces a **50ms debouncer** on user inputs and serializes all I2C transactions through a **50ms wire pacing semaphore (`_busSemaphore`)**. UI sliders remain visually smooth at 60 FPS while physical writes are throttled to the monitor's processing cadence. Immediate readback must account for this debouncing delay (test assertions require a 200ms settling delay).

### 2.3 Unsupported VCP Registers on Acer ED270 X0
- **VCP 0x14 (Color Preset / Temperature):** Calling `SetVCPFeature(0x14, ...)` returns Win32 error code `0x1F`. When the monitor is operating in standard user mode, the firmware disallows direct color preset selection via DDC/CI. PRISM exposes fine-grained RGB gain controls (0x16, 0x18, 0x1A) instead, which are fully supported.
- **VCP 0xD6 (Power Mode):** Standby and power-off commands are disabled by default in PRISM to avoid accidental loss of video signal over DisplayPort AUX channels.

---

## 3. Sub-Zero Software Dimmer Boundaries

### 3.1 Fullscreen Exclusive (FSE) DirectX Games
- **Technical Observation:** Sub-Zero software dimming applies an alpha-blended layered window (`WS_EX_LAYERED | WS_EX_TRANSPARENT`) across the desktop work area.
- **Boundary Condition:** In games running in true "Fullscreen Exclusive" mode, the game engine bypasses the Windows Desktop Window Manager (DWM) compositor to write directly to the GPU display scanout. The layered dimming window is bypassed in this exclusive mode.
- **Mitigation:**
  - In Fullscreen Exclusive games, users should utilize hardware DDC/CI brightness adjustments (which control the physical backlight).
  - For games running in "Borderless Windowed" or "Windowed" mode (the modern Windows 11 default), Sub-Zero dimming works seamlessly.

### 3.2 Windows HDR (High Dynamic Range) Interaction
- **Technical Observation:** When Windows HDR is active, the DWM compositor operates in scRGB / PQ color space.
- **Boundary Condition:** Hardware DDC/CI brightness directly controls physical backlight intensity and functions identically in SDR and HDR. However, software dimming layers and RGB color gains modify pixel values before tone mapping; excessive RGB gain boosting in HDR can lead to highlight clipping in games or media players.
- **Guidance:** PRISM presets maintain balanced RGB gains (<= 50) when HDR mode is active.

---

## 4. Audio Subsystem & Equalizer APO Boundaries

### 4.1 Dependency on Third-Party Audio Processing Object (APO)
- **Architectural Boundary:** The standard Windows Core Audio API does not provide a native, user-mode parametric equalizer engine that applies to all desktop audio streams without third-party drivers.
- **Host Reality:** Equalizer APO is currently **not installed** on this machine.
- **Current Operational State:**
  - Core Audio master volume, mute, and default endpoint switching: **Fully operational (`PASS`)**.
  - Real-time WASAPI loopback spectrum analyzer: **Fully operational (`PASS`)**.
  - DSP configuration bridge: **Fully operational (`PASS`)** — writes valid filter definitions and automatic preamp gains to `%ProgramFiles%\EqualizerAPO\config\prism_dsp.txt`.
  - System-wide acoustic filtering: **Blocked (`BLOCKED`)** until the user installs Equalizer APO 1.3.2.
- **User Experience:** When APO is absent, PRISM clearly flags the Audio Studio with an installation guide and applies Environments in "Partial Display Mode" without throwing exceptions or failing.

### 4.2 Endpoint Installation & Windows Reboot Requirements
- **Equalizer APO Driver Registration:** Equalizer APO registers as an Audio Processing Object in the Windows registry under `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{Device-GUID}\FxProperties`.
- **Limitation:** When a brand new USB headset or DAC is plugged into the system for the first time, Equalizer APO’s `Configurator.exe` must be run to attach the APO filter to that new endpoint, which may require an audio service restart or Windows reboot. PRISM cannot bypass Windows kernel-level driver registration without administrative elevation.

---

## 5. System Integration & Operating System Boundaries

### 5.1 Zero-Elevation Security Model & Windows UIPI
- **Design Philosophy:** PRISM runs entirely in user-space without requiring administrative elevation (UAC prompt) on startup or runtime.
- **UIPI Boundary:** Under Windows User Interface Privilege Isolation (UIPI), a standard user-space window cannot intercept messages from an elevated (`Run as Administrator`) window.
- **Impact on PRISM:** The global hotkey `Win + Alt + P` works globally across all windows. However, if an elevated application has exclusive foreground focus, clicking through the Sub-Zero dimmer onto that elevated application may in rare cases be blocked by Windows UIPI if the dimmer is active.

### 5.2 Multi-Monitor Hardware Availability
- **Host Workstation:** The current development workstation possesses a single physical monitor (Acer ED270 X0 connected via DisplayPort UID 260).
- **Scope Limit:** PRISM's multi-monitor enumeration architecture (`EnumDisplayMonitors` -> `GetPhysicalMonitorsFromHMONITOR`) is built to scale to N monitors, but simultaneous multi-monitor I2C bus synchronization could only be verified on the single connected physical monitor. Multi-display testing remains a priority for Phase 2 hardware labs.

---

## 6. Summary Matrix

| Subsystem | Boundary / Limitation | Severity | PRISM Architectural Mitigation |
|---|---|---|---|
| **Display** | Acer ED270 X0 broken VCP caps string | Medium | Direct register probing bypasses capabilities string entirely |
| **Display** | Scaler MCU I2C bus lockup on rapid writes | Medium | 50ms software debouncer + 50ms wire pacing semaphore |
| **Display** | VCP 0x14 (Color Preset) rejected by firmware | Low | Replaced with fully functional RGB gain registers (0x16, 0x18, 0x1A) |
| **Dimmer** | Bypassed in Fullscreen Exclusive (FSE) games | Low | Hardware DDC backlight control used; borderless windowed recommended |
| **Audio** | Equalizer APO not installed on host | Medium | Graceful detection, in-app install link, resilient partial environment execution |
| **Audio** | New audio endpoints require APO driver attachment | Low | Documented setup guide; Core Audio volume/mute functions regardless |
| **OS** | Windows UIPI over elevated foreground apps | Very Low | Zero-elevation architecture maintained for seamless startup security |
