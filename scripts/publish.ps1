# ポータブル版 (single-file) をビルドして zip を作る
# ランタイム同梱版 (self-contained) と軽量版 (framework-dependent) の 2 種類を出力する
# 使い方: pwsh scripts/publish.ps1
param(
    [string]$Runtime = "win-x64",
    [string]$Version = "1.3.0"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot
$csproj = Join-Path $repoRoot "SnapTack\SnapTack.csproj"

# 同梱版の出力先は installer/SnapTack.iss が参照しているため artifacts\publish から変えないこと
$targets = @(
    @{ SelfContained = $true;  PublishDir = "artifacts\publish";    ZipSuffix = "" }
    @{ SelfContained = $false; PublishDir = "artifacts\publish-fd"; ZipSuffix = "-fd" }
)

foreach ($t in $targets) {
    $publishDir = Join-Path $repoRoot $t.PublishDir
    $zipPath = Join-Path $repoRoot "artifacts\SnapTack-v$Version-portable-$Runtime$($t.ZipSuffix).zip"

    # 前回の出力が残っていると不要なファイルを zip に含めてしまうため消しておく
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

    dotnet publish $csproj `
        -c Release `
        -r $Runtime `
        --self-contained $($t.SelfContained.ToString().ToLower()) `
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
}
