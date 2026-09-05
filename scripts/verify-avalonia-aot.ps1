param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repositoryRoot 'fork\AlchemyStars\src\AlchemyStars.Avalonia\AlchemyStars.Avalonia.csproj'
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'output'))
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $outputRoot 'avalonia-aot-preview1'))
if (-not $publishDirectory.StartsWith($outputRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean a publish path outside the repository output directory: $publishDirectory"
}
if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
[System.IO.Directory]::CreateDirectory($publishDirectory) | Out-Null

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'

dotnet publish $project `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -o $publishDirectory `
    --nologo `
    -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) {
    throw "Native AOT publish failed with exit code $LASTEXITCODE."
}

$executable = Join-Path $publishDirectory 'AlchemyStars.Avalonia.exe'
$selfTest = Start-Process `
    -FilePath $executable `
    -ArgumentList '--self-test' `
    -WorkingDirectory $publishDirectory `
    -WindowStyle Hidden `
    -Wait `
    -PassThru
if ($selfTest.ExitCode -ne 0) {
    throw "Native AOT contract self-test failed with exit code $($selfTest.ExitCode)."
}

& (Join-Path $PSScriptRoot 'test-avalonia-aot-startup.ps1') -PublishDirectory $publishDirectory
Get-Item -LiteralPath $executable | Select-Object FullName, Length, LastWriteTime
