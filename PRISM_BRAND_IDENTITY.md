# PRISM Brand Identity & Vector Design System
**Document Version:** 2.0.0  
**Status:** Approved Architectural Direction  
**Author:** Lead Identity & Vector Graphics Designer  
**Date:** November 20, 2026  

---

## 1. Brand Concept: The Convergence of Spectrums

The name **PRISM** embodies physical transformation and multi-spectral precision. In optical physics, a triangular prism receives raw, unstructured white light and refracts it into its constituent spectral frequencies. 

In PRISM, this metaphor governs the entire product:
- **Optical Spectrum (Display):** Uncalibrated monitor luminance and raw color temperatures are refracted into precise DDC/CI hardware gains, gamma balance, and Sub-Zero software dimming.
- **Acoustic Spectrum (Audio):** Unfiltered desktop sound is refracted into 10 calibrated parametric frequency bands, harmonic enhancement, and spatial clarity.

The PRISM logo merges these two physical phenomena into a singular, memorable geometric glyph: the **Refractive P**.

---

## 2. The Refractive "P" Geometric Anatomy

```
                   Facet C: Dispersion Arc
                  ┌───────────────────────┐
                  │   Cyan -> Violet      │
                  │   /───────\           │
                  │  /         \          │
    Facet A:     │ │     P     │         │
    Optical Stem  │ │   Bowl    │         │
    (Incident     │  \         /          │
     Beam)        │   \───────/           │
                  └─────────┬─────────────┘
                            │
               Facet B: Refraction Wedge
               (Acoustic Wave & Phase Angle)
```

### Geometric Construction Rules:
1. **The Squircle Container:** A super-ellipse with formula $|x/a|^{4.2} + |y/b|^{4.2} \le 1$, providing continuous G2 curvature (unlike simple rounded rectangles which exhibit sudden curvature breaks at corner tangent points).
2. **The Optical Stem (Facet A):** A vertical pillar with a crisp left optical datum representing the incident white light beam and the acoustic amplitude axis. It features a translucent gradient fading from pure white specular highlight down to cool deep cobalt (`#1E3A8A`).
3. **The Dispersion Arc (Facet C):** An elegant 180° toroidal sweep forming the upper bowl of the 'P'. It exhibits continuous chromatic dispersion along its outer perimeter: Cyan (`#00F2FE`), Electric Periwinkle (`#4FACFE`), Royal Violet (`#7F00FF`), and Neon Rose (`#FF007F`).
4. **The Waveform Fold (Facet B):** An angled diagonal facet that folds back into the stem at a 42° angle (the critical angle of internal reflection in crown glass), visually bridging the optical facet with an acoustic sinusoidal wave.

---

## 3. Vector SVG Specifications

### 3.1 Primary Master App Icon (512x512)
Below is the production vector SVG code for the full-resolution PRISM application icon:

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="100%" height="100%">
  <defs>
    <!-- Background Canvas Gradient -->
    <radialGradient id="prismBgGlow" cx="50%" cy="30%" r="70%">
      <stop offset="0%" stop-color="#1E2A4A" stop-opacity="0.8"/>
      <stop offset="50%" stop-color="#0E1626" stop-opacity="0.95"/>
      <stop offset="100%" stop-color="#060911" stop-opacity="1.0"/>
    </radialGradient>

    <!-- Squircle Glass Border Gradient -->
    <linearGradient id="squircleBorder" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#FFFFFF" stop-opacity="0.4"/>
      <stop offset="40%" stop-color="#4CC2FF" stop-opacity="0.2"/>
      <stop offset="100%" stop-color="#7F00FF" stop-opacity="0.1"/>
    </linearGradient>

    <!-- Stem Incident Light Gradient -->
    <linearGradient id="stemGrad" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#FFFFFF" stop-opacity="0.95"/>
      <stop offset="35%" stop-color="#A5F3FC" stop-opacity="0.85"/>
      <stop offset="70%" stop-color="#0284C7" stop-opacity="0.75"/>
      <stop offset="100%" stop-color="#0F172A" stop-opacity="0.6"/>
    </linearGradient>

    <!-- Chromatic Dispersion Bowl Gradient -->
    <linearGradient id="dispersionArc" x1="0%" y1="0%" x2="100%" y2="80%">
      <stop offset="0%" stop-color="#38BDF8"/>
      <stop offset="30%" stop-color="#60A5FA"/>
      <stop offset="60%" stop-color="#818CF8"/>
      <stop offset="85%" stop-color="#C084FC"/>
      <stop offset="100%" stop-color="#F43F5E"/>
    </linearGradient>

    <!-- Internal Reflection Shadow -->
    <linearGradient id="internalReflection" x1="100%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" stop-color="#FFFFFF" stop-opacity="0.3"/>
      <stop offset="50%" stop-color="#000000" stop-opacity="0.0"/>
      <stop offset="100%" stop-color="#000000" stop-opacity="0.4"/>
    </linearGradient>

    <!-- Drop Shadow Filter -->
    <filter id="prismGlow" x="-20%" y="-20%" width="140%" height="140%">
      <feGaussianBlur stdDeviation="16" result="blur"/>
      <feComposite in="SourceGraphic" in2="blur" operator="over"/>
    </filter>
  </defs>

  <!-- Squircle Base Layer -->
  <rect x="32" y="32" width="448" height="448" rx="112" ry="112" fill="url(#prismBgGlow)"/>
  <rect x="32" y="32" width="448" height="448" rx="112" ry="112" fill="none" stroke="url(#squircleBorder)" stroke-width="2.5"/>

  <!-- Ambient Internal Caustic Flare -->
  <circle cx="320" cy="220" r="140" fill="#38BDF8" opacity="0.12" filter="url(#prismGlow)"/>
  <circle cx="200" cy="320" r="120" fill="#818CF8" opacity="0.1" filter="url(#prismGlow)"/>

  <!-- Optical Refractive 'P' Glyph -->
  <g id="refractivePGlyph" transform="translate(128, 112)">
    <!-- Facet A: Vertical Stem Column -->
    <path d="M 32 32 
             L 96 32 
             C 96 32, 96 220, 96 256 
             L 32 256 
             Z" 
          fill="url(#stemGrad)" />

    <!-- Facet B: Prismatic Internal Wedge -->
    <path d="M 96 32 
             L 168 32 
             C 220 32, 248 72, 248 124 
             C 248 176, 212 208, 160 208 
             L 96 208 
             Z" 
          fill="url(#dispersionArc)" 
          opacity="0.9" />

    <!-- Facet C: Internal Cutout & Wave Reflection -->
    <path d="M 96 84 
             L 156 84 
             C 188 84, 196 104, 196 124 
             C 196 144, 184 156, 156 156 
             L 96 156 
             Z" 
          fill="#0B132B" 
          opacity="0.85"/>

    <!-- Specular Highlight Overlay -->
    <path d="M 32 32 
             L 168 32 
             C 220 32, 248 72, 248 124 
             L 242 124 
             C 242 76, 216 38, 168 38 
             L 38 38 
             L 38 256 
             L 32 256 
             Z" 
          fill="url(#internalReflection)"/>
  </g>
</svg>
```

### 3.2 Compact System-Tray & Taskbar Icon (16x16 / 32x32 Grid Aligned)
At small resolutions (16x16 or 32x32), micro-gradients turn into blurry noise. The system-tray icon uses crisp, pixel-snapped vector geometry with high-contrast outlines:

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">
  <!-- 32x32 Pixel Snapped High-Contrast Tray Glyph -->
  <path d="M 6 4 
           L 18 4 
           C 23 4, 27 7.5, 27 13 
           C 27 18.5, 23 22, 18 22 
           L 12 22 
           L 12 28 
           L 6 28 
           Z 
           M 12 9 
           L 12 17 
           L 17.5 17 
           C 20 17, 21.5 15.5, 21.5 13 
           C 21.5 10.5, 20 9, 17.5 9 
           Z" 
        fill="#00D2FF"/>
  <!-- Specular Refraction Core -->
  <path d="M 12 9 L 17.5 9 C 19 9, 20 9.8, 20.8 11 L 12 15.5 Z" fill="#FFFFFF" opacity="0.8"/>
</svg>
```

### 3.3 Monochrome & Accessibility Glyphs
- **Dark Mode Monochrome:** Solid `#F8FAFC` glyph on `#0B0F17` base.
- **Light Mode Monochrome:** Solid `#0F172A` glyph on `#FFFFFF` base.
- **Windows High Contrast Mode:** Automatically maps to `SystemColor.Highlight` and `SystemColor.Window`.

---

## 4. Typography & Wordmark System

```
  ┌────────────────────────────────────────────────────────┐
  │                                                        │
  │    [ ❖ ]   P  R  I  S  M                               │
  │            S T U D I O                                 │
  │                                                        │
  └────────────────────────────────────────────────────────┘
```

### 4.1 Wordmark Construction
- **Primary Typeface:** `Segoe UI Variable Display` (Windows 11 native) with fallback to `Segoe UI` or `Inter`.
- **Character Spacing:** Letter-spacing set to `+0.25em` (`CharacterSpacing = 250` in WPF).
- **Font Weight:** `SemiBold` (600) for "PRISM", `Regular` (400) for "STUDIO".
- **Optical Height:** Capital letters match the visual baseline and cap-height of the logo glyph.

### 4.2 Type Scale Tokens

| Token | Family | Weight | Size (pt/px) | Line Height | Tracking | Usage |
|---|---|:---:|:---:|:---:|:---:|---|
| `Type.Display.Hero` | Segoe UI Variable Display | Bold (700) | 28px | 34px | -0.01em | Home hero header |
| `Type.Title.Section` | Segoe UI Variable Display | SemiBold (600) | 20px | 26px | 0.0em | Screen titles, studio headings |
| `Type.Title.Card` | Segoe UI Variable Text | SemiBold (600) | 15px | 20px | +0.01em | Environment card titles, modal headers |
| `Type.Body.Primary` | Segoe UI Variable Text | Regular (400) | 13px | 18px | 0.0em | Control labels, descriptive text |
| `Type.Body.Secondary`| Segoe UI Variable Text | Regular (400) | 12px | 16px | 0.0em | Subtitles, helper text, explanations |
| `Type.Caption.Meta` | Segoe UI Variable Text | Medium (500) | 11px | 14px | +0.02em | Badges, status chips, VCP register codes |
| `Type.Mono.Numeric` | Segoe UI Variable Text | SemiBold (600) | 12px | 16px | 0.0em | Tabular numbers: percentages, dB values, Hz |

---

## 5. Brand Color Tokens

```
================================================================================
PRISM PALETTE: Optical Clarity & Deep Resonance
================================================================================
Canvas Base:          #090D16  (Deep Midnight Space)
Elevated Card:        #121826  (65% opacity + 1px glass border)
Glass Border:         #232D42  (rgba(255, 255, 255, 0.08))

Primary Accent (Cyan):#00C7FF  (Light / Luminance / Display focus)
Audio Accent (Violet):#8B5CF6  (Acoustic / DSP / Frequency focus)
Dispersion Coral:     #F43F5E  (Clipping / Alert / Peak focus)
Success Emerald:      #10B981  (Connected / Hardware verified)
Warning Amber:        #F59E0B  (Partial environment / Uncalibrated)
Inactive Slate:       #64748B  (Not installed / Disabled)
================================================================================
```
