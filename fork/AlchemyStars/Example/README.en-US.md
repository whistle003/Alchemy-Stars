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

Open a project with the leftmost **Load project** button, drag it onto the window, or pass its path to `Alchemy Stars.exe`. Verify all paths and select an output directory before exporting. The Animation list, nested Animation Layers area, and Model Parts list all support right-click imports on items and empty space; `Shift+F10` works after focusing a list.

The published `MayaPlugin` folder contains `cast.py` and `castplugin.py`. Add both to Maya's script/plugin path, load `castplugin.py`, and import the generated CAST through File → Import. The verified Hawk sprint result uses 30 FPS, frames 0–66, 214 joints, 21 visible meshes, 1,284 curves, one skeleton root, and one `j_gun`.

Run `scripts\run-tests.ps1` for build, manifest-driven standard-example integrity checks, real Hawk exports, Maya 2025 validation, release packaging, and UI smoke tests. The standalone release script also verifies the source and published MP5 hashes against the same manifest. See `README.zh-CN.md` for the complete guide.
