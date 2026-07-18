#Requires -Version 7.0
<#
.SYNOPSIS
    驗證 WebFont 相依授權漂移，並產生可重現的 SPDX 2.3 SBOM。
.PARAMETER PackageDirectory
    已由 OdfKit 既有 pack 流程產生的 NuGet 套件目錄。
.PARAMETER OutputPath
    SPDX JSON 輸出路徑。
.PARAMETER VerifyExisting
    不覆寫輸出，改為驗證既有 SBOM 與目前提交、套件及相依完全一致。
.PARAMETER SchemaPath
    選用的 SPDX 2.3 官方 JSON schema；提供時會執行獨立 schema 驗證。
.PARAMETER SkipRestoreClosureValidation
    僅於 consumer runner 驗證既有 SBOM 時，略過原始專案 restore closure 與 NuGet 快取授權宣告驗證。
#>
[CmdletBinding()]
param(
    [string]$PackageDirectory = "artifacts/nuget",
    [string]$OutputPath = "artifacts/webfont-sbom/manifest.spdx.json",
    [switch]$VerifyExisting,
    [switch]$SkipRestoreClosureValidation,
    [string]$SchemaPath
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $PSScriptRoot
$policyPath = Join-Path $PSScriptRoot "webfont-dependency-policy.json"
& (Join-Path $PSScriptRoot "Test-WebFontStandardsAndDependencies.ps1")
$packageRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $PackageDirectory))
$sbomPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
$packageVersion = & (Join-Path $PSScriptRoot "Get-PackageVersion.ps1")

$projectPaths = @(
    "OdfKit.Extensions.Html.WebFonts/OdfKit.Extensions.Html.WebFonts.csproj",
    "OdfKit.WebFonts.Abstractions/OdfKit.WebFonts.Abstractions.csproj",
    "OdfKit.WebFonts.Build/OdfKit.WebFonts.Build.csproj",
    "OdfKit.WebFonts.Data.SqlServer/OdfKit.WebFonts.Data.SqlServer.csproj",
    "OdfKit.WebFonts.Encoding.Legacy/OdfKit.WebFonts.Encoding.Legacy.csproj",
    "OdfKit.WebFonts.Hosting.AspNetCore/OdfKit.WebFonts.Hosting.AspNetCore.csproj",
    "OdfKit.WebFonts.Hosting.SystemWeb/OdfKit.WebFonts.Hosting.SystemWeb.csproj",
    "OdfKit.WebFonts.OpenType/OdfKit.WebFonts.OpenType.csproj",
    "OdfKit.WebFonts.Profiles/OdfKit.WebFonts.Profiles.csproj",
    "OdfKit.WebFonts.Windows/OdfKit.WebFonts.Windows.csproj",
    "OdfKit.WebFonts.Worker/OdfKit.WebFonts.Worker.csproj"
)

$expectedPackageIds = @(
    "OdfKit.Extensions.Html.WebFonts",
    "OdfKit.WebFonts.Abstractions",
    "OdfKit.WebFonts.Build",
    "OdfKit.WebFonts.Data.SqlServer",
    "OdfKit.WebFonts.Encoding.Legacy",
    "OdfKit.WebFonts.Hosting.AspNetCore",
    "OdfKit.WebFonts.Hosting.SystemWeb",
    "OdfKit.WebFonts.OpenType",
    "OdfKit.WebFonts.Profiles",
    "OdfKit.WebFonts.Windows",
    "OdfKit.WebFonts.Worker"
)

function Get-SpdxId([string]$Prefix, [string]$Value) {
    return "SPDXRef-$Prefix-" + ($Value -replace '[^A-Za-z0-9.-]', '-')
}

function Get-NuspecMetadata([string]$PackagePath) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
        if (-not $entry) {
            throw "套件缺少 nuspec：$PackagePath"
        }

        $reader = [System.IO.StreamReader]::new($entry.Open())
        try {
            return [xml]$reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $zip.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $policyPath -PathType Leaf)) {
    throw "缺少 WebFont 相依授權政策：$policyPath"
}
if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
    throw "缺少 NuGet 套件目錄：$packageRoot"
}

$policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json -Depth 20
if ($policy.schemaVersion -ne 1) {
    throw "不支援的 WebFont 相依政策版本：$($policy.schemaVersion)"
}
if ($SkipRestoreClosureValidation -and -not $VerifyExisting) {
    throw "SkipRestoreClosureValidation 僅能搭配 VerifyExisting 使用。"
}

$allowedLicenses = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]$policy.allowedLicenses,
    [StringComparer]::Ordinal)
$policyById = [System.Collections.Generic.Dictionary[string, object]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $policy.packages) {
    if (-not $policyById.TryAdd([string]$entry.id, $entry)) {
        throw "WebFont 相依政策含重複套件：$($entry.id)"
    }
    if (-not $allowedLicenses.Contains([string]$entry.license)) {
        throw "WebFont 相依政策含未允許授權：$($entry.id) $($entry.license)"
    }
}

$resolvedById = [System.Collections.Generic.Dictionary[string, string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
$packageFolders = [System.Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
if (-not $SkipRestoreClosureValidation) {
    foreach ($relativeProjectPath in $projectPaths) {
        $projectPath = Join-Path $repoRoot $relativeProjectPath
        $assetsPath = Join-Path (Split-Path -Parent $projectPath) "obj/project.assets.json"
        if (-not (Test-Path -LiteralPath $assetsPath -PathType Leaf)) {
            throw "缺少 restore 資產；請先執行既有 pack 或 restore：$assetsPath"
        }

        $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -Depth 100
        foreach ($folderProperty in $assets.packageFolders.PSObject.Properties) {
            [void]$packageFolders.Add([System.IO.Path]::GetFullPath($folderProperty.Name))
        }
        foreach ($libraryProperty in $assets.libraries.PSObject.Properties) {
            if ([string]$libraryProperty.Value.type -ne "package") {
                continue
            }

            $separatorIndex = $libraryProperty.Name.LastIndexOf('/')
            if ($separatorIndex -le 0) {
                throw "無效的 restore 套件識別：$($libraryProperty.Name)"
            }
            $id = $libraryProperty.Name.Substring(0, $separatorIndex)
            $version = $libraryProperty.Name.Substring($separatorIndex + 1)
            $existingVersion = $null
            if ($resolvedById.TryGetValue($id, [ref]$existingVersion)) {
                if (-not [string]::Equals($existingVersion, $version, [StringComparison]::Ordinal)) {
                    throw "WebFont 相依同時解析出多個版本：$id $existingVersion / $version"
                }
            }
            else {
                $resolvedById.Add($id, $version)
            }
        }
    }
}

if (-not $SkipRestoreClosureValidation) {
    foreach ($resolved in $resolvedById.GetEnumerator()) {
        $policyEntry = $null
        if (-not $policyById.TryGetValue($resolved.Key, [ref]$policyEntry)) {
            throw "WebFont 出現未審核相依：$($resolved.Key) $($resolved.Value)"
        }
        if (-not [string]::Equals([string]$policyEntry.version, $resolved.Value, [StringComparison]::Ordinal)) {
            throw "WebFont 相依版本漂移：$($resolved.Key) $($policyEntry.version) -> $($resolved.Value)"
        }

        $packageCachePath = $null
        foreach ($packageFolder in $packageFolders) {
            $candidate = Join-Path $packageFolder "$($resolved.Key.ToLowerInvariant())/$($resolved.Value.ToLowerInvariant())"
            if (Test-Path -LiteralPath $candidate -PathType Container) {
                $packageCachePath = $candidate
                break
            }
        }
        if (-not $packageCachePath) {
            throw "NuGet global-packages 缺少已解析相依：$($resolved.Key) $($resolved.Value)"
        }

        $nuspecPath = Get-ChildItem -LiteralPath $packageCachePath -Filter "*.nuspec" -File | Select-Object -First 1
        if (-not $nuspecPath) {
            throw "NuGet 快取缺少 nuspec：$($resolved.Key) $($resolved.Value)"
        }
        [xml]$dependencyNuspec = Get-Content -LiteralPath $nuspecPath.FullName -Raw
        $metadata = $dependencyNuspec.package.metadata
        if ([string]$policyEntry.declarationKind -eq "expression") {
            $actualDeclaration = [string]$metadata.license.InnerText
            $actualKind = [string]$metadata.license.type
            if ($actualKind -ne "expression" -or $actualDeclaration -ne [string]$policyEntry.declaration) {
                throw "NuGet 授權宣告漂移：$($resolved.Key) $actualKind $actualDeclaration"
            }
        }
        elseif ([string]$policyEntry.declarationKind -eq "url") {
            $actualDeclaration = [string]$metadata.licenseUrl
            if ($actualDeclaration -ne [string]$policyEntry.declaration) {
                throw "NuGet 授權 URL 漂移：$($resolved.Key) $actualDeclaration"
            }
        }
        else {
            throw "未知的授權宣告種類：$($policyEntry.declarationKind)"
        }
    }

    foreach ($policyEntry in $policy.packages) {
        if (-not $resolvedById.ContainsKey([string]$policyEntry.id) -and -not [bool]$policyEntry.optional) {
            throw "WebFont 相依政策含已不再解析的套件：$($policyEntry.id)"
        }
    }
}

$internalPackages = @()
foreach ($id in $expectedPackageIds) {
    $packagePath = Join-Path $packageRoot "$id.$packageVersion.nupkg"
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "SBOM 缺少 WebFont 發布套件：$packagePath"
    }
    $nuspecXml = Get-NuspecMetadata $packagePath
    if ([string]$nuspecXml.package.metadata.id -ne $id `
        -or [string]$nuspecXml.package.metadata.version -ne $packageVersion) {
        throw "SBOM 套件識別與檔名不一致：$packagePath"
    }

    $internalPackages += [ordered]@{
        SPDXID = Get-SpdxId "Package" "$id-$packageVersion"
        name = $id
        versionInfo = $packageVersion
        packageFileName = [System.IO.Path]::GetFileName($packagePath)
        downloadLocation = "NOASSERTION"
        filesAnalyzed = $false
        checksums = @([ordered]@{
            algorithm = "SHA256"
            checksumValue = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
        })
        licenseConcluded = "CC0-1.0"
        licenseDeclared = "CC0-1.0"
        copyrightText = "NOASSERTION"
        supplier = "Organization: OdfKit contributors"
        externalRefs = @([ordered]@{
            referenceCategory = "PACKAGE-MANAGER"
            referenceType = "purl"
            referenceLocator = "pkg:nuget/$id@$packageVersion"
        })
    }
}

$externalPackages = @()
foreach ($entry in $policy.packages | Sort-Object id) {
    $externalPackages += [ordered]@{
        SPDXID = Get-SpdxId "Dependency" "$($entry.id)-$($entry.version)"
        name = [string]$entry.id
        versionInfo = [string]$entry.version
        downloadLocation = "https://www.nuget.org/packages/$($entry.id)/$($entry.version)"
        filesAnalyzed = $false
        licenseConcluded = [string]$entry.license
        licenseDeclared = [string]$entry.license
        copyrightText = "NOASSERTION"
        externalRefs = @([ordered]@{
            referenceCategory = "PACKAGE-MANAGER"
            referenceType = "purl"
            referenceLocator = "pkg:nuget/$($entry.id)@$($entry.version)"
        })
    }
}

$revision = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{40}$') {
    throw "無法取得 SBOM 來源提交。"
}
$created = [DateTimeOffset]::Parse(
    (& git -C $repoRoot show -s --format=%cI $revision).Trim(),
    [Globalization.CultureInfo]::InvariantCulture).UtcDateTime.ToString(
        "yyyy-MM-ddTHH:mm:ssZ",
        [Globalization.CultureInfo]::InvariantCulture)
$rootId = "SPDXRef-Package-OdfKit-WebFonts"
$relationships = @([ordered]@{
    spdxElementId = "SPDXRef-DOCUMENT"
    relationshipType = "DESCRIBES"
    relatedSpdxElement = $rootId
})
foreach ($package in $internalPackages) {
    $relationships += [ordered]@{
        spdxElementId = $rootId
        relationshipType = "CONTAINS"
        relatedSpdxElement = $package.SPDXID
    }
}
foreach ($package in $externalPackages) {
    $relationships += [ordered]@{
        spdxElementId = $rootId
        relationshipType = "DEPENDS_ON"
        relatedSpdxElement = $package.SPDXID
    }
}

$rootPackage = [ordered]@{
    SPDXID = $rootId
    name = "OdfKit WebFonts release set"
    versionInfo = $packageVersion
    downloadLocation = "NOASSERTION"
    filesAnalyzed = $false
    licenseConcluded = "CC0-1.0"
    licenseDeclared = "CC0-1.0"
    copyrightText = "NOASSERTION"
    supplier = "Organization: OdfKit contributors"
    externalRefs = @([ordered]@{
        referenceCategory = "OTHER"
        referenceType = "vcs"
        referenceLocator = "git+https://github.com/rubujo/OdfKit.git@$revision"
    })
}
$document = [ordered]@{
    spdxVersion = "SPDX-2.3"
    dataLicense = "CC0-1.0"
    SPDXID = "SPDXRef-DOCUMENT"
    name = "OdfKit-WebFonts-$packageVersion"
    documentNamespace = "https://github.com/rubujo/OdfKit/sbom/webfonts/$packageVersion/$revision"
    creationInfo = [ordered]@{
        created = $created
        creators = @(
            "Organization: OdfKit contributors",
            "Tool: eng/Test-WebFontSupplyChain.ps1"
        )
        licenseListVersion = "3.28.0"
    }
    documentDescribes = @($rootId)
    packages = @($rootPackage) + $internalPackages + $externalPackages
    relationships = $relationships
}

$expectedJson = (($document | ConvertTo-Json -Depth 20) -replace "`r?`n", "`n") + "`n"
if ($VerifyExisting) {
    if (-not (Test-Path -LiteralPath $sbomPath -PathType Leaf)) {
        throw "缺少待驗證的 WebFont SBOM：$sbomPath"
    }
    $actualJson = ([System.IO.File]::ReadAllText($sbomPath) -replace "`r?`n", "`n")
    if (-not [string]::Equals($actualJson, $expectedJson, [StringComparison]::Ordinal)) {
        throw "WebFont SBOM 與目前提交、套件或相依不一致：$sbomPath"
    }
    Write-Host "OK：WebFont SPDX 2.3 SBOM 與發布產物一致。"
}
else {
    $outputDirectory = Split-Path -Parent $sbomPath
    [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
    [System.IO.File]::WriteAllText(
        $sbomPath,
        $expectedJson,
        [System.Text.UTF8Encoding]::new($false))
    Write-Host "OK：已產生 WebFont SPDX 2.3 SBOM：$sbomPath"
}

if (-not [string]::IsNullOrWhiteSpace($SchemaPath)) {
    $schemaFullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $SchemaPath))
    if (-not (Test-Path -LiteralPath $schemaFullPath -PathType Leaf)) {
        throw "缺少 SPDX 2.3 JSON schema：$schemaFullPath"
    }
    $schemaValid = $expectedJson | Test-Json -SchemaFile $schemaFullPath
    if (-not $schemaValid) {
        throw "WebFont SBOM 未通過 SPDX 2.3 官方 schema：$sbomPath"
    }
    Write-Host "OK：WebFont SBOM 通過 SPDX 2.3 官方 JSON schema。"
}

if ($SkipRestoreClosureValidation) {
    Write-Host "OK：consumer runner 已驗證 WebFont SBOM、提交與 NuGet 發布產物一致。"
}
else {
    Write-Host "OK：WebFont $($resolvedById.Count) 個 NuGet 相依版本與授權宣告未漂移。"
}
