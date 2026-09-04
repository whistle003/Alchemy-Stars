[**English**](README.md) | [简体中文](README.zh-CN.md)

# Alchemy Stars

Alchemy Stars (炼金之星) is a production-focused improvement of [Scobalula/Alchemist](https://github.com/Scobalula/Alchemist) for Windows, first-person CAST weapon assets, and Autodesk Maya 2025. It retains Alchemist's WPF batch interface and RedFox animation pipeline while completing a reliable model-and-animation export workflow.

The maintained source lives in `fork/AlchemyStars` and pins the matching RedFox revision to keep builds reproducible. The earlier standalone rewrite remains preserved on the `independent-rewrite-v1` branch and is no longer the active implementation.

## Improvements over upstream Alchemist

| Area | Upstream Alchemist | Alchemy Stars 1.1.0 |
| --- | --- | --- |
| Maya model and animation | Import behavior can leave duplicate skeletons or lose weapon motion | Physically merges parts, same-name bones, skin weights, and emits one baked animation per file |
| Output formats | Primarily CAST / SEAnim | Adds real FBX and native SMD while retaining CAST / SEAnim |
| FBX workflow | Not provided | Detects the local Maya installation and uses the official `fbxmaya` plug-in without bundling a large conversion runtime |
| Asset import | Primarily the original UI controls | System file dialogs, drag-and-drop, blank-area context menus, and `Shift+F10`; drops over animation layers are routed there first |
| Localization | Original UI capability | Follows the system language by default, can be pinned to Simplified Chinese or English, and refreshes open About content |
| Continuity | Project files retain absolute paths | Also remembers recent animation, layer, model, project, and output folders by category |
| UI and distribution | Original settings layout and icons | Purpose-specific icons, unclipped tabbed settings, protected language/About controls, and a compact framework-dependent package |
| Regression validation | Upstream examples | Preserves the original MP5 examples byte-for-byte and validates CAST, FBX, SMD, IK, skinning, and weapon motion with real Hawk assets |

Alchemy Stars keeps the upstream animation-layer concepts intact. Attribution and licenses for Alchemist, RedFox, and the CAST components are included in every release package.

## Highlights

- Merges view hands and weapons by same-name bones so the shared `j_gun` is not renamed or split into a second skeleton.
- Normalizes model order as ViewHands → Weapon → Attachment and physically combines all parts before export.
- Keeps every mesh, material, and remapped skin weight in each CAST while including exactly one selected baked animation.
- Preserves Normal, Additive, Gesture, GesturePose, positive offsets, and negative offsets through the RedFox sampling pipeline.
- Rejects cyclic IK targets and fixes two-bone IK and animation-clone state loss.
- Restores layer and part ownership after project loading so reorder, remove, and drag operations remain usable.
- Supports `.cast`, `.fbx`, `.smd`, and `.seanim`; SMD contains the full skeleton and per-frame local transforms, while FBX preserves models, skinning, and animation through Maya.
- Uses system file dialogs for animation, pose-layer, model, project, and output paths and remembers the most recent folder for each category.
- Offers Follow System, Simplified Chinese, and English interface modes.
- Keeps all completion, warning, and error dialogs centered over the application; long diagnostics are scrollable and copyable.
- Uses a redesigned alchemy-flask-and-star application icon and function-specific toolbar icons.

## Download and use

Download the latest ZIP from [GitHub Releases](https://github.com/ez4cywa/Alchemy-Stars/releases), extract it, and run:

`Alchemy Stars.exe`

The app starts with an empty batch. Use the toolbar buttons, the folder buttons beside path fields, or the context menus to select assets with the system file browser.

Open **Settings → Output** to choose `.cast`, `.fbx`, `.smd`, or `.seanim` as the default output format. The selection applies immediately and becomes the default for new tasks; a saved project keeps its own format. FBX requires a locally installed Maya, with Maya 2025 preferred. SMD does not require Maya.

On the Animation page, right-click anywhere in the main list—including blank space—and choose **Import animations…**. The animation-layer area has its own **Import animation layers…** menu. When an external file is dropped over an animation-layer area, that hovered animation takes priority over the outer selection. The Model Parts list offers the same blank-area right-click workflow. All lists also support `Shift+F10`.

Each batch entry produces a separate file containing one baked animation. Project files store absolute paths; after moving to another computer, reselect the assets and output directory, then use **Save Project As**.

## Standard examples

The release directory and ZIP include the complete `Example` folder. `MP5Base.aprj` and `MP5Grip.aprj` are migrated directly from upstream Alchemist and remain byte-identical. `manifest.json` records the required files, structure, and checksums. Improved Hawk sprint, idle, and batch projects live under `Example/Hawk`.

After extracting a release, open `Example/README.en-US.md`; the Chinese guide is `Example/README.zh-CN.md`. Online copies are available in the [English example guide](https://github.com/ez4cywa/Alchemy-Stars/blob/main/fork/AlchemyStars/Example/README.en-US.md) and [Chinese example guide](https://github.com/ez4cywa/Alchemy-Stars/blob/main/fork/AlchemyStars/Example/README.zh-CN.md).

Examples are not loaded automatically. Open or drop an `.aprj` file, or pass it as a command-line argument. Future Hawk sprint checks use `Example/Hawk/HawkSprint.aprj` as the single source of truth: idle base animation, two ordered additive layers, IK, model parts, format, and output naming all come from that project.

## Maya 2025

The release `MayaPlugin` directory contains the CAST importer. Copy `cast.py` and `castplugin.py` into a Maya script or plug-in path, load `castplugin.py` in Plug-in Manager, and import the generated CAST through **File → Import**.

For FBX, Alchemy Stars locates the local Maya installation and invokes `fbxmaya`. Set `ALCHEMY_STARS_MAYAPY` to select a particular `mayapy.exe`. Conversion is isolated in an ASCII-only temporary workspace, so Chinese Windows user names, output folders, and output file names remain supported despite the Maya 2025 FBX plug-in's path limitation. Enable **Fill Timeline** when manually importing FBX into Maya.

The Hawk sprint release artifact has been tested headlessly in Maya 2025 with:

- 214 joints, one skeleton root, and one `j_gun`;
- 21 imported and visible skinned meshes;
- 1,284 translation/rotation curves with every transform channel keyed on every frame;
- 30 FPS and a 0–66 playback range;
- maximum left-hand IK positional error of about 0.050;
- preserved right-hand and weapon motion, while unsafe cyclic right-hand IK is skipped.

The generated report is `fork/AlchemyStars/output/sat_vm_ar_hawk_sprint_alchemy_stars.maya2025.json`.

## Build and validation

Development requires the .NET 9 SDK. The compact Windows x64 release is framework-dependent and requires the [.NET 9 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/9.0); it does not bundle .NET or Maya.

```powershell
.\scripts\run-tests.ps1
.\scripts\build-release.ps1
```

`run-tests.ps1` builds the improved upstream project, verifies that the standard MP5 examples were not modified, generates actual Hawk CAST/SMD/FBX outputs, and reimports CAST and FBX into Maya 2025 when available. It checks the skeleton, meshes, skinning, frame range, IK, and weapon animation, including weapon-first model ordering and Chinese TEMP/output paths and names. The UI smoke suite checks centered dialogs, the protected language/About layout, settings clipping, four format choices, three language modes, accessible toolbar controls, and context imports.

## Source and licenses

- Improved Alchemist source: `fork/AlchemyStars`, GPL-3.0; see `fork/AlchemyStars/LICENSE`.
- Pinned RedFox revision: `fork/RedFox` Git submodule.
- Maya CAST plug-in: `third_party/cast`, MIT; see `THIRD_PARTY_NOTICES.md`.

Upstream baseline: Alchemist `d86da66536ed3bf304a5cb7142d360fb934f73fb`; RedFox `7031da79614d1d979b1f17cae9d4bda2c699fd53`.
