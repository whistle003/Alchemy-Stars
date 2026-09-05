# Avalonia + Native AOT migration

Status: complete test implementation on `codex/avalonia-aot`; do not merge into `main` or publish as a stable release before .NET 11 GA.

Version: `1.3.0-preview.6`
Runtime: .NET 11 Preview 7
UI framework: Avalonia 12.1.2
Production baseline: WPF v1.1.9 on `main`

## Result

The test branch now contains the complete Alchemy Stars desktop workflow rather than a migration shell:

- create, open, save and save-as legacy-compatible `.aprj` projects;
- add, edit, reorder and remove view-hand, weapon and attachment model parts;
- add base animations, optional hand poses and ordered Normal/Additive/Gesture/GesturePose layers;
- configure left/right IK, frame rate, output names and an explicitly selected output folder;
- export full-scene or animation-only CAST, FBX, SMD and SEAnim through the existing proven conversion implementation;
- choose full or relevant-bones-only baking, old Call of Duty compatibility, prefixes and suffixes;
- remember per-purpose file-browser locations and follow system, Chinese or English UI language;
- use file browsers, editable/pasteable paths, direct path drops, list drop zones and blank-area context menus;
- keep progress, success and error feedback centered inside the owning window.

New animation output folders stay blank. The engine also rejects an output that resolves to any model, animation, pose or layer input, so a same-named export cannot overwrite source data.

## Architecture

```text
Avalonia views and adapters
        │
        ├── WorkspaceProjectStore + source-generated JSON metadata
        ├── ApplicationPreferencesStore
        └── IAnimationExportEngine
                    │
                    └── shared Alchemist/RedFox conversion implementation

WPF production view models ─────────┘
```

The UI layer owns presentation, localization, file pickers and drag/drop. The engine owns skeleton identity, model composition, animation sampling, IK, validation and output formats. Legacy JSON uses source-generated `System.Text.Json` metadata, including the old `$id` / `$values` shape, so Native AOT does not depend on runtime reflection.

## Accessibility and visual behavior

- The minimum supported window is 900 × 600 DIP; each page scrolls vertically without horizontal clipping.
- The main shell adapts the supplied Beutl editor reference into a compact activity rail, project breadcrumb, asset library, composition canvas, functional animation-layer tracks, property inspector and 30 DIP full-width status strip. AtomBox contributes the restrained form/list spacing model.
- All primary controls use persistent labels, at least 44 DIP command targets and visible focus rings.
- Icon-only toolbar commands use a consistent 20 DIP rounded-stroke family, localized tooltips and UI Automation names.
- `Ctrl+O`, `Ctrl+S`, `Ctrl+Shift+S`, `Ctrl+E` and `Esc` cover project/export/dialog actions.
- Every drag or reorder workflow also has a file-browser, context-menu or button alternative.
- Windows UI Automation checks the published native executable for required names, keyboard focus, 44 × 44 key targets and initial dialog focus.
- The About icon is displayed inside a padded 112 × 112 region and is not clipped.
- Motion is intentionally absent, so reduced-motion users do not lose information or controls.

## Verification

Completed on 2026-09-05:

- Release solution build: 0 warnings and 0 errors, excluding the expected preview-support notice.
- Acceptance suite: all 32 checks passed, including CAST, animation-only CAST, selective baking, SMD, Maya-backed FBX, the workspace store and source-overwrite prevention.
- Native AOT self-test, real Win32 startup, Windows UI Automation and four-page off-screen rendering: passed.
- Native AOT standard `HawkSprint.aprj` export: passed.
- Native AOT and managed Hawk CAST outputs are byte-identical: SHA-256 `DB5940259349C1952E2049C27D55853B0A26A94C8AD30E8730B122C372287C81`.
- The clean `win-x64` publish is 7 files / 38.17 MiB. The application executable is 21,006,336 bytes; PDB files are rejected.

Run the complete native verification with the pinned preview SDK:

```powershell
./scripts/verify-avalonia-aot.ps1
```

The standard-project path can also be exercised directly:

```powershell
./output/avalonia-aot-preview6/AlchemyStars.Avalonia.exe --project-smoke ./fork/AlchemyStars/Example/Hawk/HawkSprint.aprj <output-folder>
```

## Native AOT notes

- Avalonia 12.1.2 currently emits two DirectComposition ILC diagnostics about non-blittable `bool` callbacks. Real Win32 startup, rendering and UI Automation pass; those executable checks remain release gates.
- Runtime assets use explicit `avares://` URIs and compiled bindings so trimming preserves the required UI metadata.
- The published app is self-contained and does not require a separate .NET runtime. Maya is not bundled and remains an external requirement for FBX conversion.
- Build telemetry is disabled in verification to keep restricted and CI runs deterministic.

## Remaining production gate

No implementation phase remains on the test branch. Production migration waits only for the final .NET 11 SDK, an updated dependency audit, a repeat of all 32 engine/Maya checks, Native AOT startup/UI Automation checks on the GA runtime, and explicit approval to merge. Until then, WPF v1.1.9 remains the supported release.
