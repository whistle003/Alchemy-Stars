$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $projectRoot 'release\Alchemy Stars'
$zipPath = Join-Path $projectRoot 'release\AlchemyStars-win-x64.zip'

dotnet publish (Join-Path $projectRoot 'src\AlchemyStars.App\AlchemyStars.App.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
Write-Output "Release: $publishDir"
Write-Output "Archive: $zipPath"

