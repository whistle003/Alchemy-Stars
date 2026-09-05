# Language-consistent context menus — 1.3.0-preview.8

This page inherits the screenshot-led editor rules from `avalonia-aot-1.3.0-preview.7.md`.

- Visible context-menu labels follow the active application language; never combine Chinese and English in one command.
- Keyboard shortcuts remain in parentheses because they are input notation, not a second UI language.
- Animation, animation-layer, model-part and viewport framing menus use the same `UiText` source as the rest of the workspace and refresh when the global language changes.
- Preserve native menu semantics, keyboard access, focus behavior and descriptive screen-reader names.
