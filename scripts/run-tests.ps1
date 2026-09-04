$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $projectRoot 'fork\AlchemyStars\AlchemyStars.slnx'
$acceptanceProject = Join-Path $projectRoot 'fork\AlchemyStars\tests\AlchemyStars.Acceptance\AlchemyStars.Acceptance.csproj'
$output = Join-Path $projectRoot 'fork\AlchemyStars\output'
$mayaPython = 'D:\Maya2025\bin\mayapy.exe'
$sprintCast = Join-Path $output 'sat_vm_ar_hawk_sprint_alchemy_stars.cast'
$mayaScene = Join-Path $output 'sat_vm_ar_hawk_sprint_alchemy_stars.ma'
$mayaReport = Join-Path $output 'sat_vm_ar_hawk_sprint_alchemy_stars.maya2025.json'

dotnet build $solution -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
dotnet run --project $acceptanceProject -c Release --no-build -- $output
if ($LASTEXITCODE -ne 0) { throw "Acceptance tests failed with exit code $LASTEXITCODE" }

if (Test-Path -LiteralPath $mayaPython) {
    & $mayaPython `
        (Join-Path $projectRoot 'maya\verify_cast_in_maya.py') `
        $sprintCast `
        $mayaScene `
        $mayaReport
    if ($LASTEXITCODE -ne 0) { throw "Maya 2025 acceptance failed with exit code $LASTEXITCODE" }
}
