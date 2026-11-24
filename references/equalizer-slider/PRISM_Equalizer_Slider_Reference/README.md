# PRISM Equalizer Slider UI Reference

This package contains the supplied equalizer slider concept in a small,
browser-previewable reference project.

## Files
- `index.html` — sample equalizer layout with 15 vertical sliders.
- `equalizer-reference.css` — browser-ready CSS styling.
- `ANTIGRAVITY_INSTRUCTIONS.md` — implementation guidance for PRISM.

## Important
This is a visual interaction reference only. It does not implement audio
processing, filters, frequency response, or DSP. PRISM must connect each
slider to its existing, verified audio processing pipeline.

The original CSS supplied with the reference used SCSS syntax (variables,
nesting and mixins) and a non-standard `orient` attribute. The included CSS
has been adapted to regular CSS for browser preview. Antigravity should
translate the visual concept into the existing C# WPF application rather
than embedding this HTML/CSS as the production equalizer.
