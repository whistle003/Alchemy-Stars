$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

dotnet build (Join-Path $projectRoot 'AlchemyStars.slnx') -c Release
dotnet run --project (Join-Path $projectRoot 'tests\AlchemyStars.Tests\AlchemyStars.Tests.csproj') -c Release --no-build

