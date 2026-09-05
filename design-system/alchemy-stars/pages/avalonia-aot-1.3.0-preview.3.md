# Avalonia AOT complete workflow — 1.3.0-preview.3

## Scope

This page overrides the master design notes for the complete Avalonia test interface. It does not authorize replacing the WPF production build before .NET 11 GA.

## Layout

- Native window chrome; 72 DIP product header, 194 DIP navigation rail, scrollable work area and 36 DIP status strip.
- Minimum viewport 900 × 600 DIP; no horizontal scrolling or controls hidden behind the header/status regions.
- Animation and model pages use a list/editor split. Settings and About use a single vertical scroll surface.
- Dialogs and busy state are overlays centered over the owning window, with a scrollable body and fixed action.

## Visual language

- Background `#101014`, sidebar `#17171F`, surfaces `#20202B` / `#292937`.
- Primary text `#F7F7FA`, muted text `#B8B8C7`, border `#3B3B4C`, focus `#60A5FA` and action orange `#F97316`.
- Toolbar/navigation icons are vector-only, 20 DIP, transparent fill, 1.8 DIP rounded stroke. No emoji or font glyph is used for structural toolbar commands.
- The product icon keeps 10 DIP inner padding inside a 112 × 112 About container.

## Interaction

- All primary buttons are at least 44 DIP high; icon-only toolbar buttons are 44 × 44 DIP.
- Icon-only commands have a localized tooltip and `AutomationProperties.Name`; decorative vector paths do not receive pointer input.
- Form controls have persistent visible labels plus specific bilingual automation names, including unique left/right IK names.
- Focus uses a 2 DIP blue outline distinct from selection and brand color.
- Drag/drop is optional: system file pickers, pasteable text fields, context menus and move buttons expose the same operations.
- The animation-layer drop zone handles the event and prevents it from bubbling into the base-animation zone.
- Modal close receives initial keyboard focus and Escape dismisses the overlay.
- No functional animation is used; reduced-motion behavior is therefore equivalent.

## Acceptance

- Render Animations, Model parts, Settings and About/dialog at 900 × 600 in English and Chinese combinations.
- Run Windows UI Automation against the Native AOT executable and verify required names, keyboard focus and minimum key-target bounds.
- Reject icon regressions that render line paths as filled blocks or mix stroke widths/styles in the primary command layer.
