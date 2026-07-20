# ポータブル版 (self-contained single-file) をビルドして zip を作る
# 使い方: pwsh scripts/publish.ps1
param(
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot
$publishDir = Join-Path $repoRoot "artifacts\publish"
$zipPath = Join-Path $repoRoot "artifacts\SnapTack-v$Version-portable-$Runtime.zip"

dotnet publish (Join-Path $repoRoot "SnapTack\SnapTack.csproj") `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath
Write-Host "Portable zip: $zipPath"
