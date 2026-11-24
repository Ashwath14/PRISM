# PRISM Design System & Component Architecture
**Document Version:** 2.0.0  
**Phase:** Phase 2 Core Design System  
**Author:** Lead UI/UX Systems Architect  
**Date:** November 20, 2026  

---

## 1. Design Token Architecture

The PRISM design system enforces strict visual cohesion across all modules. Every view, card, slider, button, and flyout consumes centralized design tokens defined in `src/PRISM.App/Styles/DesignTokens.xaml`.

```
PRISM Theme System
├── Palette & Surface Tokens (Hex & Alpha Brushes)
├── Material Tokens (Mica Base, Card Glass, Transient Acrylic)
├── Elevation & Depth Tokens (Border Radii, Layer Dropshadows)
├── Typography Tokens (Font Families, Sizes, Weights, Tracking)
└── Motion & Interaction Tokens (Spring Easing, Duration Constants)
```

---

## 2. Color Palette & Material Specification

### 2.1 Surface & Layer Architecture
Surfaces are stratified into four distinct physical elevations to establish effortless depth without visual clutter:

| Level | Token Name | Hex / RGBA | Role & Visual Behavior |
|:---:|---|---|---|
| **0** | `Prism.Brush.Canvas` | `#080C14` | The window root canvas. Deep midnight space with subtle bottom-up dark gradient. |
| **1** | `Prism.Brush.Sidebar` | `#0B101C` (92% opacity) | Left navigation sidebar surface. Stable, grounded column anchoring the main controls. |
| **2** | `Prism.Brush.Surface.Card` | `rgba(18, 26, 42, 0.65)` | Primary content card surface. Optical glass tinted with indigo-slate, sampling window depth. |
| **3** | `Prism.Brush.Surface.Elevated` | `rgba(28, 38, 60, 0.85)` | Elevated surface for modals, quick flyouts, bottom dock, and active popovers. |
| **Overlay** | `Prism.Brush.Surface.Hover` | `rgba(255, 255, 255, 0.05)` | Dynamic translucent sheen applied on pointer hover. |

### 2.2 Borders & Optical Edges
- `Prism.Brush.Border.Subtle`: `rgba(255, 255, 255, 0.07)` — Standard card and panel boundary.
- `Prism.Brush.Border.Card`: `rgba(255, 255, 255, 0.12)` — Elevated card stroke with subtle top-to-bottom light falloff.
- `Prism.Brush.Border.Interactive`: `rgba(0, 199, 255, 0.40)` — Active/selected outline for focused inputs and active cards.
- `Prism.Brush.Border.Separator`: `rgba(255, 255, 255, 0.06)` — Hairline divider (1px).

### 2.3 Semantic Accents & Status Tints
Accent colors are used intentionally to communicate system domain and hardware state:

```
┌───────────────────────────────────────────────────────────────────────────┐
│ DOMAIN ACCENTS                                                            │
│   Display Accent (Light):     #00C7FF  (Crisp Sky Cyan)                   │
│   Audio Accent (Resonance):   #8B5CF6  (Muted Royal Violet)               │
│   Combined Environment:       #6366F1  (Electric Indigo)                  │
├───────────────────────────────────────────────────────────────────────────┤
│ HARDWARE & STATUS BADGES                                                  │
│   Success (Hardware Active):  #10B981  (Emerald Green)                    │
│   Warning (Partial/Unsaved):  #F59E0B  (Warm Amber)                       │
│   Alert / Peak (Clipping):    #EF4444  (Signal Crimson)                   │
│   Inactive (Uninstalled):     #64748B  (Slate Neutral)                    │
└───────────────────────────────────────────────────────────────────────────┘
```

### 2.4 Text & Icon Foreground Hierarchy
- `Prism.Brush.Text.Primary`: `#F8FAFC` (Pure porcelain white, high contrast, 100% legibility).
- `Prism.Brush.Text.Secondary`: `#94A3B8` (Cool silver-slate for subtitles and captions).
- `Prism.Brush.Text.Tertiary`: `#64748B` (Muted slate for technical metadata, units, and hints).
- `Prism.Brush.Text.Disabled`: `rgba(148, 163, 184, 0.35)` (Unselectable text).

---

## 3. Geometry, Elevation & Corner Radii

```
================================================================================
GEOMETRIC TOKEN STANDARDS
================================================================================
Card Corner Radius:        16px  (Outer card boundaries and containers)
Button / Input Radius:     10px  (Standard buttons, dropdowns, text fields)
Pill / Capsule Radius:     999px (Capsule sliders, status badges, chip buttons)
Flyout Window Radius:      20px  (Floating Quick Control and modal dialogs)

Standard Layout Grid:      8px base unit
Card Padding:              20px internal gutter
Card Separation Gutter:    16px
Section Margin:            24px
================================================================================
```

---

## 4. Reusable Component State Matrices

Every component adheres to a strict 6-state interactive matrix:

### 4.1 Buttons (Primary, Secondary, Ghost, Icon)
1. **Default:** Low-contrast glass background (`rgba(255,255,255,0.06)`), 1px subtle border (`#2A3548`), white text.
2. **Hover:** Surface lightens to `rgba(255,255,255,0.12)`, border brightens to `#3F5270`, cursor: `Hand`.
3. **Pressed:** Background darkens slightly (`rgba(0,0,0,0.15)`), scale transform drops to `0.98` for tactile click feel.
4. **Focused (Keyboard):** 2px high-contrast cyan outline (`#00C7FF`) with 2px offset.
5. **Disabled:** Opacity 40%, cursor: `NotAllowed`, mouse events suppressed.
6. **Active / Selected:** Accent-tinted glass background (`rgba(0, 199, 255, 0.15)`), solid accent border.

### 4.2 Capsule Sliders (The Signature PRISM Slider)
Inspired by macOS Control Center and Logic Pro channel strips:
- **Track Height:** 28px capsule (`CornerRadius = 14px`).
- **Inactive Track:** Deep charcoal-glass trough (`rgba(20, 28, 44, 0.70)`).
- **Active Filled Track:** Smooth gradient from `#0090FF` to `#00D2FF` (Display) or `#6D28D9` to `#8B5CF6` (Audio).
- **Thumb:** Tactile white pill thumb with a soft drop shadow (`BlurRadius = 8`, `Opacity = 0.35`), expanding subtly from 22px to 26px on mouse hover.
- **Embedded Labels:** Leading icon (sun/speaker) sits seamlessly on the left; trailing value (`72%` / `-3.0 dB`) displays in monospace typography on the right.

### 4.3 Segmented Controls & Navigation Items
- **Navigation Sidebar Items:**
  - Full width pill button with leading icon (20x20) and text.
  - Active state features a smooth horizontal indicator bar on the left edge (`#00C7FF`, width 3px) and an elevated translucent pill background (`rgba(0, 199, 255, 0.12)`).
- **Segmented Mode Switchers:**
  - Outer pill container enclosing 2 to 4 options.
  - Selected tab animates a sliding white/accent pill under the active text label.

### 4.4 Status Chips & Badges
- **Pill Shape:** Height 22px, padding `0 10px`, typography `11px Medium`.
- **Connected / Active:** `#10B981` text + `#10B981` dot + `rgba(16, 185, 129, 0.15)` pill background.
- **Not Installed / Inactive:** `#94A3B8` text + `rgba(148, 163, 184, 0.12)` background.
- **Partial Environment:** `#F59E0B` text + `rgba(245, 158, 11, 0.15)` background.

---

## 5. Motion, Physics & Interaction Timing

PRISM interfaces respect Microsoft Fluent motion guidelines and Apple spring curves:

| Interaction | Duration | Easing Function | Description |
|---|:---:|---|---|
| **Hover Transitions** | 120ms | `QuadraticEase (EaseOut)` | Rapid, perceptible brightening of cards and buttons |
| **Slider Drag Response** | 0ms / Live | Linear (Direct tracking) | Zero lag during mouse drag; throttled to 50ms I2C wire bus |
| **Flyout Open / Close** | 220ms | `CubicEase (EaseOut)` | Smooth slide-and-fade transition above system tray |
| **Environment Switch** | 300ms | `CubicEase (EaseInOut)` | Coordinated fade-through of display and audio settings |
| **Tab / View Navigation** | 200ms | `ExponentialEase (EaseOut)` | Subtle 8px horizontal slide + opacity crossfade |

---

## 6. Accessibility & Contrast Verification

- **Color Contrast:** All body text (`#F8FAFC`) against card backgrounds (`#121A2A`) achieves a contrast ratio of **12.4:1**, far exceeding WCAG AAA standard (7.0:1).
- **Secondary Text:** `#94A3B8` against card backgrounds achieves **6.1:1**, exceeding WCAG AA standard for small text and AAA for large text.
- **Keyboard Traversal:** Logical tab-indexing configured across all inputs with visible focus rings.
