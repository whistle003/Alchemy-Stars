# Avalonia + Native AOT migration

Status: test-only on `codex/avalonia-aot`; do not merge into `main` or publish as a stable release.

Version: `1.3.0-preview.1`<br>
Runtime: .NET 11 Preview 7<br>
UI framework: Avalonia 12.1.2

## Milestone 1 result

- `AlchemyStars.Engine` defines the WPF-free export interface, immutable request records, capabilities and structured validation errors.
- The engine adapter compiles the same proven conversion implementation used by WPF. It retains skeleton merging, IK, animation layers, selective bone baking, animation-only CAST, CAST, FBX, SMD and SEAnim output.
- `AlchemyStars.Avalonia` is a compiled-binding desktop shell with automatic system-language selection, Chinese/English switching, keyboard-visible focus and no WPF or MaterialDesign reference.
- The Avalonia project publishes as a self-contained `win-x64` Native AOT executable. The current WPF app remains the production baseline and is unchanged by this milestone.

The shared implementation is linked into the engine during the transition so there is one algorithm source, not a fork. Its historical `Alchemist.UI` internal namespace is intentionally hidden behind `IAnimationExportEngine`; move those internal files into the engine namespace after the WPF adapter switches to the new interface.

## Module seam

```text
WPF view models ── existing adapter ─┐
                                    ├── shared conversion implementation
Avalonia views ─ IAnimationExportEngine ─ typed request records
```

`IAnimationExportEngine` is the migration seam. UI state, file pickers, drag/drop, localization and dialogs stay in each UI adapter. Skeleton topology, animation composition and export rules stay in the engine implementation.

## Verification

Completed on 2026-09-05:

- Release solution build: 0 warnings, 0 errors (excluding the expected .NET preview-support notice).
- Existing and new acceptance suite: all 30 checks passed, including CAST, animation-only CAST, selective baking, SMD, Maya-backed FBX and the engine seam.
- Native AOT logic self-test and repeated real-window startup smoke: passed.
- Native AOT Hawk export: byte-identical to the managed engine output, SHA-256 `DB5940259349C1952E2049C27D55853B0A26A94C8AD30E8730B122C372287C81`.
- Native AOT UI preview: rendered from the published executable and visually checked for clipping, icon visibility, contrast and scroll safety.

From a shell using the SDK pinned by `global.json`:

```powershell
./scripts/verify-avalonia-aot.ps1
```

The verification publishes the native executable, runs its contract self-test, creates a real Avalonia/Win32 window off-screen, waits for layout and rendering initialization, and checks the process exits successfully.

The complete acceptance suite also invokes the engine interface with the standard Hawk sprint recipe (idle base plus sprint loop and sprint offset as additive layers). It validates the generated one-animation Maya CAST alongside every existing WPF regression.

During development, the AOT Hawk path can be exercised directly:

```powershell
./output/avalonia-aot-preview1/AlchemyStars.Avalonia.exe --hawk-smoke <hands.cast> <weapon.cast> <idle.cast> <sprint_loop.cast> <sprint_offset_additive.cast> <output-folder>
```

## Native AOT notes

- Build output can contain two Avalonia DirectComposition ILC diagnostics about non-blittable `bool` callbacks. They are emitted by Avalonia 12.1.2 even though the actual Win32 window startup regression passes repeatedly; keep the real startup test as the release gate.
- The current clean `win-x64` publish is approximately 38.4 MB across seven files: a 19.4 MB application executable plus Avalonia's Skia, HarfBuzz and ANGLE native rendering libraries and the Maya bridge scripts. Package PDB files are explicitly excluded.
- Application-level styles use deferred resource lookup. Static lookups from styles declared before the resource dictionary caused a deterministic AOT startup failure and are covered by the window startup regression.
- Runtime image and icon resources use explicit `avares://AlchemyStars.Avalonia/...` URIs so they remain resolvable after trimming.
- Avalonia build telemetry is disabled in verification scripts to make restricted and CI builds deterministic.

## Next milestone

Migrate one complete vertical workflow: model-part import, animation/layer import, request construction, export progress and centered result/error dialogs. Keep project serialization in an adapter until source-generated JSON metadata is added and verified under trimming.
