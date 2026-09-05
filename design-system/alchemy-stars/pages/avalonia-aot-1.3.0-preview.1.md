# Avalonia AOT migration shell — 1.3.0-preview.1

## Purpose

This page defines the test-only Avalonia shell. It does not replace the WPF production interface in this milestone.

## Layout

- Native title bar; no custom window chrome during the migration.
- 72 DIP product header, 224 DIP navigation rail, scrollable work area, 36 DIP status strip.
- Main content remains usable at 900 × 600 DIP; cards use clear text wrapping rather than clipping.
- All command targets are at least 44 DIP high and show a visible keyboard-focus ring.

## Visual direction

- Background `#101014`, sidebar `#17171F`, surfaces `#20202B` / `#292937`.
- Primary text `#F7F7FA`, secondary text `#B8B8C7`, border `#3B3B4C`.
- Alchemy orange `#F97316` is reserved for identity and primary actions.
- Focus blue `#60A5FA` remains distinct from both selection and brand color.
- Icons are simple Avalonia vector paths; structural icons never use emoji or text glyphs.

## Interaction and accessibility

- System UI culture selects Chinese for `zh-*`; all other cultures begin in English.
- A persistent language command switches the complete shell and exposes an accessible name.
- Navigation and verification controls remain keyboard reachable.
- The production-WPF notice is always visible in the navigation rail so preview status cannot be mistaken for release readiness.
- Motion is intentionally absent from the first AOT shell; no capability depends on animation.
