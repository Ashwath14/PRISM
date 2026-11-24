# Instructions for Antigravity — PRISM Equalizer Slider Reference

Use this package as a **visual reference** for the equalizer controls in PRISM.

## What to take from the reference
- Multiple slim vertical faders arranged in a single horizontal row.
- Clear separation between frequency bands.
- A distinct thumb/handle with a visible active position.
- A compact, framed equalizer section.
- A restrained cool-blue accent against a dark surface.
- A clean, technical equalizer appearance.

## What must be improved for PRISM
- Use the correct 10-band frequency layout already specified by the PRISM project
  (for example 31, 62, 125, 250, 500, 1k, 2k, 4k, 8k and 16k Hz), not the
  15 generic sliders in this sample unless there is a deliberate product decision.
- Display each frequency label clearly beneath its slider.
- Show the current gain value in dB for each band.
- Use a meaningful neutral/0 dB center position and a symmetric gain range,
  such as -12 dB to +12 dB, if supported by the DSP engine.
- Make keyboard, mouse and accessibility interaction work.
- Ensure the control is sharp at Windows 100%, 125%, 150% and 200% scaling.
- Ensure the equalizer fits the available window width and can scroll horizontally
  on smaller windows rather than clipping.
- Provide Reset EQ, preset selection, preamp and a clear active/bypass state.
- Add a response curve that reflects the actual filter coefficients/settings.
- Provide immediate but stable updates to the actual DSP pipeline.
- Do not merely update the slider UI while leaving audio unchanged.
- Preserve current working audio and device integration.

## Critical engineering requirements
1. Implement this natively in the existing WPF/.NET application.
2. Do not ship this HTML page as the production equalizer.
3. Do not assume CSS `orient` is a standard browser or WPF property.
4. The original supplied stylesheet used SCSS syntax. The included CSS is a
   browser-preview adaptation, not a direct WPF stylesheet.
5. Verify the actual audio output changes when sliders move.
6. If Equalizer APO or another DSP backend is unavailable, report that honestly.
7. Add tests for slider-to-filter mapping, bounds, reset, preset loading and
   persistence.
8. Keep the existing PRISM visual language; adapt the concept rather than
   copying the entire sample.

The HTML/CSS is inspiration for the visual treatment, not authority for the
DSP architecture or final control count.
