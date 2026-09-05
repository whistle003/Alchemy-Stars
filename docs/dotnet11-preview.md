# .NET 11 Preview compatibility line

- Branch: `codex/dotnet11-preview`
- Application version: `1.2.0-preview.1`
- Pinned SDK: `11.0.100-preview.7.26381.103`
Status: test-only; do not merge into `main` or publish as a stable release.

## Scope

Alchemy Stars UI, core, scripting, and acceptance projects target `net11.0` (`net11.0-windows7.0` for WPF projects). The pinned RedFox submodule remains unchanged at `net9.0`; its assemblies run on the .NET 11 process and keep the upstream boundary clean.

`global.json` opts into the exact Preview 7 SDK. Developers must install that SDK, or invoke it from an isolated SDK directory. The compact application remains framework-dependent and requires the matching .NET 11 Preview Desktop Runtime.

## Compatibility findings

1. .NET 11 rejects `EnableCompressionInSingleFile=true` for framework-dependent single-file publishing (`NETSDK1176`). The preview branch disables bundle-internal compression, retains `PublishSingleFile=true`, and relies on the outer ZIP for download compression. It does not bundle the Desktop Runtime.
2. The release script now creates a validated archive subdirectory when it does not yet exist, allowing preview artifacts to stay separate from stable assets.
3. Package restore and compilation succeed with the existing `log4net`, `MaterialDesignThemes`, Roslyn, Microsoft.TSS, RedFox, and Cast.NET dependencies.

## Verification completed on 2026-09-05

- Release build: 0 warnings, 0 errors (apart from the expected `NETSDK1057` preview-support message).
- Actual WPF Chinese/English layout suite: 0 failures.
- Alchemy Stars acceptance suite: all 29 checks passed.
- Maya 2025: full and relevant-bones-only CAST imports passed with one skeleton, 215 joints, 21 visible meshes, weapon/right-hand animation, 30 FPS, and the expected 0–66 frame range.
- 1911, P27, and Hawk: full/selective/animation-only CAST, FBX, SMD, independent source-rig, skinning, weapon ancestry, and per-frame world-matrix comparisons passed.
- Packaged EXE UI smoke: version, 21 toolbar controls, settings, About, localization, context imports, and pasted path inputs passed.

Local test artifact (not committed): `release/preview/AlchemyStars-1.2.0-preview.1-win-x64.zip`.

## GA migration gate

When Microsoft publishes .NET 11 GA:

1. Replace the preview SDK pin with the final `11.0.100` SDK and remove preview opt-in.
2. Change the application version from `1.2.0-preview.1` to the approved stable version and update runtime documentation/UI expectations.
3. Recheck single-file publishing rules and package size; keep the runtime external unless explicitly changed.
4. Restore from clean caches, build, run all acceptance/Maya/UI checks above, and compare output matrices with the stable baseline.
5. Merge into `main` and publish only after explicit approval.
