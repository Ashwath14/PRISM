# PRISM Liquid Glass Slider Reference

A browser-previewable version of the liquid-glass horizontal slider supplied by
the user.

## Files
- `index.html` — demo markup.
- `slider.css` — visual styling.
- `slider.js` — pointer, touch, keyboard and value interaction.
- `ANTIGRAVITY_INSTRUCTIONS.md` — guidance for native PRISM integration.

## Preview
Open `index.html` in a modern browser.

## Important
This is a UI interaction reference only. The demo emits a `sliderinput` custom
event with `{ value: 0..100 }`; it does not control Windows brightness or audio
volume. PRISM must connect its native WPF slider value to the existing,
verified Windows display/audio service.
