# Alchemy Stars

Alchemy Stars is a maintained, Maya-ready fork of [Scobalula/Alchemist](https://github.com/Scobalula/Alchemist). It retains the RedFox animation pipeline while adding deterministic view-hands/weapon skeleton merging, one-animation Maya CAST packaging, safe IK, layer offsets, DQS skinning, validation, published examples, and acceptance tests. The `codex/avalonia-aot` test branch contains the complete Avalonia Native AOT workflow; WPF v1.1.9 remains the stable baseline on `main` until .NET 11 GA.

Open `AlchemyStars.slnx` with Visual Studio or build this test branch with the .NET 11 Preview SDK pinned by the repository `global.json`:

```powershell
dotnet build .\AlchemyStars.slnx -c Release
dotnet run --project .\tests\AlchemyStars.Acceptance\AlchemyStars.Acceptance.csproj -c Release
```

The root of `Example` contains the original `MP5Base.aprj` and `MP5Grip.aprj` projects byte-for-byte as this fork's standard examples. A shared `manifest.json` drives integrity checks in acceptance and release packaging. Improved sprint, idle, and combined Hawk projects live under `Example\Hawk`; each generated Hawk CAST contains all model data and exactly one baked animation. The complete example set and bilingual instructions are copied into every published build. See the repository-root `README.md` for Maya 2025 validation results and release instructions.

This fork remains licensed under GPL-3.0. The pinned RedFox and Cast.NET dependencies retain their respective upstream licenses.
