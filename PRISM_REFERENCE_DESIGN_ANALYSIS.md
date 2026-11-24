# PRISM — UI Reference Design Analysis & Translation Architecture

> **Project:** PRISM — Display & Audio Studio  
> **Status:** Phase 1 Architectural Analysis Complete  
> **Target Framework:** Native WPF (.NET 8 Windows Desktop)  
> **References Analyzed:** Liquid Glass Slider, Equalizer Slider, Glass Button  

---

## 1. Executive Summary & Core Objective

The user provided three UI reference packages:
1. **Liquid Glass Slider** (`PRISM_Liquid_Glass_Slider_Reference.zip`)
2. **Equalizer Slider** (`PRISM_Equalizer_Slider_Reference.zip`)
3. **Glass Button** (`2025-05-29-glass-button.zip`)

The goal is **not** to embed HTML/CSS in WebViews, nor to superficially paste three isolated controls into an outdated layout. Rather, these references serve as the aesthetic and interaction foundation for a **holistic visual overhaul of the entire PRISM application**.

This document formally details:
- What will be adopted from each reference.
- What will be improved and optimized for a dark studio interface.
- How each visual concept is translated into native WPF styles, control templates, and animations.
- Where each control pattern will be deployed across PRISM's 7 view surfaces.
- Technical limitations and architectural guarantees (preserving real DDC/CI hardware and WASAPI audio processing).

---

## 2. Reference 1: Liquid Glass Slider

### 2.1 Visual & Interactive Anatomy
- **Track:** Long, sleek, horizontal capsule track (height: 8–10px, radius: 999px) with dark translucent inset styling.
- **Progress Fill:** Dynamic linear gradient spanning vibrant cool-blue to electric cyan (`#49A3FC` to `#3681EE`).
- **Thumb:** Wide, pill-shaped translucent glass capsule (width: 54–64px, height: 26–30px, radius: 999px).
- **Glass Optics:** Multi-layered specular sheen:
  - Base translucent glass background (`rgba(255, 255, 255, 0.08)` to `0.15`).
  - Inset specular rim light (`inset 1px 1px 0 rgba(69, 168, 243, 0.30)`, `inset -1px -1px 0 rgba(255, 255, 255, 0.12)`).
  - Ambient elevation shadow (`0 4px 14px rgba(0, 0, 0, 0.40)`).
- **Interaction:**
  - Hover: subtle track illumination and specular brightness increase.
  - Active/Dragging: tactile scale compression (`scaleY(0.96) scaleX(1.04)`) with grab/grabbing cursor.
  - Smooth value readout with tabular figures.

### 2.2 Translation to Native WPF
- **WPF Control:** `System.Windows.Controls.Slider` (Horizontal).
- **Template Architecture:** Custom `ControlTemplate` (`PrismLiquidGlassSliderStyle`) with:
  - Background track: `Border` with `CornerRadius="5"`, background `#141C2B`, border `#1E293B`.
  - Selection/Progress fill: `Border` with `CornerRadius="5"`, fill `LinearGradientBrush` (Cyan to Sapphire).
  - Thumb: `Thumb` with `ControlTemplate` containing:
    - Outer glass pill `Border` (`CornerRadius="14"`).
    - Inner specular highlight border (`BorderThickness="1"` with subtle linear gradient stroke).
    - Glow drop shadow effect (`DropShadowEffect Color="#00E5FF" BlurRadius="8" Opacity="0.3"`).
    - Smooth Storyboard triggers for `IsMouseOver` and `IsDragging`.
- **Where Applied in PRISM:**
  - Display Studio Brightness & Contrast sliders.
  - Display Studio RGB Gain sliders (customized with red, green, blue accent tints).
  - Audio Studio Master Volume slider.
  - Persistent Floating Bottom Quick Control Dock (Brightness & Volume).
  - Quick Control Flyout Window (`QuickControlFlyoutWindow.xaml`).
- **Improvements & Dark Studio Adaptations:**
  - The CSS reference used a light background (`#f8f8f8`). PRISM adapts the liquid glass thumb to deep charcoal surfaces (`#0B0F19`), ensuring high contrast against the luminous progress track.
  - Hardware Throttling: Slider updates to DDC/CI monitor luminance are debounced (150ms) to prevent I2C bus flooding while maintaining instant UI responsiveness.

---

## 3. Reference 2: Equalizer Slider

### 3.1 Visual & Interactive Anatomy
- **Enclosure:** Compact, technical audio console frame (`#20242D` / `#111827`) with subtle border elevation.
- **Fader Track:** Slim vertical channel (width: 14–18px, height: 160–190px, corner radius: 4px) with inset dark graphite background.
- **Spectral Fill:** Vertical multi-stop gradient indicating frequency energy:
  - Deep Sub-Bass: Deep Indigo / Navy (`#214893`)
  - Mid Bass: Royal Blue (`#2769D3`)
  - Midrange: Sky Blue (`#55A5E6`)
  - High Air: Luminous Ice Blue (`#6FC5F0`)
- **Fader Cap (Thumb):** Tactile hardware mixer fader cap (width: 28px, height: 16px) with horizontal grip indicator slit and cyan position glow.
- **Labels & Metrics:**
  - Top: High-contrast dB metric (`+6.0 dB`, `0.0 dB`, `-3.5 dB`).
  - Center: 0 dB unity gain reference hash mark.
  - Bottom: ISO standard frequency label (`31 Hz`, `62 Hz`, `125 Hz`, ..., `16 kHz`) with functional acoustic role (`Sub-bass`, `Kick punch`, `Warmth`, ..., `Air`).

### 3.2 Translation to Native WPF
- **WPF Control:** `System.Windows.Controls.Slider` (Vertical, `Orientation="Vertical"`).
- **Template Architecture:** Custom `ControlTemplate` (`PrismVerticalEqSliderStyle`) with:
  - Vertical track `Border` with inset border and dark backing.
  - Active fill `Border` clipped to current value.
  - Custom `Thumb` styled as an anodized aluminum fader cap with a center index line.
  - Reference grid lines at +12 dB, 0 dB, -12 dB.
- **Where Applied in PRISM:**
  - 10-Band Graphic Equalizer in Audio Studio.
  - Parametric filter gain sliders.
- **Improvements & Audio DSP Integration:**
  - The CSS reference was a static 15-slider mockup. PRISM implements an authentic **10-Band ISO Graphic Equalizer** (-12.0 dB to +12.0 dB) with zero at exact geometric center.
  - Dynamic Transfer Curve: Moving any fader instantly updates the mathematical Biquad transfer function polygon drawn above the EQ bank and feeds real-time audio through `PrismAudioEngine`.

---

## 4. Reference 3: Glass Button

### 4.1 Visual & Interactive Anatomy
- **Geometry:** Refractive, liquid-glass pill button (`border-radius: 999vw`).
- **Surface Optics:**
  - Subtle angled gradient background (`linear-gradient(-75deg, rgba(255,255,255,0.06), rgba(255,255,255,0.18), rgba(255,255,255,0.06))`).
  - Dual-edge specular reflection (`inset 0 1px 1px rgba(255,255,255,0.40)`, `inset 0 -1px 1px rgba(0,0,0,0.30)`).
  - Ambient diffuse drop shadow with multi-tier blur.
- **Interaction:**
  - Hover: Subtle scale contraction (`scale(0.98)`), increased backdrop luminescence, animated specular sheen sweep.
  - Pressed: Tactile depression (`scale(0.95)`), shadow tightening, authentic mechanical feel.
  - Focus: Luminous cyan focus ring (`0 0 0 2px #00E5FF`).
  - Disabled: Reduced opacity (0.35), disabled hit testing.

### 4.2 Translation to Native WPF
- **WPF Control:** `System.Windows.Controls.Button`.
- **Template Architecture:**
  - `PrismGlassButtonStyle` (Secondary/Standard): Translucent glass surface, specular border, cyan hover glow.
  - `PrismGlassPrimaryButtonStyle` (Call-to-Action): Electric cyan gradient glass fill with crisp dark text.
  - `PrismGlassWarningButtonStyle` (Reset/Rollback): Amber-tinted glass surface for safe reset confirmations.
- **Where Applied in PRISM:**
  - Main Actions: "Open Display Studio", "Open Audio Studio", "Play Audition Track".
  - Mode Changes: "Apply Mode", "Keep Changes", "Revert Now".
  - Audition Bench: "A/B Bypass (Dry / Wet)", Test Track Selectors.
  - Reset to Defaults buttons across all screens.

---

## 5. Visual Hierarchy & Design System Tokens

| Design Token | Token Value | Purpose / Surface |
| :--- | :--- | :--- |
| `PrismCanvasBrush` | `#080C14` | Deepest application background |
| `PrismSurfaceBaseBrush` | `#0D131F` | Primary panel and view surface |
| `PrismCardGlassBrush` | `#121927` (85% Opacity) | Standard elevated cards |
| `PrismElevatedGlassBrush` | `#162032` (92% Opacity) | Prominent controls, bottom dock, flyout |
| `ElectricCyanBrush` | `#00E5FF` | Primary active accent, focus rings |
| `SapphireBlueBrush` | `#38BDF8` | Secondary blue accent, progress tracks |
| `WarningAmberBrush` | `#F59E0B` | Reset actions, countdown warnings |
| `SuccessGreenBrush` | `#10B981` | Hardware connected, DSP active indicator |
| `TextPrimaryBrush` | `#F8FAFC` | Headings, active values, button labels |
| `TextSecondaryBrush` | `#94A3B8` | Subtitles, parameter descriptions |
| `TextTertiaryBrush` | `#64748B` | Metric units, frequency subtitles |

---

## 6. Implementation & Verification Roadmap

1. **Tokens & Controls (`PrismColors.xaml` & `PrismControls.xaml`):**
   - Implement `PrismLiquidGlassSliderStyle`.
   - Implement `PrismVerticalEqSliderStyle`.
   - Implement `PrismGlassButtonStyle` and `PrismGlassPrimaryButtonStyle`.
2. **Horizontal Slider Replacement:**
   - Update `MainWindow.xaml` bottom dock.
   - Update `DisplayStudioView.xaml` (Brightness, Contrast, RGB).
   - Update `AudioStudioView.xaml` (Volume, Balance, Bass, Treble, Width, Preamp).
   - Update `QuickControlFlyoutWindow.xaml`.
3. **Equalizer Fader Replacement:**
   - Update `AudioStudioView.xaml` 10-Band EQ grid with new vertical fader caps, dB readouts, and frequency labels.
4. **Glass Button Integration:**
   - Upgrade hero buttons, audition controls, bypass toggle, and reset buttons across all views.
5. **Quality Assurance & Verification:**
   - Clean Release & Debug compilation.
   - 100% test pass rate across all 53+ automated unit/integration tests.
   - Live hardware verification with connected Acer ED270 X0 monitor and WASAPI audio loopback.
   - In-memory WPF visual rendering to high-res PNG screenshots for all redesigned views.
