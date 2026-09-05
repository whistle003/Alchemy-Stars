[**English**](avalonia-aot-user-guide.md) | [简体中文](avalonia-aot-user-guide.zh-CN.md)

# Alchemy Stars Avalonia preview quick guide

This guide applies to `1.3.0-preview.8` on the test branch. WPF v1.1.9 remains the supported release until .NET 11 GA.

The 48 DIP activity rail switches between Animations, Model parts, Settings and About. The base-animation library sits on the left, a real CAST preview in the center, composition layers across the bottom, and collapsible properties on the right. Drag the dividers or focus them and use arrow keys to resize panels. Icon commands have localized tooltips and UI Automation names; the current page remains visible in the breadcrumb.

1. Open **Model parts** and add view hands first, then the weapon and attachments. The first item defaults to View hands; later items default to Weapon attached to `tag_weapon`.
2. Open **Animations** and add the base animation. Paths remain editable and accept pasted Windows **Copy as path** values.
3. Add optional left/right pose files and enable only the IK chains required by the animation.
4. Add layers in order. A file dropped or right-click imported inside **Animation layers** is always treated as a layer, not a new base animation.
5. Enter an output name and explicitly choose an output folder. New entries intentionally leave this field blank.
6. In **Settings**, choose CAST, FBX, SMD or SEAnim. Animation-only CAST and relevant-bones-only baking are optional; keep full baking for the broadest compatibility.
7. Choose **Export all** or press `Ctrl+E`. Progress and results stay centered inside the application.

The app remembers the last directory for each picker category and can follow the Windows display language or be pinned to Chinese/English. Project files remain compatible with the original `.aprj` structure.

For the canonical Hawk recipe, open `fork/AlchemyStars/Example/Hawk/HawkSprint.aprj`. It is the single source of truth used by managed and Native AOT export verification.

## Merged CAST preview

Choose **Build preview** in the composition workspace header to merge the selected animation into a unique temporary CAST with the existing export engine. It does not require or change the formal output folder; its cache is removed after loading an independent scene. **Open CAST preview** reads an existing merged file. A successful CAST export also previews the selected result.

- Drag the viewport or use arrow keys to orbit; use the wheel or +/− to zoom.
- Use playback, Space, the frame slider and previous/next frame buttons to inspect animation.
- Press F to frame the subject. Right-click **Fit subject** or press Shift+F to include all geometry, including distant spare parts.
- Toggle the bone button for a skeleton overlay. Animation-only CAST contains curves, not an embedded skeleton: load its matching model parts into the current project first. The viewport identifies this project-supplied skeleton; matching bone names alone cannot guarantee a matching bind pose.
- The current renderer shows clay geometry without textures/materials. Settings edits do not automatically rebuild a snapshot: choose **Build preview** again. Colored layer bars show composition order, not actual duration.
- Sampling and drawing run in the background, with at most one render in flight and a 960×640 resolution cap. Playback performance depends on scene complexity; validate final usage in Maya.

Keyboard commands:

| Command | Shortcut |
| --- | --- |
| Open project | `Ctrl+O` |
| Save project | `Ctrl+S` |
| Save project as | `Ctrl+Shift+S` |
| Export all | `Ctrl+E` |
| Close result/error dialog | `Esc` |

For migration architecture, package details and validation evidence, see [Avalonia + Native AOT migration](avalonia-aot-migration.md).
