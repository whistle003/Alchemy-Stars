# Changelog

## 1.1.6 — 2026-09-05

- Added direct Explorer file drops to every editable source-path field; handled drops stop at the field instead of bubbling into the surrounding animation, layer, or model import list.
- Output-folder fields accept either a folder or a file (using its containing folder), and successful field drops update the corresponding remembered directory.
- Added acceptance coverage for CAST filtering, folder resolution, and quoted clipboard paths.

## 1.1.5 — 2026-09-04

- Restored direct editing and clipboard paste support for animation, pose, animation-layer, model-part, and output-folder paths while retaining every file-browser button.
- Trimmed surrounding whitespace and matching quotes on focus loss, so paths copied with Windows **Copy as path** are accepted without manual cleanup.
- Added UI automation coverage for editable, keyboard-focusable, screen-reader-named path fields and quoted-path normalization.

## 1.1.4 — 2026-09-04

- Replaced the narrow output-format drop-down with four direct, keyboard-focusable radio options so the popup can no longer detach, shrink, or cover help text.
- Reflowed output settings into a responsive two-column card layout and increased the adaptive dialog height; scrolling now appears only when the window or localized content genuinely needs it.
- Extended UI automation to verify all four format choices, focusability, minimum target height, dialog containment, and the non-overlapping IK fields.

## 1.1.3 — 2026-09-04

- Redesigned the Output and IK settings dialog with grouped cards, explicit field labels, consistent spacing, and scroll-safe layout.
- Removed overlapping floating IK labels and added unique accessibility names plus predictable keyboard order for all eight IK fields.
- Expanded UI smoke coverage for label/input separation, minimum input height, bilingual content, and output-option toggles.

## 1.1.2 — 2026-09-04

- Added an optional relevant-bones-only bake for CAST, FBX, and SEAnim pipelines.
- Retains all base-animation, pose, layer, IK-chain, and indirectly changed bones; unknown solvers safely fall back to a full bake.
- Keeps compatibility mode as the default and documents the matching-bind-pose requirement for animation-only imports.
- Added full-versus-selective curve comparison and separate Maya 2025 validation; Hawk sprint retains 121 of 214 bones.
- Added global and project-level persistence plus bilingual, accessible settings UI.

## 1.1.1 — 2026-09-04

- Added optional animation-only CAST output containing one merged, baked animation without model geometry, materials, or skinning.
- Preserved full-scene CAST as the default and stored the option globally and in `.aprj` projects.
- Added animation-only CAST structure and weapon-curve acceptance coverage.

## 1.1.0 — 2026-09-04

Compared with upstream Alchemist, this release keeps its animation-layer and RedFox pipeline while adding a verified single-skeleton Maya package, direct SMD, Maya-backed FBX, safer import UX, persistent folders, and a complete bilingual release workflow.

- Added real FBX export through the locally installed Autodesk Maya FBX plug-in.
- Added native SMD animation export with complete skeleton hierarchy and per-frame transforms.
- Added `.cast`, `.fbx`, `.smd`, and `.seanim` default-format selection.
- Added automatic system-language detection plus Follow System, Simplified Chinese, and English choices.
- Added persistent recent folders for animations, layers, models, projects, and output.
- Redesigned the settings dialog to prevent clipping and protected the language/About controls from toolbar overlap.
- Updated About content and Hawk validation for CAST, FBX, SMD, skinning, and weapon motion.
- Kept the Windows x64 package framework-dependent; no .NET or Maya runtime is bundled.

## 1.0.3

- Redesigned toolbar icons to match their actions.
- Prioritized external file drops over the hovered animation-layer region.

## 1.0.2

- Fixed merged Maya animation output, including weapon animation and single-skeleton import.

## 1.0.0

- Initial Alchemy Stars release based on Scobalula/Alchemist.
