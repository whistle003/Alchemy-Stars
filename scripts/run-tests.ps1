$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $projectRoot 'fork\AlchemyStars\AlchemyStars.slnx'
$acceptanceProject = Join-Path $projectRoot 'fork\AlchemyStars\tests\AlchemyStars.Acceptance\AlchemyStars.Acceptance.csproj'
$output = Join-Path $projectRoot 'fork\AlchemyStars\output'
$exampleDirectory = Join-Path $projectRoot 'fork\AlchemyStars\Example'
$mayaPython = 'D:\Maya2025\bin\mayapy.exe'
$acceptanceReport = Join-Path $output 'acceptance-report.json'

dotnet build $solution -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
$acceptanceStartedUtc = [DateTime]::UtcNow
dotnet run --project $acceptanceProject -c Release --no-build -- $output $exampleDirectory
if ($LASTEXITCODE -ne 0) { throw "Acceptance tests failed with exit code $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $acceptanceReport)) { throw "Acceptance report was not created: $acceptanceReport" }

$report = Get-Content -LiteralPath $acceptanceReport -Raw | ConvertFrom-Json
$sprintCast = [System.IO.Path]::GetFullPath([string]$report.artifacts.sprintCast)
$resolvedOutput = [System.IO.Path]::GetFullPath($output)
if (-not $sprintCast.StartsWith($resolvedOutput + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Acceptance reported a sprint CAST outside the test output directory: $sprintCast"
}
if (-not (Test-Path -LiteralPath $sprintCast)) { throw "Acceptance sprint CAST was not created: $sprintCast" }
if ((Get-Item -LiteralPath $sprintCast).LastWriteTimeUtc -lt $acceptanceStartedUtc.AddSeconds(-2)) {
    throw "Acceptance sprint CAST was not refreshed during this run: $sprintCast"
}

$sprintStem = [System.IO.Path]::GetFileNameWithoutExtension($sprintCast)
$mayaScene = Join-Path $output ($sprintStem + '.ma')
$mayaReport = Join-Path $output ($sprintStem + '.maya2025.json')

if (Test-Path -LiteralPath $mayaPython) {
    & $mayaPython `
        (Join-Path $projectRoot 'maya\verify_cast_in_maya.py') `
        $sprintCast `
        $mayaScene `
        $mayaReport
    if ($LASTEXITCODE -ne 0) { throw "Maya 2025 acceptance failed with exit code $LASTEXITCODE" }
}

& (Join-Path $PSScriptRoot 'build-release.ps1')
if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE" }
& (Join-Path $PSScriptRoot 'test-ui.ps1')
if ($LASTEXITCODE -ne 0) { throw "UI smoke tests failed with exit code $LASTEXITCODE" }
