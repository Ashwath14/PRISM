# PRISM: Technical Risks, Limitations & Mitigation Strategies
**Comprehensive Failure Mode and Effects Analysis (FMEA) for Display and Audio Systems**

---

## 1. Executive Summary

Low-level Windows hardware interaction carries unique architectural hazards:
* Direct monitor communication via DDC/CI operates over an unbuffered, low-speed physical I2C bus prone to microcontroller lockups.
* Audio processing interacts with the Windows real-time audio subsystem (`audiodg.exe`), where unhandled faults can mute all system audio.
* Display configurations and power states change dynamically across sleep, wake, and docking events.

To guarantee that PRISM operates with rock-solid, commercial-grade stability, every potential risk has been identified, analyzed, and paired with a deterministic software mitigation.

---

## 2. Risk Matrix & Mitigation Blueprint

### Risk 1: DDC/CI Bus Saturation & Monitor Scaler Lockups
* **Severity:** **HIGH** | **Likelihood:** High if unmitigated
* **The Hazard:**
  Standard DDC/CI operates over the I2C physical lines in DisplayPort/HDMI cables at a clock speed of only 100 kHz. When a user drags a brightness slider, standard UI frameworks fire events at 60 Hz or higher. Flooding the monitor's internal scaler microcontroller with rapid I2C write packets will cause the scaler MCU to freeze, leading to monitor flickering, panel blackouts, or Windows graphics driver timeouts (TDR).
* **Engineering Mitigation:**
  1. **Asynchronous Command Pacing:** The UI thread never calls DXVA2 APIs directly. All hardware commands are dispatched to a single-threaded background worker channel (`System.Threading.Channels.Channel<T>`).
  2. **Debounced Leaky-Bucket Queue:** While the user is actively dragging a slider, intermediate commands are dropped. Only the most recent target value is retained and dispatched at a throttled rate (maximum 1 packet every 50ms).
  3. **Mandatory Wire Delay:** An explicit 40ms sleep is enforced between consecutive I2C packets, providing the monitor microcontroller adequate time to process the command and clear its hardware buffer.

---

### Risk 2: Malformed Monitor Capabilities String (The "Unsupported" False Negative)
* **Severity:** **MEDIUM** | **Likelihood:** **CONFIRMED** on test hardware (Acer ED270 X0)
* **The Hazard:**
  Calling the high-level DXVA2 API `GetMonitorCapabilities` on the host's Acer ED270 X0 returns error code `0xC0262587` (`ERROR_GRAPHICS_DDCCI_INVALID_CAPABILITIES_STRING`). Many monitor manufacturers construct non-standard EDID/MCCS capability strings with missing punctuation or syntax errors. If an application relies on `GetMonitorCapabilities`, it will falsely assume DDC/CI is completely unsupported.
* **Engineering Mitigation:**
  1. **Direct Register Polling:** PRISM bypasses high-level capabilities string parsing entirely.
  2. **Graceful VCP Probing:** The initialization sequence directly calls the low-level API `GetVCPFeatureAndVCPFeatureReply` for standard registers (`0x10` Brightness, `0x12` Contrast, `0x16/18/1A` RGB Gains, `0x60` Input Source). If the monitor scaler answers the low-level query (as the Acer ED270 X0 proved it does), the feature is marked fully supported regardless of high-level string errors.

---

### Risk 3: Physical Video Path Drops (Docks, Adapters & MST Hubs)
* **Severity:** **MEDIUM** | **Likelihood:** Medium across varied hardware setups
* **The Hazard:**
  Cheap HDMI/DisplayPort switches, certain USB-C docks, and DisplayPort Multi-Stream Transport (MST) daisy chains often fail to pass the secondary I2C DDC lines through their internal multiplexers. When this occurs, DDC/CI communication fails completely, returning Win32 error `0xC0262582` (`ERROR_GRAPHICS_DDCCI_MONITOR_RETURNED_INVALID_RESPONSE`).
* **Engineering Mitigation:**
  1. **Transparent Diagnostic Messaging:** PRISM's Diagnostics tab clearly informs the user: *"DDC/CI communication blocked by video adapter or hub. Check monitor OSD DDC/CI setting or connect directly via DisplayPort/HDMI."*
  2. **Seamless Software Dimming Fallback:** When hardware DDC/CI writes fail, PRISM automatically transitions to its transparent DirectComposition DWM overlay dimming engine, ensuring the user can still dim their screen comfortably without receiving an error dialog.

---

### Risk 4: Display Topology & Power State Changes (Sleep / Wake / Reconnect)
* **Severity:** **MEDIUM** | **Likelihood:** High
* **The Hazard:**
  When a monitor enters power-saving sleep, changes resolution, or is disconnected, its physical monitor handle (`hPhysicalMonitor`) becomes invalid. Subsequent calls to DXVA2 APIs using stale handles can cause access violations or memory leaks.
* **Engineering Mitigation:**
  1. **Window Message Listener:** PRISM registers a custom Win32 message hook (`HwndSourceHook`) listening for `WM_DISPLAYCHANGE` (`0x007E`) and `WM_POWERBROADCAST` (`0x0218`).
  2. **Immediate Handle Invalidation:** Upon receiving a display change or system sleep notification, PRISM calls `DestroyPhysicalMonitors` on all active handles and temporarily pauses the I2C queue.
  3. **Delayed Re-enumeration:** When `PBT_APMRESUMEAUTOMATIC` or display restabilization is signaled, PRISM waits a safe 1500ms for monitor scaler power-up before re-enumerating displays and restoring calibrated settings.

---

### Risk 5: Audio Engine (`audiodg.exe`) Crashes & Registry Overwrites
* **Severity:** **HIGH** | **Likelihood:** Medium (if using custom kernel/APO injections)
* **The Hazard:**
  Implementing a custom System Effects APO (sAPO) requires injecting a COM DLL directly into Windows’ `audiodg.exe` process. Any unhandled memory fault in the DSP code immediately crashes `audiodg.exe`, cutting off all sound on the computer. Furthermore, major Windows 11 cumulative updates routinely reset custom `FxProperties` registry keys.
* **Engineering Mitigation:**
  1. **Two-Tier Architecture:** For v1.0, PRISM does not inject unvetted DLLs into `audiodg.exe`. 
  2. **Stable APO Bridge:** System-wide DSP is achieved by generating configuration instructions for the battle-tested, community-audited Equalizer APO engine or operating via safe user-mode WASAPI redirection.
  3. **Read-Only Loopback for Visuals:** Live 60 FPS spectrum analysis uses `AUDCLNT_STREAMFLAGS_LOOPBACK`, which is 100% read-only and mathematically incapable of crashing Windows audio.

---

### Risk 6: DSP Latency, Buffer Underruns & CPU Overhead
* **Severity:** **LOW-MEDIUM** | **Likelihood:** Low with SIMD
* **The Hazard:**
  Real-time audio processing requires processing thousands of samples every millisecond. Poorly optimized DSP code running on the UI thread will cause audible pops, clicks (buffer underruns), and noticeable mouse cursor lag.
* **Engineering Mitigation:**
  1. **AVX2 SIMD Vectorization:** All biquad filter loops and compressor envelope algorithms utilize `System.Numerics.Vector<float>`, processing 8 single-precision floating-point samples per clock cycle.
  2. **Lock-Free Ring Buffers:** Audio capture uses an atomic circular buffer without thread locks (`Monitor.Enter`), preventing priority inversion between audio threads and visualizer threads.
  3. **Zero Heap Allocation:** Processing loops operate strictly on pre-allocated float arrays; zero memory allocations occur within the real-time audio loop, eliminating garbage collection pauses.

---

### Risk 7: Elevation & Security Boundary (UAC Prompts)
* **Severity:** **LOW** | **Likelihood:** Low
* **The Hazard:**
  Utilities that demand Administrator rights (UAC elevation) on every launch frustrate users, create security exposure, and fail to start automatically at Windows login.
* **Engineering Mitigation:**
  * **Standard User Operation:** 99% of PRISM’s functionality (DXVA2 DDC/CI, Core Audio switching, WASAPI loopback, DWM overlay dimming, presets, global hotkeys) runs completely within **Standard User (Medium Integrity)** privileges.
  * PRISM never requests UAC elevation during daily use or startup. UAC elevation is strictly isolated to a one-time setup action if the user explicitly chooses to install system-wide APO registry bindings.

---

### Risk 8: Software Dimming Cursor Brightness & Fullscreen Games
* **Severity:** **LOW** | **Likelihood:** Medium
* **The Hazard:**
  When using software DWM layered overlays for Sub-Zero night dimming, the Windows hardware mouse cursor is rendered by the graphics display engine *above* the overlay, remaining bright. Additionally, exclusive fullscreen 3D games may bypass the desktop window manager.
* **Engineering Mitigation:**
  1. **Hardware-First Priority:** PRISM always drives physical hardware brightness down to `0` first before engaging the software overlay, ensuring the panel emits the lowest possible backlight.
  2. **Cursor Tinting / Color Inversion:** When Sub-Zero mode is active, PRISM can optionally load a dimmed mouse cursor scheme (`SetSystemCursor`) or apply subtle gamma ramp scaling if the user desires total cursor dimming.
  3. **Graceful Deactivation in Fullscreen:** If an exclusive fullscreen game is detected via `GetForegroundWindow`, PRISM temporarily suspends the overlay to prevent visual conflicts.

---

## 3. Summary of Design Guarantees

| Failure Scenario | Worst-Case Impact (Unmitigated) | PRISM Deterministic Behavior |
| :--- | :--- | :--- |
| User rapidly drags brightness slider 100 times | Monitor scaler MCU freezes, screen blinks | Queue drops intermediate values; writes once every 50ms |
| Monitor returns invalid capabilities string | App displays "Unsupported Monitor" error | Bypasses string; directly reads and writes VCP registers |
| Display cable is unplugged while app is running | NullReferenceException / Crash | Catches `WM_DISPLAYCHANGE`, safely frees handles, waits for reconnect |
| Computer goes into Sleep / Hibernation | Application hangs on wake | Pauses queue, frees handles, re-initializes 1.5s after wake event |
| User plays competitive esports game | Audio lag or system audio crash | Read-only WASAPI loopback; zero latency impact on game audio |
| Audio device is unplugged (headphones removed) | Unhandled COM exception | Listens to `IMMNotificationClient`, seamlessly switches UI to fallback device |
