[**English**](avalonia-aot-user-guide.md) | [简体中文](avalonia-aot-user-guide.zh-CN.md)

# Alchemy Stars Avalonia preview quick guide

This guide applies to `1.3.0-preview.6` on the test branch. WPF v1.1.9 remains the supported release until .NET 11 GA.

The compact left activity rail switches between Animations, Model parts, Settings and About. The Animation workspace places the base-animation library on the left, composition summary and layer tracks in the center, and the scrollable inspector on the right. Tooltips, persistent labels and localized UI Automation names keep navigation discoverable to keyboard and screen-reader users.

1. Open **Model parts** and add view hands first, then the weapon and attachments. The first item defaults to View hands; later items default to Weapon attached to `tag_weapon`.
2. Open **Animations** and add the base animation. Paths remain editable and accept pasted Windows **Copy as path** values.
3. Add optional left/right pose files and enable only the IK chains required by the animation.
4. Add layers in order. A file dropped or right-click imported inside **Animation layers** is always treated as a layer, not a new base animation.
5. Enter an output name and explicitly choose an output folder. New entries intentionally leave this field blank.
6. In **Settings**, choose CAST, FBX, SMD or SEAnim. Animation-only CAST and relevant-bones-only baking are optional; keep full baking for the broadest compatibility.
7. Choose **Export all** or press `Ctrl+E`. Progress and results stay centered inside the application.

The app remembers the last directory for each picker category and can follow the Windows display language or be pinned to Chinese/English. Project files remain compatible with the original `.aprj` structure.

For the canonical Hawk recipe, open `fork/AlchemyStars/Example/Hawk/HawkSprint.aprj`. It is the single source of truth used by managed and Native AOT export verification.

Keyboard commands:

| Command | Shortcut |
| --- | --- |
| Open project | `Ctrl+O` |
| Save project | `Ctrl+S` |
| Save project as | `Ctrl+Shift+S` |
| Export all | `Ctrl+E` |
| Close result/error dialog | `Esc` |

For migration architecture, package details and validation evidence, see [Avalonia + Native AOT migration](avalonia-aot-migration.md).
