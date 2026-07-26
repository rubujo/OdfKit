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
    $nugetConfig = Join-Path $resolvedPackages ".net48-smoke.NuGet.Config"
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="odfkit-local" value="$resolvedPackages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="odfkit-local">
      <package pattern="OdfKit" />
      <package pattern="OdfKit.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfig -Encoding utf8
    try {
        dotnet restore $project @properties --configfile $nugetConfig
    }
    finally {
        Remove-Item -LiteralPath $nugetConfig -Force -ErrorAction SilentlyContinue
    }
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
