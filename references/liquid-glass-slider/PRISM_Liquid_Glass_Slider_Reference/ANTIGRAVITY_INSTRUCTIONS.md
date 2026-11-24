# Antigravity — PRISM Liquid Glass Slider Reference

Use this ZIP as a visual and interaction reference for PRISM's brightness and
volume slider controls.

## Preserve from the reference
- Long, slim horizontal track.
- Blue gradient progress fill.
- Wide, pill-shaped translucent glass thumb.
- Subtle specular highlights and restrained glass treatment.
- Smooth thumb movement and slight pressed-state compression.
- Click-on-track and drag interaction.
- Rounded, clean geometry.

## Native PRISM implementation requirements
1. Implement as native WPF controls/styles within the existing .NET application.
2. Do not embed the HTML/CSS/JS as the production control.
3. Preserve the existing display brightness and Windows Core Audio volume
   services and bind the slider to their real values.
4. The thumb position and progress fill must always reflect the actual current
   hardware/system value, including changes made outside PRISM.
5. Support mouse, touch where available, keyboard and accessibility.
6. Use correct minimum/maximum/range and value formatting for each control.
7. Debounce or safely throttle hardware updates if needed; do not overwhelm
   DDC/CI or the audio endpoint with excessive calls while dragging.
8. Handle failed hardware updates and restore the displayed value to the
   verified actual value. Never show false success.
9. Provide reset-to-default/original behavior in the relevant settings section.
10. Respect Windows high-DPI scaling and reduced-motion/accessibility needs.
11. Keep glass effects subtle and performant. If WPF cannot reproduce the SVG
    displacement effect reliably, create a clean native approximation rather
    than adding fragile rendering dependencies.
12. Do not make all sliders identical if their context requires different
    ranges, units or behavior.

## Integration and verification
- Brightness slider must change the selected monitor's supported brightness
  through the existing DDC/CI service, or clearly identify software dimming
  when that is the selected mode.
- Volume slider must change the selected Windows audio endpoint's actual
  volume through Core Audio.
- Verify external changes are reflected in PRISM.
- Test dragging, clicking the track, keyboard arrows, min/max, device changes,
  monitor changes, persistence and error handling.
- Add tests for value mapping and service binding.

This reference is a visual concept, not permission to replace working hardware
integration or claim the slider is functional before real verification.
