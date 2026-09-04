# Alchemy Stars

Alchemy Stars is a maintained, Maya-ready fork of [Scobalula/Alchemist](https://github.com/Scobalula/Alchemist). It retains the original WPF workflow and RedFox animation pipeline while adding deterministic view-hands/weapon skeleton merging, one-animation Maya CAST packaging, safe IK, layer offsets, validation, published examples, and acceptance tests.

Open `AlchemyStars.slnx` with Visual Studio 2022 or build it with the .NET 9 SDK:

```powershell
dotnet build .\AlchemyStars.slnx -c Release
dotnet run --project .\tests\AlchemyStars.Acceptance\AlchemyStars.Acceptance.csproj -c Release
```

The root of `Example` contains the original `MP5Base.aprj` and `MP5Grip.aprj` projects byte-for-byte as this fork's standard examples. A shared `manifest.json` drives integrity checks in acceptance and release packaging. Improved sprint, idle, and combined Hawk projects live under `Example\Hawk`; each generated Hawk CAST contains all model data and exactly one baked animation. The complete example set and bilingual instructions are copied into every published build. See the repository-root `README.md` for Maya 2025 validation results and release instructions.

This fork remains licensed under GPL-3.0. The pinned RedFox and Cast.NET dependencies retain their respective upstream licenses.
