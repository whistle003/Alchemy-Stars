$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $projectRoot 'release\Alchemy Stars'
$zipPath = Join-Path $projectRoot 'release\AlchemyStars-win-x64.zip'
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'release'))
$resolvedPublishDir = [System.IO.Path]::GetFullPath($publishDir)
$uiProject = Join-Path $projectRoot 'fork\AlchemyStars\src\Alchemist.UI\Alchemist.UI.csproj'
$exampleSourceDir = Join-Path $projectRoot 'fork\AlchemyStars\Example'
$exampleManifestPath = Join-Path $exampleSourceDir 'manifest.json'

function Resolve-ContainedPath([string]$root, [string]$relativePath) {
    $resolvedRoot = [System.IO.Path]::GetFullPath($root)
    $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $resolvedRoot ($relativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)))
    if (-not $resolvedPath.StartsWith($resolvedRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Example path escapes its root: $relativePath"
    }
    return $resolvedPath
}

function Test-ExactExampleSet([string]$root, [string[]]$expectedPaths) {
    $expected = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $expectedPaths) {
        [void]$expected.Add(($path -replace '\\', '/'))
    }

    $actual = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File -Force) {
        $relativePath = [System.IO.Path]::GetRelativePath($root, $file.FullName) -replace '\\', '/'
        if (-not $relativePath.StartsWith('Output/', [System.StringComparison]::OrdinalIgnoreCase) -and
            -not $relativePath.StartsWith('Hawk/Output/', [System.StringComparison]::OrdinalIgnoreCase)) {
            [void]$actual.Add($relativePath)
        }
    }

    $unexpected = @($actual | Where-Object { -not $expected.Contains($_) })
    if ($unexpected.Count -gt 0) {
        throw "Example directory contains undeclared files: $($unexpected -join ', ')"
    }
    $missing = @($expected | Where-Object { -not $actual.Contains($_) })
    if ($missing.Count -gt 0) {
        throw "Example manifest declares missing files: $($missing -join ', ')"
    }
}

if (-not (Test-Path -LiteralPath $exampleManifestPath)) {
    throw "Example manifest is missing: $exampleManifestPath"
}
$exampleManifest = Get-Content -LiteralPath $exampleManifestPath -Raw | ConvertFrom-Json
if ($exampleManifest.SchemaVersion -ne 1) {
    throw "Unsupported example manifest schema: $($exampleManifest.SchemaVersion)"
}

if (-not $resolvedPublishDir.StartsWith($releaseRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clear a publish directory outside the release folder: $resolvedPublishDir"
}

if (Test-Path -LiteralPath $resolvedPublishDir) {
    Remove-Item -LiteralPath $resolvedPublishDir -Recurse -Force
}

dotnet restore $uiProject -r win-x64
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}

dotnet publish $uiProject `
    -c Release `
    -r win-x64 `
    --no-restore `
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

$requiredExamples = @('manifest.json') +
    @($exampleManifest.StandardExamples.Path) +
    @($exampleManifest.ImprovedExamples.Path) +
    @($exampleManifest.Documentation)
Test-ExactExampleSet $exampleSourceDir $requiredExamples
foreach ($fileName in $requiredExamples) {
    $sourcePath = Resolve-ContainedPath $exampleSourceDir $fileName
    $publishedPath = Resolve-ContainedPath (Join-Path $resolvedPublishDir 'Example') $fileName
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Source example file is missing: $sourcePath"
    }
    if (-not (Test-Path -LiteralPath $publishedPath)) {
        throw "Published example file is missing: $publishedPath"
    }
}
Test-ExactExampleSet (Join-Path $resolvedPublishDir 'Example') $requiredExamples

foreach ($standardExample in $exampleManifest.StandardExamples) {
    $sourcePath = Resolve-ContainedPath $exampleSourceDir $standardExample.Path
    $publishedPath = Resolve-ContainedPath (Join-Path $resolvedPublishDir 'Example') $standardExample.Path
    $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash
    $publishedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $publishedPath).Hash
    if ($sourceHash -ne $standardExample.Sha256) {
        throw "Standard source example changed: $($standardExample.Path)"
    }
    if ($publishedHash -ne $standardExample.Sha256) {
        throw "Published standard example changed: $($standardExample.Path)"
    }
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $resolvedPublishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
Write-Output "Release: $resolvedPublishDir"
Write-Output "Archive: $zipPath"
