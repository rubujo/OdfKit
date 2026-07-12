#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$PackageDirectory = "artifacts/nuget",
    [string]$PackageVersion = "0.0.1",
    [switch]$UseProjectReferences
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "tests/OdfKit.NetFramework48Smoke/OdfKit.NetFramework48Smoke.csproj"

if (-not $IsWindows) {
    throw "net48 smoke 必須在 Windows CLR 上執行。"
}

$properties = @(
    "-p:OdfKitPackageVersion=$PackageVersion"
)
if ($UseProjectReferences) {
    $properties += "-p:UseLocalPackages=false"
    dotnet restore $project @properties
}
else {
    $packagePath = if ([System.IO.Path]::IsPathRooted($PackageDirectory)) {
        $PackageDirectory
    }
    else {
        Join-Path $repoRoot $PackageDirectory
    }
    $resolvedPackages = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($packagePath)
    if (-not (Test-Path -LiteralPath $resolvedPackages -PathType Container)) {
        throw "找不到 net48 smoke 的本機套件目錄：$resolvedPackages"
    }

    $properties += "-p:UseLocalPackages=true"
    dotnet restore $project @properties --source $resolvedPackages --source "https://api.nuget.org/v3/index.json"
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build $project -c $Configuration --no-restore @properties
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$executable = Join-Path $repoRoot "tests/OdfKit.NetFramework48Smoke/bin/$Configuration/net48/OdfKit.NetFramework48Smoke.exe"
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "找不到 net48 smoke 執行檔：$executable"
}

& $executable
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "net48 CLR consumer smoke 通過。"
