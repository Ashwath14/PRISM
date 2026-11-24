# PRISM — Advanced Feature Research
## Technical Investigation & Architectural Foundation

**Document Version:** 2.2.0  
**Authors:** PRISM Engineering Team  
**Scope:** Windows Display Subsystems, Core Audio, Equalizer APO, Spatial Audio, AutoEq Databases, Color Management

---

## 1. Display Subsystem Research

### 1.1 Resolution & Refresh Rate Management (Win32 CCD & GDI)
Windows provides two distinct API tiers for display mode discovery and management:
1. **User-mode GDI / User32 (`EnumDisplaySettingsEx` & `ChangeDisplaySettingsEx`):**
   - **Enumeration:** `EnumDisplaySettingsEx(lpszDeviceName, modeIndex, ref DEVMODE, dwFlags)` iterates through all valid display modes (`DEVMODE` structure: `dmPelsWidth`, `dmPelsHeight`, `dmDisplayFrequency`, `dmBitsPerPel`).
   - **Mode Validation:** Calling `ChangeDisplaySettingsEx(device, ref devMode, IntPtr.Zero, CDS_TEST, IntPtr.Zero)` verifies whether the display hardware and graphics driver can accept the mode without altering the active desktop.
   - **Dynamic Switching:** Calling `ChangeDisplaySettingsEx(device, ref devMode, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero)` applies the resolution and refresh rate instantly.
   - **Safety / Automatic Rollback Pattern:** When applying a new display mode, a 15-second confirmation countdown timer is started. If the user does not explicitly click "Keep Changes" (e.g. if the monitor goes out of range or flickers black), the application invokes `ChangeDisplaySettingsEx` with the previously stored `DEVMODE` to restore the original resolution and refresh rate automatically.
   - **Native Hardware Constraints:** The connected physical monitor is an **Acer ED270 X0** (Full HD 1920×1080 native resolution, high refresh rate up to 240Hz over DisplayPort / HDMI 2.0). Modes beyond 1920×1080 are not native and will never be misrepresented as 4K.

### 1.2 Windows Advanced Color & HDR Architecture
- **Connecting and Configuring Displays (CCD) API:**
  - Modern Windows 10/11 uses `QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ...)` and `DisplayConfigGetDeviceInfo(...)`.
  - Passing `DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO` (Type 9) returns the `DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO` packet containing:
    - `advancedColorSupported`: Indicates whether the monitor and GPU driver support High Dynamic Range (HDR) or Wide Color Gamut (WCG).
    - `advancedColorEnabled`: Indicates whether Advanced Color (HDR) is currently toggled on in Windows.
    - `colorEncoding`: Reports pixel encoding (`DISPLAYCONFIG_COLOR_ENCODING_RGB`, `YCBCR444`, `YCBCR422`, `YCBCR420`).
    - `bitsPerColorChannel`: Reports color depth (8-bit, 10-bit, 12-bit).
  - Calling `DisplayConfigSetDeviceInfo` with `DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE` enables or disables HDR programmatically.
  - **Hardware Reality & Dolby Vision:** On an 8-bit SDR panel (such as the Acer ED270 X0, which has standard ~250 cd/m² luminance and an 8-bit SDR color pipeline), HDR and Dolby Vision are not natively supported. PRISM provides transparent reporting: "Hardware: SDR 8-bit Panel (Acer ED270 X0). Advanced Color & Dolby Vision are not supported on this display pipeline." No simulated or fake HDR toggles are permitted.

### 1.3 Color Management & Visual Controls
- **Windows Image Color Management (ICM):** `GetICMProfileW(hdc, ref size, filename)` checks whether an ICC color profile is assigned to the display.
- **DDC/CI Hardware RGB Gains (VCP 0x16, 0x18, 0x1A):** Physical video gain registers inside the monitor scaler allow real hardware color temperature adjustment without crushing dynamic range in software.
- **Software Sub-Zero Dimming:** For ultra-dark nighttime viewing below the monitor's minimum hardware luminance (0%), a zero-overhead transparent layered window (`WS_EX_LAYERED | WS_EX_TRANSPARENT`) provides perceptual dimming down to 10% of minimum luminance without affecting screenshot or screen recording pipelines.

---

## 2. Professional Audio Subsystem Research

### 2.1 Core Audio & Windows Endpoint Routing
- **IMMDeviceEnumerator & IMMDevice:** Core Audio APIs allow enumerating all active `eRender` audio endpoints, detecting friendly names (`Cosmic Byte Proteus`, `Realtek Audio`, etc.), volume levels, and mute states.
- **Hardware Channel Balance:** `IAudioEndpointVolume::GetChannelVolumeLevelScalar` and `SetChannelVolumeLevelScalar` allow genuine Left/Right stereo balance calibration directly on the hardware DAC.
- **Active Endpoint Switching:** PRISM interfaces with the Windows shell audio routing service (`IPolicyConfig::SetDefaultEndpoint`) to allow seamless audio output switching from within the PRISM studio, falling back cleanly to Windows Sound Settings if elevation policies require it.

### 2.2 Mathematical Biquad Equalizer & Auto-Preamp Headroom
- **Biquad Digital Filter Transfer Functions:**
  - Standard Robert Bristow-Johnson (RBJ) Audio EQ Cookbook biquad formulas:
    - **Peaking Filter (PK):** $H(s) = \frac{s^2 + s \cdot (A/Q) + 1}{s^2 + s \cdot (1/(A \cdot Q)) + 1}$
    - **Low-Shelf Filter (LS / LSC):** Continuous low-frequency transition with adjustable corner frequency $f_c$ and slope.
    - **High-Shelf Filter (HS / HSC):** Smooth high-frequency shelving for air and presence.
    - **High-Pass (HP) & Low-Pass (LP):** 12 dB/octave resonant filters.
    - **Notch (NO):** High-Q surgical frequency attenuation (e.g., taming 60 Hz mains hum).
- **Inter-Sample Clipping & Headroom Management:**
  - Boosting any frequency band increases the peak signal level. Without attenuation, digital audio signals reaching 0 dBFS will clip, causing severe harmonic distortion.
  - PRISM implements **Auto-Preamp Headroom Calculation**:
    $$\text{Preamp}_{\text{effective}} = \text{Preamp}_{\text{user}} - \max(0, \text{PeakBoost}_{\text{all filters}})$$
  - This ensures complete unity-gain headroom protection at all volume levels.

### 2.3 Headphone Frequency Response Correction (AutoEq Research)
- **AutoEq Project (Jaakko Pasanen):** A widely recognized open-source initiative that normalizes headphone measurements from leading acoustic laboratories (including Oratory1990, Crinacle, InnerFidelity, and Rtings) against the **Harman Target curve** (Harman OE 2018 for over-ear, Harman IE 2019 for in-ear).
- **Target Curve Philosophy:** Neutral acoustic tuning compensates for human ear canal acoustic resonance (concha and pinna gain around 3 kHz) and room reverberation.
- **Pre-computed Reference Correction Profiles:** PRISM incorporates curated parametric profiles for widely used reference headphones:
  - *Sennheiser HD 600 / 650:* Sub-bass lift below 80 Hz, balanced midrange.
  - *Audio-Technica ATH-M50x:* Smooths mid-bass elevation, tames 10 kHz peak.
  - *Sony WH-1000XM4 / XM5:* Attenuates boomy 150 Hz resonance, clarifies 2-4 kHz vocal range.
  - *Apple AirPods Max / Pro:* Harman target refinement with smooth treble extension.
  - *Beyerdynamic DT 770 / 990 Pro:* Tames the sharp 6-8 kHz "Beyer peak" for non-fatiguing monitoring.

### 2.4 Real-Time Audio DSP Enhancements
- **Loudness Normalization:** Compares dynamic peak headroom with Fletcher-Munson equal-loudness curves to maintain consistent perceptual timbre across whispering and high-volume passages.
- **Stereo Widening (Mid/Side Matrix):**
  $$\text{Mid} = \frac{L + R}{2}, \quad \text{Side} = \frac{L - R}{2}$$
  $$\text{Width Factor } w \in [0.0, 2.0]: \quad L' = \text{Mid} + w \cdot \text{Side}, \quad R' = \text{Mid} - w \cdot \text{Side}$$
- **Dynamic Range Compressor / Limiter:** Smooth soft-knee compression with user-adjustable threshold (-30 dB to 0 dB) and ratio (1.5:1 to 8:1) to prevent ear fatigue during action movies or competitive gaming.

---

## 3. Spatial Audio & Advanced Media Research

### 3.1 Windows Spatial Audio APIs
- **ISpatialAudioClient Interface:** Introduced in Windows 10, enabling object-based 3D spatial rendering.
- **Runtime Detection:**
  - Calling `ISpatialAudioClient::GetMaxDynamicObjectCount` returns non-zero (typically 128) when Spatial Audio is active on the selected audio endpoint.
  - Supported formats on Windows:
    - *Windows Sonic for Headphones* (Built-in, license-free, 7.1.4 virtual surround)
    - *Dolby Atmos for Headphones* (Requires Dolby Access application & license)
    - *DTS Headphone:X* (Requires DTS Sound Unbound & license)
- **Honest System Detection:** PRISM checks installed AppX packages (`DolbyLaboratories.DolbyAtmos`, `DTSInc.DTSSoundUnbound`) and active endpoint spatial settings. If Dolby Atmos is installed and licensed, PRISM recognizes it; if not, PRISM clearly directs the user to Windows Sound settings without attempting to spoof or fake Dolby Atmos processing.

### 3.2 Dolby Vision Detection
- Dolby Vision on Windows requires an HDR10/Dolby Vision certified display, a certified GPU driver, HEVC video extensions with Dolby Vision metadata parser (`DolbyLaboratories.DolbyVision`), and a Dolby Vision profile.
- On standard SDR displays, PRISM reports compatibility status transparently.
