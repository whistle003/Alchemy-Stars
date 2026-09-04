# Alchemy Stars

Alchemy Stars is a maintained, Maya-ready fork of [Scobalula/Alchemist](https://github.com/Scobalula/Alchemist). It retains the original WPF workflow and RedFox animation pipeline while adding deterministic view-hands/weapon skeleton merging, one-animation Maya CAST packaging, safe IK, layer offsets, validation, presets, and acceptance tests.

Open `AlchemyStars.slnx` with Visual Studio 2022 or build it with the .NET 9 SDK:

```powershell
dotnet build .\AlchemyStars.slnx -c Release
dotnet run --project .\tests\AlchemyStars.Acceptance\AlchemyStars.Acceptance.csproj -c Release
```

The bundled sprint and idle projects are in `presets`. Each output contains all model data and exactly one baked animation. See the repository-root `README.md` for usage, Maya 2025 validation results, and release instructions.

This fork remains licensed under GPL-3.0. The pinned RedFox and Cast.NET dependencies retain their respective upstream licenses.
