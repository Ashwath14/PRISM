# PRISM: Technical Architecture & System Design
**Engineering Specification for a High-Performance, Glassy Windows Desktop Application**

---

## 1. Technology Stack Evaluation & Decision Matrix

To ensure PRISM delivers native performance, low memory usage, fluid 60 FPS animations, and seamless low-level hardware interoperability, we conducted an objective evaluation across five candidate Windows desktop technologies:

| Evaluation Criteria | WPF (.NET 8 / 9) | WinUI 3 (Windows App SDK) | Avalonia UI | C++ / Win32 / Direct2D | Electron / Web |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Idle Memory Footprint** | **30 MB – 45 MB** | 65 MB – 95 MB | 45 MB – 65 MB | **15 MB – 25 MB** | 160 MB – 320 MB |
| **Windows 11 Native Mica/Acrylic** | **Full (via DWM APIs)** | Full (Native) | Partial (Platform-dep) | Full (Manual DirectComp) | Poor (CSS Simulation) |
| **P/Invoke & Win32 Interop** | **Zero Overhead / Seamless** | Good | Good | Direct C / Native | Bridge Overhead |
| **Real-Time DSP (SIMD)** | **High (`Vector<float>`, AVX2)** | High (.NET) | High (.NET) | **Ultra-High (Hand-tuned)** | Poor (JS/Wasm latency) |
| **UI Design Freedom & Vector Art** | **Outstanding (XAML/Shapes)** | Moderate (Rigid) | High (XAML) | Difficult (High LOC) | High (CSS/HTML) |
| **System Tray & Headless Mode** | **Effortless** | Problematic (Lifecycle) | Good | Excellent | Heavy Background Cost |
| **Developer Ergonomics** | **High (CommunityToolkit.Mvvm)** | Medium (SDK Churn) | High | Low (Steep curve) | High |

### Architectural Recommendation:
**PRISM will be built on C# .NET 8 (Desktop Runtime) using WPF, augmented by DirectComposition for Windows 11 Mica/Acrylic translucency, and SIMD-accelerated DSP.**

#### Why This Stack Wins:
1. **Host Alignment:** The target system already possesses the `Microsoft.WindowsDesktop.App 8.0.19` runtime installed and active.
2. **Minimal Footprint:** A trimmed, optimized .NET 8 WPF application achieves a working set of ~35 MB, dropping under 20 MB when minimized to the system tray.
3. **Hardware Interoperability:** P/Invoke access to `dxva2.dll`, `user32.dll`, `gdi32.dll`, and Core Audio COM interfaces (`IMMDeviceEnumerator`, `IAudioClient`, `IAudioMeterInformation`) operates with zero marshaling friction.
4. **SIMD Audio Computation:** Modern .NET JIT compiles `System.Numerics.Vector<float>` directly into AVX2 hardware instructions, processing millions of audio samples per second with negligible CPU usage (<0.2%).
5. **Rock-Solid Stability:** Unlike WinUI 3, which still experiences lifecycle anomalies with background system tray apps, WPF’s window messaging pipeline is mature and predictable.

---

## 2. High-Level Modular Architecture

PRISM follows clean architectural separation with strict dependency inversion, structured into six decoupled assemblies:

```
+-------------------------------------------------------------------------+
|                                PRISM.UI                                 |
|  - Windows 11 Glassy Blue Shell (Mica/Acrylic / DWM Blur)               |
|  - Display Studio Views, Audio Studio Views, Preset Manager             |
|  - Interactive Drag-and-Drop EQ Visualizer & 60 FPS Spectrum Canvas     |
|  - Compact Floating Flyout Window (Win+Alt+P)                           |
+-------------------------------------------------------------------------+
                                    |
                                    v (MVVM Data Binding & Relay Commands)
+-------------------------------------------------------------------------+
|                             PRISM.ViewModels                            |
|  - DisplayViewModel, AudioViewModel, QuickControlViewModel              |
|  - CommunityToolkit.Mvvm (ObservableObject, RelayCommand, Messenger)    |
|  - Debounced Input Routers (Throttling UI changes to hardware workers)  |
+-------------------------------------------------------------------------+
          |                                                 |
          v                                                 v
+-----------------------------+   +---------------------------------------+
|    PRISM.Hardware.Display   |   |          PRISM.Hardware.Audio         |
|  - DXVA2 / DDC/CI Low-Level |   |  - CoreAudio MMDevice Enumeration     |
|  - Debounced I2C Write Queue|   |  - WASAPI Loopback Capture (FFT Stream|
|  - DWM Sub-Zero Dim Overlay |   |  - IPolicyConfig Endpoint Switcher    |
|  - Hardware VCP Prober      |   |  - Session Audio Volume & Metering    |
+-----------------------------+   +---------------------------------------+
          |                                                 |
          +-----------------------+-------------------------+
                                  |
                                  v
+-------------------------------------------------------------------------+
|                                PRISM.Dsp                                |
|  - Second-Order Biquad Peaking / Shelf Filters (RBJ EQ Cookbook)        |
|  - SIMD Vectorized Floating-Point Math (`Vector<float>`)                |
|  - Dynamic Range Compressor & Soft-Knee Limiter                         |
|  - Mid-Side Stereo Widening Matrix                                      |
|  - Radix-2 Hann-Windowed Fast Fourier Transform (FFT)                   |
|  - APO Configuration Generator (Equalizer APO Bridge)                   |
+-------------------------------------------------------------------------+
                                  |
                                  v
+-------------------------------------------------------------------------+
|                              PRISM.Storage                              |
|  - JSON Profile Storage with Atomic Transactions & File-Locking         |
|  - Machine Hardware Profile Cache (EDID to capability map)              |
|  - Global Hotkey Configuration Store                                    |
+-------------------------------------------------------------------------+
```

---

## 3. Subsystem Architectural Details

### 3.1 Display Control Pipeline (`PRISM.Hardware.Display`)

The display subsystem manages hardware monitors without risking I2C bus saturation or monitor MCU lockups.

```
[UI Slider Drag]
       |
       v (Values 0 - 100)
[Debounced Queue (50ms Throttling)]
       |
       v (Latest Coalesced Value)
[Async DDC/CI Worker Thread]
       |
       +---> [If VCP 0x10 Supported] --------> [dxva2::SetVCPFeature(0x10)]
       |                                                |
       |                                                v (40ms Inter-packet Sleep)
       |
       +---> [If Value < 0% (Sub-Zero Dim)] -> [DirectComposition DWM Overlay]
```

#### Key Components:
1. **`DdcCiManager`:**
   * Enumerate displays via `EnumDisplayMonitors` and `GetPhysicalMonitorsFromHMONITOR`.
   * Queries hardware capabilities using a resilient direct-VCP probe (testing `0x10`, `0x12`, `0x16`, `0x18`, `0x1A`, `0x60`, `0xD6`).
   * Manages handle lifecycles; calls `DestroyPhysicalMonitors` safely on exit or monitor disconnect.
2. **`I2CWriteQueue`:**
   * Thread-safe `System.Threading.Channels.Channel<DdcCommand>` ensuring sequential, non-overlapping writes to the hardware.
   * Drops redundant intermediate slider values if the user drags rapidly, dispatching only the latest value followed by a mandatory 40ms wire sleep.
3. **`SubZeroDimOverlayWindow`:**
   * High-performance borderless window configured with:
     ```csharp
     SetWindowLong(hwnd, GWL_EXSTYLE, 
         GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
     ```
   * Sits above all normal desktop windows. Mouse clicks and keyboard focus pass directly through without obstruction. Adjusts opacity dynamically when the user drops below 0% hardware brightness.

---

### 3.2 Audio & DSP Pipeline (`PRISM.Hardware.Audio` & `PRISM.Dsp`)

```
+-------------------------------------------------------------------------+
|                  WASAPI Event-Driven Loopback Stream                    |
+-------------------------------------------------------------------------+
                                    |
                                    v (1024-sample floating-point packets)
+-------------------------------------------------------------------------+
|                    Lock-Free Ring Buffer (Interlocked)                  |
+-------------------------------------------------------------------------+
                                    |
                                    v
+-------------------------------------------------------------------------+
|                      Real-Time 60 FPS FFT Worker                        |
|  1. Hann Windowing: w[n] = 0.5 * (1 - cos(2*PI*n / (N-1)))              |
|  2. In-Place SIMD Fast Fourier Transform                                |
|  3. Logarithmic Band Aggregation (32 / 64 Bins)                         |
|  4. Asymmetric Peak-Hold & Falloff Ballistics (Instant Rise, 600ms Decay)|
+-------------------------------------------------------------------------+
                                    |
                                    v
+-------------------------------------------------------------------------+
|               WPF CompositionTarget.Rendering UI Canvas                 |
+-------------------------------------------------------------------------+
```

#### Key Components:
1. **`EndpointManager`:**
   * Wraps `IMMDeviceEnumerator` to track audio output devices, plug/unplug events, and state changes.
   * Switches default Windows audio output device instantly using `IPolicyConfig::SetDefaultEndpoint` (the proven COM interface used by Windows Settings).
2. **`DspChain`:**
   * Executes a cascade of second-order IIR biquad filters for 10-band graphic EQ and parametric EQ.
   * Employs vector math (`System.Numerics.Vector<float>`) for multi-channel stereo processing.
   * Applies mid-side stereo widening and dynamic compression envelope calculation.
3. **`ApoConfigurationBridge`:**
   * Writes live parametric filter instructions to the system-wide Equalizer APO config file (`config.txt`) or shared memory channel.
   * Allows users with the APO installed to experience true system-wide EQ, compression, and spatialization across all Windows apps with zero latency.

---

## 4. UI Shell, Theming & Window Architecture

### 4.1 Windows 11 Visual Styling
* **Backdrop Material:** PRISM leverages `DwmSetWindowAttribute` with `DWMWA_SYSTEMBACKDROP_TYPE = 2` (Mica) or `3` (Acrylic) on Windows 11.
* **Glass Composition:** Multi-layered visual tree:
  1. Base: Semi-transparent deep navy surface (`#0B111E` at 70% opacity).
  2. Blur Layer: Native OS acrylic/Mica refraction.
  3. Specular Layer: 1px subtle inner border (`rgba(255, 255, 255, 0.08)` top/left, `rgba(0, 0, 0, 0.3)` bottom/right) providing the crisp Apple-like glassware feel.
  4. Interactive Accent: Electric cyan (`#00B4D8`) glow shaders on active sliders and toggle nodes.

### 4.2 Compact Floating Quick-Control Flyout
* A secondary lightweight window (`PrismFlyout.xaml`).
* Listens for the global hotkey `Win + Alt + P` registered via `RegisterHotKey`.
* Appears smoothly adjacent to the taskbar notification area or centered on the active display.
* Contains: Master Brightness Slider, Sub-Zero Dim Toggle, Master Audio Volume, Output Device Picker, and Preset Switcher.
* Dismisses automatically on de-activation (`WM_ACTIVATE` / lost focus).

---

## 5. Persistence & Storage Architecture

All configurations are persisted in local, human-readable JSON files inside `%APPDATA%\PRISM\`:
* `profiles.json`: Saved user environments (Display settings + Audio curves).
* `hardware_cache.json`: Detected monitor EDIDs and their probed VCP capabilities.
* `app_settings.json`: General preferences (launch at startup, global shortcuts, active theme).

### Atomic File Transactions:
To eliminate configuration corruption during sudden system shutdowns:
1. Serialize the configuration to a temporary file: `profiles.json.tmp`.
2. Flush disk buffers to guarantee physical write.
3. Atomically replace the existing file using Win32 `ReplaceFile` / `File.Replace`.

---

## 6. Process & Lifecycle Management

* **Single Instance Enforcement:** Guaranteed via a named system mutex: `Global\PRISM_SingleInstance_Mutex`. If a second instance launches, it signals the existing instance via Named Pipes (`\\.\pipe\PRISM_IPC`) to bring the main window to the foreground and exits immediately.
* **Tray Minimization & Memory Trimming:**
  * Closing the main window (`WM_CLOSE`) cancels termination and hides the window (`Visibility = Hidden`).
  * PRISM calls `SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1)` upon minimizing to release unused working set memory back to Windows, keeping the idle footprint below 20 MB.
* **Clean Shutdown:** Unregisters global hotkeys, destroys physical monitor handles (`DestroyPhysicalMonitors`), flushes any pending I2C writes, and releases audio loopback sessions.
