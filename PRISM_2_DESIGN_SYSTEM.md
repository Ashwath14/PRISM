# PRISM 2.0 — Midnight Graphite Design System & Material Architecture

**Date:** November 20, 2026  
**Product Version:** PRISM 2.0.0-phase2  
**Design Direction:** Midnight Graphite  
**Inspiration:** Apple macOS Control Center, Apple Logic Pro, Leica Studio Precision, Native Windows Modern Fluent

---

## 1. Palette Specification

The **Midnight Graphite** design system is the permanent visual identity of PRISM 2.0. It eschews saturated neon blues and gaming RGB aesthetics in favor of a calm, professional, high-legibility dark studio palette.

### 1.1 Main Surfaces

| Token | Hex Value | WPF Resource Key | Purpose |
|---|:---:|---|---|
| **AppBackground** | `#0D1117` | `PrismCanvasBrush` / `AppBackgroundBrush` | Main window canvas, root backdrop |
| **SurfacePrimary** | `#161B22` | `PrismSidebarBrush` / `SurfacePrimaryBrush` | Sidebar navigation, primary structural panels |
| **SurfaceSecondary**| `#1F2630` | `PrismCardGlassBrush` / `SurfaceSecondaryBrush`| Content cards, module containers, control decks |
| **SurfaceElevated** | `#2A3342` | `PrismElevatedGlassBrush` / `SurfaceElevatedBrush`| Floating dialogs, Quick Control flyout, popovers |
| **SurfaceGlass** | `#202A38` | `GlassSurfaceBrush` / `SurfaceGlassBrush` | Translucent glass base (65% to 85% opacity) |

### 1.2 Domain & Semantic Accents

| Token | Hex Value | WPF Resource Key | Purpose & Domain Rule |
|---|:---:|---|---|
| **DisplayAccent** | `#76C7C0` | `DisplayAccentBrush` / `TealAccentBrush` | Muted Teal — Exclusively for display luminance, contrast, DDC/CI controls |
| **AudioAccent** | `#A99AC8` | `AudioAccentBrush` / `LavenderAccentBrush` | Soft Lavender — Exclusively for audio volume, 10-band EQ, acoustic profiles |
| **PremiumAccent** | `#C6A77A` | `PremiumAccentBrush` / `ChampagneAccentBrush` | Warm Champagne — High-value highlights, custom presets, active master toggles |
| **Success** | `#63C99A` | `SuccessGreenBrush` | Operational verified state, connected monitor, active loopback DSP |
| **Warning** | `#D6A76A` | `WarningAmberBrush` | Auto-rollback countdown, DDC/CI retry latency, headroom clipping warnings |
| **Error** | `#E17D85` | `ErrorRedBrush` | Hardware write failure, I2C bus collision, missing audio device |

### 1.3 Typography & Hierarchy

| Token | Hex Value | WPF Resource Key | Contrast Ratio & Usage |
|---|:---:|---|---|
| **TextPrimary** | `#E8E7E2` | `TextPrimaryBrush` | 13.8:1 (AAA) — Main headings, device names, active slider values |
| **TextSecondary** | `#B9C0CA` | `TextSecondaryBrush` | 9.2:1 (AAA) — Body descriptions, section labels, control names |
| **TextMuted** | `#8993A0` | `TextMutedBrush` / `TextTertiaryBrush` | 5.5:1 (AA) — Metadata, port registers, hardware timestamps |
| **BorderSubtle** | `#343E4C` | `PrismBorderSubtleBrush` | Card boundaries, dividers, slider troughs |

---

## 2. Liquid Glass Material Stack

True physical glass in dark interfaces requires precise multi-layer optics rather than flat transparency. PRISM 2.0 implements a 5-layer optical composite:

```
┌────────────────────────────────────────────────────────┐  ◄── Layer 5: 1px Specular White Top Bevel (#90FFFFFF)
│ ┌────────────────────────────────────────────────────┐ │
│ │ ░░░ Layer 3: 60° Internal Diagonal Reflection Sheen│ │
│ │                                                    │ │
│ │         Layer 2: Smoked Translucent Body           │ │  ◄── Layer 4: Anisotropic Rim Light Border
│ │                  (#CC1F2630)                       │ │      (Top: #80FFFFFF ──► Bottom: #1AFFFFFF)
│ │                                                    │ │
│ └────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────┘
   ░░░░░░░ Layer 1: Contact Drop Shadow (Blur 24px, #000000 45%) ░░░░░░░
```

1. **Layer 1 (Contact Drop Shadow):** High-spread dark shadow (`BlurRadius="24"`, `Direction="270"`, `Color="#000000"`, `Opacity="0.45"`) establishing spatial elevation above the deep graphite backdrop.
2. **Layer 2 (Smoked Translucent Body):** Dense graphite tint (`#CC1F2630`) filtering background visual clutter while allowing wallpaper ambient colors to diffuse through softly.
3. **Layer 3 (Internal Reflection Sheen):** An anisotropic 60-degree linear sheen (`#1AFFFFFF` fading to `#00FFFFFF`) simulating interior light refraction within thick crystal.
4. **Layer 4 (Anisotropic Rim Lighting):** Top-down border gradient transitioning from `#80FFFFFF` at the top edge to `#1AFFFFFF` at the bottom edge, mimicking physical ceiling downlighting.
5. **Layer 5 (Specular Upper Bevel):** 1px sharp white highlight band (`#90FFFFFF`) along the interior top border, providing a crisp tactile physical edge.

---

## 3. Typography Scale & Engine Configuration

All text is rendered using Windows 11 `Segoe UI Variable Display` (with `Segoe UI` and `Inter` fallbacks), pinned to integer device pixels via WPF MilCore:

```xml
<Setter Property="TextOptions.TextFormattingMode" Value="Display" />
<Setter Property="TextOptions.TextRenderingMode" Value="ClearType" />
<Setter Property="RenderOptions.ClearTypeHint" Value="Enabled" />
<Setter Property="SnapsToDevicePixels" Value="True" />
<Setter Property="UseLayoutRounding" Value="True" />
```

### Hierarchy Scale

| Role | Font Size | Weight | Line Height | Usage |
|---|:---:|:---:|:---:|---|
| **H1 Display** | 30–32 px | Bold (700) | 38 px | Screen hero headers ("Display & Audio, in Sync.") |
| **H2 Section** | 22–24 px | Semibold (600) | 30 px | Major section titles, studio names |
| **H3 Card** | 16–18 px | Semibold (600) | 24 px | Device identity ("ED270 X0"), module names |
| **Body Primary**| 13–14 px | Regular (400) | 20 px | Informational descriptions, status summaries |
| **Body Secondary**| 12–13 px | Regular (400) | 18 px | Secondary subtitles, helper text |
| **Numeric Value**| 15–16 px | Bold (700) | 20 px | Live percentages (45%, 79%), Hz indicators |
| **Badge / Pill**| 10–11 px | Semibold (600) | 14 px | Status indicators ("Connected", "Active Mode") |

---

## 4. Component Library Architecture

### 4.1 Glass Buttons
- **Primary Teal Glass Button:** Used for display actions ("Apply Mode", "Keep Changes"). Filled with muted teal gradient (`#76C7C0` to `#5EA8A2`), dark text (`#0D1117`), and specular rim highlight.
- **Primary Lavender Glass Button:** Used for audio actions ("Apply Preset", "Play Audition Track"). Filled with soft lavender gradient (`#A99AC8` to `#8F80B0`), dark text (`#0D1117`).
- **Secondary Glass Button:** Smoked graphite body (`#CC1F2630`), anisotropic border (`#80FFFFFF` to `#1AFFFFFF`), primary text (`#E8E7E2`).
- **Warning Champagne Button:** Champagne tinted glass (`#33C6A77A` body, `#C6A77A` text) for destructive or reversible operations ("Restore Defaults", "Reset All").

### 4.2 Tactile Sliders
- **Display Slider:** Muted teal glowing fill track (`#76C7C0`), dark trough (`#161B22`), and smooth capsule thumb with 1px white rim.
- **Audio Slider:** Soft lavender glowing fill track (`#A99AC8`), dark trough (`#161B22`), and smooth capsule thumb.
- **RGB Gain Sliders:** Individual Red (`#E17D85`), Green (`#63C99A`), and Blue (`#76C7C0` / `#8B9CF6`) tracks with corresponding live numeric feedback.

### 4.3 Motion Physics & Dispatcher Pacing
- **Hover Transitions:** 120 ms cubic ease-out.
- **Press Interactions:** 90 ms linear ease.
- **Toggle Switches:** 160 ms quintic ease-out.
- **Spectrum Analyzer Loopback:** Gated at 25 ms (~40 FPS) with `DispatcherPriority.Render` to protect 60 FPS slider animations.
