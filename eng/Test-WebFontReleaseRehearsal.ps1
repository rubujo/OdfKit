#Requires -Version 7.0
<#
.SYNOPSIS
    以同批發布產物演練本機 NuGet feed、SBOM 消費與漏洞稽核。
.PARAMETER PackageDirectory
    已驗證的 NuGet 套件目錄。
.PARAMETER SbomPath
    WebFont SPDX 2.3 SBOM 路徑。
.PARAMETER OutputDirectory
    演練證據輸出目錄；必須位於方案 artifacts 目錄內。
#>
[CmdletBinding()]
param(
    [string]$PackageDirectory = "artifacts/nuget",
    [string]$SbomPath = "artifacts/webfont-sbom/manifest.spdx.json",
    [string]$OutputDirectory = "artifacts/webfont-release-rehearsal"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts"))
$packageRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $PackageDirectory))
$resolvedSbomPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $SbomPath))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$artifactsPrefix = $artifactsRoot.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $outputRoot.StartsWith($artifactsPrefix, $comparison)) {
    throw "OutputDirectory 必須位於方案 artifacts 目錄內。"
}
if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
    throw "缺少待發布套件目錄：$packageRoot"
}
if (-not (Test-Path -LiteralPath $resolvedSbomPath -PathType Leaf)) {
    throw "缺少待發布 WebFont SBOM：$resolvedSbomPath"
}

$packageVersion = & (Join-Path $PSScriptRoot "Get-PackageVersion.ps1")

& (Join-Path $PSScriptRoot "Test-NuGetPack.ps1") `
    -OutputDirectory $PackageDirectory `
    -SkipPack `
    -SkipConsumerSmoke `
    -VerifyHashManifest
& (Join-Path $PSScriptRoot "Test-WebFontSupplyChain.ps1") `
    -PackageDirectory $PackageDirectory `
    -OutputPath $SbomPath `
    -VerifyExisting `
    -SkipRestoreClosureValidation

$sbom = Get-Content -LiteralPath $resolvedSbomPath -Raw | ConvertFrom-Json -Depth 30
$revision = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{40}$') {
    throw "無法取得發布演練來源提交。"
}
if ([string]$sbom.documentNamespace -notlike "*/$packageVersion/$revision") {
    throw "WebFont SBOM 未繫結共同版本 $packageVersion 與目前提交：$revision"
}

$externalPackageIds = @(
    $sbom.packages |
        Where-Object { [string]$_.SPDXID -like "SPDXRef-Dependency-*" } |
        ForEach-Object { [string]$_.name } |
        Sort-Object -Unique)
if ($externalPackageIds.Count -eq 0) {
    throw "WebFont SBOM 未列出可供 source mapping 與漏洞稽核的外部相依。"
}

$internalPackageIds = @(
    $sbom.packages |
        Where-Object { [string]$_.SPDXID -like "SPDXRef-Package-*" -and $_.name -ne "OdfKit WebFonts release set" } |
        ForEach-Object { [string]$_.name } |
        Sort-Object -Unique)
$expectedConsumerPackages = @(
    "OdfKit.Extensions.Html.WebFonts",
    "OdfKit.WebFonts.Abstractions",
    "OdfKit.WebFonts.Build",
    "OdfKit.WebFonts.Data.SqlServer",
    "OdfKit.WebFonts.Encoding.Legacy",
    "OdfKit.WebFonts.Hosting.AspNetCore",
    "OdfKit.WebFonts.Hosting.SystemWeb",
    "OdfKit.WebFonts.Sidecar",
    "OdfKit.WebFonts.OpenType",
    "OdfKit.WebFonts.Profiles",
    "OdfKit.WebFonts.Windows",
    "OdfKit.WebFonts.Worker"
)
if (($internalPackageIds -join "`n") -ne (($expectedConsumerPackages | Sort-Object) -join "`n")) {
    throw "WebFont SBOM 的發布套件集合與演練契約不一致。"
}

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}
$workRoot = Join-Path $outputRoot "work"
$feedRoot = Join-Path $workRoot "feed"
$consumerRoot = Join-Path $workRoot "consumer"
$toolRoot = Join-Path $workRoot "tool"
$nugetCache = Join-Path $workRoot "nuget-cache"
New-Item -ItemType Directory -Path $feedRoot, $consumerRoot, $toolRoot, $nugetCache -Force | Out-Null

$packageFiles = @(Get-ChildItem -LiteralPath $packageRoot -Filter "*.nupkg" -File | Sort-Object Name)
if ($packageFiles.Count -eq 0) {
    throw "發布演練找不到 nupkg。"
}
foreach ($packageFile in $packageFiles) {
    & dotnet nuget push $packageFile.FullName `
        --source $feedRoot `
        --no-symbols `
        --force-english-output
    if ($LASTEXITCODE -ne 0) {
        throw "無法將套件發布至隔離本機 feed：$($packageFile.Name)"
    }
}

$escapedFeedRoot = [Security.SecurityElement]::Escape($feedRoot)
$externalMappings = $externalPackageIds |
    ForEach-Object { '      <package pattern="' + [Security.SecurityElement]::Escape($_) + '" />' }
$nugetConfigPath = Join-Path $workRoot "NuGet.release.config"
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="odfkit-release" value="$escapedFeedRoot" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <auditSources>
    <clear />
    <add key="nuget.org-audit" value="https://data.nuget.org/v3/index.json" />
  </auditSources>
  <packageSourceMapping>
    <packageSource key="odfkit-release">
      <package pattern="OdfKit" />
      <package pattern="OdfKit.*" />
    </packageSource>
    <packageSource key="nuget.org">
$($externalMappings -join "`n")
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfigPath -Encoding utf8NoBOM

$libraryPackageIds = $expectedConsumerPackages | Where-Object {
    $_ -notin @("OdfKit.WebFonts.Build", "OdfKit.WebFonts.Hosting.SystemWeb")
}
$packageReferences = $libraryPackageIds |
    ForEach-Object { '    <PackageReference Include="' + $_ + '" Version="' + $packageVersion + '" />' }
$consumerProjectPath = Join-Path $consumerRoot "WebFontReleaseConsumer.csproj"
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
$($packageReferences -join "`n")
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $consumerProjectPath -Encoding utf8NoBOM
@'
Console.WriteLine("PASS: OdfKit WebFont release-feed consumer loaded.");
'@ | Set-Content -LiteralPath (Join-Path $consumerRoot "Program.cs") -Encoding utf8NoBOM

# $consumerRoot 位於方案目錄內，會被根目錄 Directory.Packages.props 的 Central Package
# Management 波及；此處以明確版本安裝已發佈套件，需退出 CPM 才能還原成功。
@"
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumerRoot "Directory.Build.props") -Encoding utf8NoBOM

$previousNugetPackages = $env:NUGET_PACKAGES
$succeeded = $false
try {
    $env:NUGET_PACKAGES = $nugetCache
    $restoreArguments = @(
        "restore",
        $consumerProjectPath,
        "--configfile",
        $nugetConfigPath,
        "--force",
        "--no-http-cache",
        "--nologo",
        "-p:NuGetAudit=true",
        "-p:NuGetAuditMode=all",
        "-p:NuGetAuditLevel=moderate",
        "-p:WarningsAsErrors=NU1900%3BNU1902%3BNU1903%3BNU1904%3BNU1905"
    )
    & dotnet @restoreArguments
    if ($LASTEXITCODE -ne 0) {
        throw "發布演練 consumer 還原或 NuGet 漏洞稽核失敗。"
    }

    $assetsPath = Join-Path $consumerRoot "obj/project.assets.json"
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -Depth 100
    foreach ($packageId in $libraryPackageIds) {
        $libraryKey = "$packageId/$packageVersion"
        if (-not $assets.libraries.PSObject.Properties[$libraryKey]) {
            throw "發布演練 consumer 未從 nupkg 還原套件：$libraryKey"
        }
        $cachePath = Join-Path $nugetCache "$($packageId.ToLowerInvariant())/$packageVersion"
        if (-not (Test-Path -LiteralPath $cachePath -PathType Container)) {
            throw "發布演練 consumer 的隔離快取缺少套件：$libraryKey"
        }
    }

    & dotnet build $consumerProjectPath -c Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "發布演練 consumer 建置失敗。"
    }
    & dotnet run --project $consumerProjectPath -c Release --no-build --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "發布演練 consumer 執行失敗。"
    }

    & dotnet tool install OdfKit.WebFonts.Build `
        --tool-path $toolRoot `
        --version $packageVersion `
        --configfile $nugetConfigPath `
        --no-cache
    if ($LASTEXITCODE -ne 0) {
        throw "發布演練無法由隔離 feed 安裝 WebFont CLI。"
    }
    $toolCommand = Get-ChildItem -LiteralPath $toolRoot -File |
        Where-Object { $_.BaseName -eq "odfkit-webfonts" -or $_.Name -eq "odfkit-webfonts" } |
        Select-Object -First 1
    if ($null -eq $toolCommand) {
        throw "發布演練的 WebFont CLI 套件缺少命令入口。"
    }
    & $toolCommand.FullName --help
    if ($LASTEXITCODE -ne 0) {
        throw "發布演練的 WebFont CLI 無法由隔離 feed 執行。"
    }

    $recoveryPackage = $packageFiles |
        Where-Object { $_.Name -eq "OdfKit.WebFonts.OpenType.$packageVersion.nupkg" } |
        Select-Object -First 1
    if ($null -eq $recoveryPackage) {
        throw "發布復原演練找不到 OpenType nupkg 快照。"
    }
    $feedPackage = Get-ChildItem -LiteralPath $feedRoot -Filter $recoveryPackage.Name -File -Recurse |
        Select-Object -First 1
    if ($null -eq $feedPackage) {
        throw "發布復原演練找不到已推送的 OpenType nupkg。"
    }

    $expectedRecoverySha256 = (Get-FileHash -LiteralPath $recoveryPackage.FullName -Algorithm SHA256).
        Hash.ToLowerInvariant()
    $quarantineRoot = Join-Path $workRoot "quarantine"
    New-Item -ItemType Directory -Path $quarantineRoot -Force | Out-Null
    Move-Item -LiteralPath $feedPackage.FullName -Destination (Join-Path $quarantineRoot $feedPackage.Name)
    Remove-Item -LiteralPath $nugetCache -Recurse -Force
    Remove-Item -LiteralPath (Join-Path $consumerRoot "obj") -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $consumerRoot "bin") -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $nugetCache -Force | Out-Null

    & dotnet @restoreArguments 2>&1 | Out-Null
    $revokedRestoreExitCode = $LASTEXITCODE
    if ($revokedRestoreExitCode -eq 0) {
        throw "發布復原演練在必要 nupkg 撤除後未能 fail closed。"
    }

    Copy-Item -LiteralPath $recoveryPackage.FullName -Destination $feedPackage.FullName
    $restoredRecoverySha256 = (Get-FileHash -LiteralPath $feedPackage.FullName -Algorithm SHA256).
        Hash.ToLowerInvariant()
    if ($restoredRecoverySha256 -ne $expectedRecoverySha256) {
        throw "發布復原演練由不可變快照復原的 nupkg SHA-256 不一致。"
    }

    Remove-Item -LiteralPath $nugetCache -Recurse -Force
    Remove-Item -LiteralPath (Join-Path $consumerRoot "obj") -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $consumerRoot "bin") -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $nugetCache -Force | Out-Null
    & dotnet @restoreArguments
    if ($LASTEXITCODE -ne 0) {
        throw "發布復原演練無法由不可變快照重新還原 consumer。"
    }
    & dotnet build $consumerProjectPath -c Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "發布復原演練重新還原後無法建置 consumer。"
    }
    & dotnet run --project $consumerProjectPath -c Release --no-build --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "發布復原演練重新還原後無法執行 consumer。"
    }

    $commitTimestamp = (& git -C $repoRoot show -s --format=%cI $revision).Trim()
    $evidence = [ordered]@{
        schemaVersion = 1
        packageVersion = $packageVersion
        sourceRevision = $revision
        sourceCommitTimestamp = $commitTimestamp
        packageCount = $packageFiles.Count
        webFontPackageCount = $internalPackageIds.Count
        auditedExternalPackageCount = $externalPackageIds.Count
        localFeedSourceMapping = "OdfKit;OdfKit.*"
        nugetAudit = [ordered]@{
            enabled = $true
            mode = "all"
            minimumReportedSeverity = "moderate"
            failureCodes = @("NU1900", "NU1902", "NU1903", "NU1904", "NU1905")
            source = "https://data.nuget.org/v3/index.json"
        }
        packageHashManifestSha256 = (Get-FileHash `
                -LiteralPath (Join-Path $packageRoot "SHA256SUMS") `
                -Algorithm SHA256).Hash.ToLowerInvariant()
        sbomSha256 = (Get-FileHash -LiteralPath $resolvedSbomPath -Algorithm SHA256).Hash.ToLowerInvariant()
        consumerTargetFramework = "net10.0"
        cliInstalledFromLocalFeed = $true
        incidentRecovery = [ordered]@{
            revokedPackage = $recoveryPackage.Name
            revokedRestoreExitCode = $revokedRestoreExitCode
            failClosed = $true
            immutableSnapshotSha256 = $expectedRecoverySha256
            restoredFeedSha256 = $restoredRecoverySha256
            restoreBuildRunSucceeded = $true
        }
    }
    $evidencePath = Join-Path $outputRoot "evidence.json"
    ($evidence | ConvertTo-Json -Depth 10) + "`n" |
        Set-Content -LiteralPath $evidencePath -Encoding utf8NoBOM
    $succeeded = $true
}
finally {
    $env:NUGET_PACKAGES = $previousNugetPackages
    if ($succeeded) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force
    }
}

Write-Host "PASS：$packageVersion 同批資產已通過本機 feed、乾淨 consumer、SBOM 消費、NuGet Audit 與撤除復原演練。"
