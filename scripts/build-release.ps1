$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $projectRoot 'release\Alchemy Stars'
$zipPath = Join-Path $projectRoot 'release\AlchemyStars-win-x64.zip'
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'release'))
$resolvedPublishDir = [System.IO.Path]::GetFullPath($publishDir)

if (-not $resolvedPublishDir.StartsWith($releaseRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clear a publish directory outside the release folder: $resolvedPublishDir"
}

if (Test-Path -LiteralPath $resolvedPublishDir) {
    Remove-Item -LiteralPath $resolvedPublishDir -Recurse -Force
}

dotnet publish (Join-Path $projectRoot 'fork\AlchemyStars\src\Alchemist.UI\Alchemist.UI.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $resolvedPublishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $resolvedPublishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
Write-Output "Release: $resolvedPublishDir"
Write-Output "Archive: $zipPath"
