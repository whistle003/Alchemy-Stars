# Changelog

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
