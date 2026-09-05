# Beutl-inspired Avalonia workbench — 1.3.0-preview.5

## Scope

This override defines the test-branch editor shell. WPF v1.1.9 remains the production baseline until the .NET 11 GA gate is approved.

## Reference synthesis

- Beutl is the primary shell reference: flat near-black workbench, compact activity/navigation chrome, dock-like asset and property panels, subtle splitters and an accent underline for the active tool panel.
- AtomBox is the secondary component reference: restrained list rows, explicit labels, compact form spacing and 6 DIP utility-surface radii.
- No Beutl, Dock.Avalonia or AtomUI package is introduced. The project reimplements only the applicable visual hierarchy with existing Avalonia primitives to protect Native AOT size and trimming behavior.

## Tokens

- Workbench `#222327`; sidebar/panel `#0E0E0F`; control surface `#17181A`.
- Passive divider `#222327`; essential control boundary `#62656D` (at least 3:1 against the control surface).
- Primary text `#F5F5F5`; secondary text `#B8BAC0`; focus/accent `#60CDFF` / `#4CC2FF`.
- Structural icons are 20 DIP, 1.8 DIP rounded-stroke vectors. Controls keep a minimum 44 DIP target.

## Layout

- 72 DIP activity rail, 56 DIP project command header and 30 DIP status strip.
- Animation and model pages use a resource list plus property inspector separated by a 6 DIP splitter gutter.
- Panel headers are 32 DIP with a 2 DIP accent underline on the active property panel.
- The 900 × 600 minimum viewport must remain free of horizontal scrolling; long property content scrolls inside its panel.

## Accessibility gates

- Every icon-only action has a localized tooltip and UI Automation name.
- Visible focus is independent of selection; primary input boundaries meet non-text contrast requirements.
- Drop, context-menu and reorder gestures always have file-picker or button alternatives.
- Result/error overlays remain centered in the owner, receive keyboard focus and close with Escape.
- No functional animation is required, so reduced-motion behavior is equivalent.
