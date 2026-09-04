$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $projectRoot 'fork\AlchemyStars\AlchemyStars.slnx'
$acceptanceProject = Join-Path $projectRoot 'fork\AlchemyStars\tests\AlchemyStars.Acceptance\AlchemyStars.Acceptance.csproj'
$output = Join-Path $projectRoot 'fork\AlchemyStars\output'
$exampleDirectory = Join-Path $projectRoot 'fork\AlchemyStars\Example'
$mayaPython = 'D:\Maya2025\bin\mayapy.exe'
$acceptanceReport = Join-Path $output 'acceptance-report.json'

function Resolve-AcceptanceArtifact([string]$path, [string]$label, [datetime]$startedUtc) {
    $resolvedPath = [System.IO.Path]::GetFullPath($path)
    $resolvedOutput = [System.IO.Path]::GetFullPath($output)
    if (-not $resolvedPath.StartsWith($resolvedOutput + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Acceptance reported $label outside the test output directory: $resolvedPath"
    }
    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        throw "Acceptance $label was not created: $resolvedPath"
    }
    if ((Get-Item -LiteralPath $resolvedPath).LastWriteTimeUtc -lt $startedUtc.AddSeconds(-2)) {
        throw "Acceptance $label was not refreshed during this run: $resolvedPath"
    }
    return $resolvedPath
}

function Test-CastInMaya([string]$castPath, [string]$label) {
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($castPath)
    $artifactDirectory = Split-Path -Parent $castPath
    $mayaScene = Join-Path $artifactDirectory ($stem + '.ma')
    $mayaReport = Join-Path $artifactDirectory ($stem + '.maya2025.json')
    & $mayaPython `
        (Join-Path $projectRoot 'maya\verify_cast_in_maya.py') `
        $castPath `
        $mayaScene `
        $mayaReport
    if ($LASTEXITCODE -ne 0) { throw "Maya 2025 $label acceptance failed with exit code $LASTEXITCODE" }
}

dotnet build $solution -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
$acceptanceStartedUtc = [DateTime]::UtcNow
dotnet run --project $acceptanceProject -c Release --no-build -- $output $exampleDirectory
if ($LASTEXITCODE -ne 0) { throw "Acceptance tests failed with exit code $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $acceptanceReport)) { throw "Acceptance report was not created: $acceptanceReport" }

$report = Get-Content -LiteralPath $acceptanceReport -Raw | ConvertFrom-Json
$sprintCast = Resolve-AcceptanceArtifact ([string]$report.artifacts.sprintCast) 'sprint CAST' $acceptanceStartedUtc
$weaponFirstSprintCast = Resolve-AcceptanceArtifact ([string]$report.artifacts.weaponFirstSprintCast) 'weapon-first sprint CAST' $acceptanceStartedUtc

if (Test-Path -LiteralPath $mayaPython) {
    Test-CastInMaya $sprintCast 'standard-order sprint'
    Test-CastInMaya $weaponFirstSprintCast 'weapon-first sprint'
}

& (Join-Path $PSScriptRoot 'build-release.ps1')
if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE" }
& (Join-Path $PSScriptRoot 'test-ui.ps1')
if ($LASTEXITCODE -ne 0) { throw "UI smoke tests failed with exit code $LASTEXITCODE" }
