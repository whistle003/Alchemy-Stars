# Alchemy Stars standard examples

`MP5Base.aprj` and `MP5Grip.aprj` are byte-identical copies of every project in the original `D:\_tiqu\Alchemist.UI.0.0.0.9\Example` folder. They are the project standards and remain at the root of this directory. Build and acceptance checks protect their hashes and structure from accidental changes.

| Path | Role |
| --- | --- |
| `MP5Base.aprj` | Original 26-animation, 5-part MP5 project; outputs `.cast` |
| `MP5Grip.aprj` | Original 26-animation, 6-part vertical-grip project; outputs `.seanim` |
| `manifest.json` | Shared path, structure, and standard-hash source for acceptance and release packaging |
| `Hawk\HawkSprint.aprj` | Improved Hawk sprint derived from the standard sprint-loop pattern |
| `Hawk\HawkIdle.aprj` | Improved one-animation Hawk idle project |
| `Hawk\HawkBatch.aprj` | Sprint and idle in one project, exported as two independent files |

The original example folder contains project files only. Its absolute MP5 asset paths point to the original author's machine, so relocate each model, animation, and pose with the browse buttons, choose an output folder, and use **Save project as** instead of overwriting the standards.

## Standard sprint pattern

Both MP5 projects contain 26 animation rows and 13 layers. The standard `sprint_loop` row uses Idle as the base, then applies `sprint_loop` followed by `sprint_offset_additive` as two Additive layers with unset offsets. `sprint_in` and `sprint_out` also use Idle bases, each with one transition layer. Layer order is significant.

`Hawk\HawkSprint.aprj` preserves that pattern exactly: Hawk Idle is the base, Sprint Loop is Additive layer 1, and Sprint Offset is Additive layer 2. It enables safe left-hand IK and disables right-hand IK because the supplied Hawk skeleton would otherwise create a cyclic target dependency. This is an asset-safety improvement, not a change to the source layering method.

Each exported Hawk CAST includes the shared view-hands and weapon model data and exactly one baked animation. `HawkSprint.aprj` is the sole configuration source for future Hawk sprint acceptance; tests do not duplicate its export settings.

## Using the projects

Open a project with the leftmost **Load project** button, drag it onto the window, or pass its path to `Alchemy Stars.exe`. Verify all paths and select an output directory before exporting. The application remembers the last animation, layer, model, project, and output folders separately across restarts. The Animation list, nested Animation Layers area, and Model Parts list all support right-click imports on items and empty space; `Shift+F10` works after focusing a list. Files dropped from Explorer over Animation Layers are imported into the hovered animation before any outer-row selection is considered. Export canonicalizes model order as ViewHands, Weapon, then Attachment and physically combines every part into one Model with remapped skin weights. A weapon listed before the hands no longer creates a second Maya skeleton, even when Maya's `Import Merge` option is off.

Choose `.cast`, `.fbx`, `.smd`, or `.seanim` under **Settings → Output**. The choice applies to the current task and becomes the default for new tasks. FBX uses the official `fbxmaya` plug-in from the locally installed Maya (Maya 2025 is preferred and auto-detected); SMD is written directly and contains the skeleton hierarchy plus per-frame local transforms.

The published `MayaPlugin` folder contains `cast.py` and `castplugin.py`. Add both to Maya's script/plugin path, load `castplugin.py`, and import the generated CAST through File → Import. A new scene does not require `Import Merge`; enable it only when intentionally merging into a skeleton that is already in the scene. FBX users can enable **Fill Timeline** on import. Verified CAST and FBX Hawk sprint results use 30 FPS, frames 0–66, 214 joints, 21 skinned meshes, one skeleton root, and one animated `j_gun`; SMD is checked for 214 nodes, 67 frames, and weapon world-space motion.

Run `scripts\run-tests.ps1` for build, manifest-driven standard-example integrity checks, real Hawk exports, Maya 2025 validation, release packaging, and UI smoke tests. The standalone release script also verifies the source and published MP5 hashes against the same manifest. See `README.zh-CN.md` for the complete guide.
