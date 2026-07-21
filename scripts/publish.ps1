# ポータブル版 (single-file) をビルドして zip を作る
# ランタイム同梱版 (self-contained) と軽量版 (framework-dependent) の 2 種類を出力する
# 使い方: pwsh scripts/publish.ps1
param(
    [string]$Runtime = "win-x64",
    # 省略時は Directory.Build.props の <Version> を使う (CI からは明示的に渡す)
    [string]$Version
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot
$csproj = Join-Path $repoRoot "SnapTack\SnapTack.csproj"

# -Version 省略時は props の値を読み取る。zip 名にバージョンを含めるため、
# ビルドへ渡さない場合でも実際の値を知る必要がある
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (dotnet msbuild $csproj -getProperty:Version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Version)) {
        throw "Directory.Build.props からバージョンを取得できませんでした"
    }
    $versionArgs = @()
    Write-Host "Version (props): $Version"
} else {
    $versionArgs = @("-p:Version=$Version")
    Write-Host "Version (explicit): $Version"
}

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
        @versionArgs `
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
