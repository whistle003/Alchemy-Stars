param(
    [Parameter(Mandatory)][string]$SourceDirectory,
    [switch]$WithBlender,
    [string]$BlenderPath = 'C:\Program Files\Blender Foundation\Blender 4.3\blender.exe'
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$dotnetPath = Join-Path $repositoryRoot 'output/dotnet-sdk/dotnet.exe'
if (Test-Path -LiteralPath $dotnetPath) { $env:DOTNET_ROOT = Split-Path -Parent $dotnetPath }
else { $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source }
$project = Join-Path $repositoryRoot 'fork/AlchemyStars/src/AlchemyStars.Avalonia/AlchemyStars.Avalonia.csproj'
$assembly = Join-Path $repositoryRoot 'fork/AlchemyStars/src/AlchemyStars.Avalonia/bin/Release/net11.0/AlchemyStars.Avalonia.dll'
$outputDirectory = Join-Path $repositoryRoot 'output/dual-scarab'
& $dotnetPath build $project -c Release --nologo -v:q
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$smokeArguments = @('--dual-smoke', (Resolve-Path -LiteralPath $SourceDirectory).Path, $outputDirectory)
if ($WithBlender) {
    if (-not (Test-Path -LiteralPath $BlenderPath)) { throw 'Blender executable is missing.' }
    $env:ALCHEMY_STARS_FBX_BACKEND = 'blender'
    $env:ALCHEMY_STARS_BLENDER = $BlenderPath
    $smokeArguments += '--fbx'
}
& $dotnetPath $assembly @smokeArguments
if ($LASTEXITCODE -ne 0) { throw 'Dual engine verification failed.' }
if ($WithBlender) {
    foreach ($clip in Get-ChildItem -LiteralPath $outputDirectory -Filter '*_dual.cast') {
        $report = Join-Path $outputDirectory ($clip.BaseName + '.blender.json')
        $log = Join-Path $outputDirectory ($clip.BaseName + '.blender.log')
        & $BlenderPath --background --factory-startup --python-exit-code 1 --python (Join-Path $repositoryRoot 'blender/convert_cast.py') -- $clip.FullName --verify --report $report *> $log
        if ($LASTEXITCODE -ne 0) { throw "Blender verification failed. See $log" }
        Write-Output "PASS Blender: $($clip.BaseName)"
    }
    $fireStem = 'sat_vm_ww_scarab_akimbo_fire_dual'
    & $BlenderPath --background --factory-startup --python-exit-code 1 --python (Join-Path $repositoryRoot 'blender/verify_model_companion.py') -- (Join-Path $outputDirectory ($fireStem + '_model.cast')) (Join-Path $outputDirectory 'animation-only-fire.cast') (Join-Path $outputDirectory ($fireStem + '.cast')) *> (Join-Path $outputDirectory 'model-companion.blender.log')
    if ($LASTEXITCODE -ne 0) { throw 'Blender separate model/animation validation failed. See model-companion.blender.log.' }
    Write-Output 'PASS Blender: separate model then animation import'
}
