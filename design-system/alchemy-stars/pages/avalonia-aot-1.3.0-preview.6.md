# Reference-driven creative workbench — 1.3.0-preview.6

## Intent

This revision adapts the supplied Beutl editor screenshot to the real Alchemy Stars workflow. It does not add non-functional playback, viewport or 3D editing affordances.

## Shell

- 64 DIP activity rail with persistent text labels.
- 56 DIP project breadcrumb and command bar.
- Full-height, edge-to-edge editor panels with 1 DIP separators.
- 30 DIP status bar for project state and exact preview version.

## Animation page

- Left: base-animation asset library and import/remove actions.
- Center upper: composition summary for the selected animation, model count, layer count and sprint-batch action.
- Center lower: functional animation-layer tracks, priority drop target and compact layer editor.
- Right: scrollable animation, hand-pose, IK and export inspector.

## Model page

- Left: ordered model-part library and reorder actions.
- Center: current assembly and attachment relationship.
- Right: model file, part type and parent-bone inspector.

## Visual tokens

- Workbench `#18191B`; rail `#121214`; editor surface `#1D1D1F`; composition canvas `#03060C`.
- Divider `#36373B`; essential control boundary `#62656D`.
- Text `#F5F5F5`; muted text `#B8BAC0`; accent/focus `#4CC2FF` / `#60CDFF`.
- Icons remain 20 DIP rounded-stroke vectors; interactive targets remain at least 44 DIP.

## Accessibility

- Activity destinations retain visible labels instead of relying on icons alone.
- Icon commands expose localized tooltips and UI Automation names.
- File drop and context-menu flows retain keyboard-accessible buttons and editable path fields.
- Long inspectors scroll independently; dialogs remain centered inside the owner and receive initial focus.
