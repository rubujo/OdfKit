#Requires -Version 7.0
<#
.SYNOPSIS
為完整 OdfKit release set 產生可重現的 SPDX 3.0.1 JSON-LD SBOM，
並產生僅供 GitHub artifact attestation 使用的 SPDX 2.3 相容檔。
.PARAMETER PackageDirectory
已完成驗證的 NuGet 套件目錄。
.PARAMETER Spdx3OutputPath
主要 SPDX 3.0.1 JSON-LD 輸出。
.PARAMETER Spdx23OutputPath
GitHub attestation 相容用 SPDX 2.3 JSON 輸出。
.PARAMETER VerifyExisting
驗證既有檔案，不覆寫。
.PARAMETER Spdx3SchemaPath
選用的 SPDX 3.0.1 官方 JSON Schema。
.PARAMETER Spdx23SchemaPath
選用的 SPDX 2.3 官方 JSON Schema。
#>
[CmdletBinding()]
param(
    [string]$PackageDirectory = "artifacts/nuget",
    [string]$Spdx3OutputPath = "artifacts/sbom/OdfKit.spdx3.jsonld",
    [string]$Spdx23OutputPath = "artifacts/sbom/OdfKit.spdx.json",
    [switch]$VerifyExisting,
    [string]$Spdx3SchemaPath,
    [string]$Spdx23SchemaPath
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $PackageDirectory))
$spdx3Path = [IO.Path]::GetFullPath((Join-Path $repoRoot $Spdx3OutputPath))
$spdx23Path = [IO.Path]::GetFullPath((Join-Path $repoRoot $Spdx23OutputPath))
$packageVersion = & (Join-Path $PSScriptRoot "Get-PackageVersion.ps1")
if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
    throw "SBOM 找不到 NuGet 發布目錄：$packageRoot"
}

# 完整專案 SBOM 必須包含測試、工具與非 packable 專案的解析相依；不可依賴工作站上
# 可能過期或只涵蓋 packable projects 的既有 obj。先還原方案以取得一致的 assets closure。
& dotnet restore (Join-Path $repoRoot "OdfKit.slnx") --nologo
if ($LASTEXITCODE -ne 0) {
    throw "無法還原完整方案以建立 SBOM 相依閉包。"
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
$namespace = "https://github.com/rubujo/OdfKit/sbom/release/$packageVersion/$revision"

function Get-SafeId([string]$value) {
    return [regex]::Replace($value, '[^A-Za-z0-9.-]', '-')
}

function Get-NuspecMetadata([string]$packagePath) {
    $zip = [IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
        if ($null -eq $entry) {
            throw "NuGet 套件缺少 nuspec：$packagePath"
        }
        $stream = $entry.Open()
        try {
            $reader = [IO.StreamReader]::new($stream)
            try {
                return [xml]$reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $zip.Dispose()
    }
}

$releasePackages = @(
    Get-ChildItem -LiteralPath $packageRoot -File -Filter "*.nupkg" |
        Where-Object { $_.Name -notlike "*.symbols.nupkg" } |
        Sort-Object Name)
if ($releasePackages.Count -eq 0) {
    throw "SBOM 找不到任何 NuGet 發布套件。"
}

$internal = @()
foreach ($file in $releasePackages) {
    $nuspec = Get-NuspecMetadata $file.FullName
    $metadata = $nuspec.package.metadata
    $id = [string]$metadata.id
    $version = [string]$metadata.version
    if ([string]::IsNullOrWhiteSpace($id) -or $version -ne $packageVersion) {
        throw "NuGet 發布套件識別或版本不一致：$($file.Name)"
    }
    if ([string]$metadata.repository.commit -ne $revision) {
        throw "NuGet 發布套件未繫結目前 SBOM 來源提交 $revision：$($file.Name)"
    }
    $internal += [pscustomobject]@{
        Id = $id
        Version = $version
        File = $file
        SpdxId = "$namespace#SPDXRef-Package-$(Get-SafeId $id)"
    }
}

$externalByKey = [Collections.Generic.SortedDictionary[string, object]]::new(
    [StringComparer]::OrdinalIgnoreCase)
$assetsFiles = Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Filter "project.assets.json" |
    Where-Object {
        $_.FullName -notmatch '[\\/]artifacts[\\/]' -and
        $_.FullName -notmatch '[\\/]\.git[\\/]'
    }
foreach ($assetsFile in $assetsFiles) {
    $assets = Get-Content -LiteralPath $assetsFile.FullName -Raw | ConvertFrom-Json -Depth 100
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ([string]$library.Value.type -ne "package" -or $library.Name -notmatch '^(?<id>.+)/(?<version>[^/]+)$') {
            continue
        }
        $id = $Matches.id
        $version = $Matches.version
        $key = "$id/$version"
        if (-not $externalByKey.ContainsKey($key)) {
            $externalByKey.Add($key, [pscustomobject]@{
                    Id = $id
                    Version = $version
                    SpdxId = "$namespace#SPDXRef-Dependency-$(Get-SafeId $id)-$(Get-SafeId $version)"
                })
        }
    }
}
if ($externalByKey.Count -eq 0) {
    throw "SBOM 未從 project.assets.json 找到任何實際解析的 NuGet 相依。"
}

$creationInfoId = "_:creationInfo"
$organizationId = "$namespace#SPDXRef-Organization-OdfKit"
$toolId = "$namespace#SPDXRef-Tool-Test-ReleaseSbom"
$documentId = "$namespace#SPDXRef-DOCUMENT"
$sbomId = "$namespace#SPDXRef-SBOM"
$rootId = "$namespace#SPDXRef-ReleaseSet"
$elementIds = [Collections.Generic.List[string]]::new()
$graph = [Collections.Generic.List[object]]::new()
$graph.Add([ordered]@{
        "@id" = $creationInfoId
        type = "CreationInfo"
        specVersion = "3.0.1"
        created = $created
        createdBy = @($organizationId)
        createdUsing = @($toolId)
    })
$graph.Add([ordered]@{
        type = "Organization"
        spdxId = $organizationId
        name = "OdfKit contributors"
        creationInfo = $creationInfoId
    })
$graph.Add([ordered]@{
        type = "Tool"
        spdxId = $toolId
        name = "eng/Test-ReleaseSbom.ps1"
        creationInfo = $creationInfoId
    })
$elementIds.Add($organizationId)
$elementIds.Add($toolId)

$graph.Add([ordered]@{
        type = "software_Package"
        spdxId = $rootId
        name = "OdfKit release set"
        software_packageVersion = $packageVersion
        software_downloadLocation = "https://github.com/rubujo/OdfKit/releases/tag/v$packageVersion"
        software_homePage = "https://github.com/rubujo/OdfKit"
        software_primaryPurpose = "library"
        creationInfo = $creationInfoId
    })
$elementIds.Add($rootId)

foreach ($package in $internal) {
    $graph.Add([ordered]@{
            type = "software_Package"
            spdxId = $package.SpdxId
            name = $package.Id
            software_packageVersion = $package.Version
            software_downloadLocation = "https://github.com/rubujo/OdfKit/releases/tag/v$packageVersion"
            software_packageUrl = "pkg:nuget/$($package.Id)@$($package.Version)"
            software_primaryPurpose = "library"
            verifiedUsing = @([ordered]@{
                    type = "Hash"
                    algorithm = "sha256"
                    hashValue = (Get-FileHash -LiteralPath $package.File.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                })
            creationInfo = $creationInfoId
        })
    $elementIds.Add($package.SpdxId)
}
foreach ($dependency in $externalByKey.Values) {
    $graph.Add([ordered]@{
            type = "software_Package"
            spdxId = $dependency.SpdxId
            name = $dependency.Id
            software_packageVersion = $dependency.Version
            software_downloadLocation = "https://www.nuget.org/packages/$($dependency.Id)/$($dependency.Version)"
            software_packageUrl = "pkg:nuget/$($dependency.Id)@$($dependency.Version)"
            software_primaryPurpose = "library"
            creationInfo = $creationInfoId
        })
    $elementIds.Add($dependency.SpdxId)
}

$relationshipNumber = 0
foreach ($package in $internal) {
    $relationshipId = "$namespace#SPDXRef-Relationship-$relationshipNumber"
    $relationshipNumber++
    $graph.Add([ordered]@{
            type = "Relationship"
            spdxId = $relationshipId
            from = $rootId
            to = @($package.SpdxId)
            relationshipType = "contains"
            creationInfo = $creationInfoId
        })
    $elementIds.Add($relationshipId)
}
foreach ($dependency in $externalByKey.Values) {
    $relationshipId = "$namespace#SPDXRef-Relationship-$relationshipNumber"
    $relationshipNumber++
    $graph.Add([ordered]@{
            type = "Relationship"
            spdxId = $relationshipId
            from = $rootId
            to = @($dependency.SpdxId)
            relationshipType = "dependsOn"
            creationInfo = $creationInfoId
        })
    $elementIds.Add($relationshipId)
}

$graph.Add([ordered]@{
        type = "software_Sbom"
        spdxId = $sbomId
        name = "OdfKit complete release SBOM"
        software_sbomType = @("build")
        element = @($elementIds)
        rootElement = @($rootId)
        creationInfo = $creationInfoId
    })
$documentElements = @($elementIds) + @($sbomId)
$graph.Add([ordered]@{
        type = "SpdxDocument"
        spdxId = $documentId
        name = "OdfKit-$packageVersion"
        profileConformance = @("core", "software")
        element = $documentElements
        rootElement = @($sbomId)
        creationInfo = $creationInfoId
    })

$spdx3 = [ordered]@{
    "@context" = "https://spdx.org/rdf/3.0.1/spdx-context.jsonld"
    "@graph" = $graph
}
$spdx3Json = (($spdx3 | ConvertTo-Json -Depth 30) -replace "`r?`n", "`n") + "`n"

$spdx23Packages = @([ordered]@{
        SPDXID = "SPDXRef-ReleaseSet"
        name = "OdfKit release set"
        versionInfo = $packageVersion
        downloadLocation = "https://github.com/rubujo/OdfKit/releases/tag/v$packageVersion"
        filesAnalyzed = $false
        licenseConcluded = "CC0-1.0"
        licenseDeclared = "CC0-1.0"
        copyrightText = "NOASSERTION"
    })
foreach ($package in $internal) {
    $spdx23Packages += [ordered]@{
        SPDXID = "SPDXRef-Package-$(Get-SafeId $package.Id)"
        name = $package.Id
        versionInfo = $package.Version
        packageFileName = $package.File.Name
        downloadLocation = "https://github.com/rubujo/OdfKit/releases/tag/v$packageVersion"
        filesAnalyzed = $false
        checksums = @([ordered]@{
                algorithm = "SHA256"
                checksumValue = (Get-FileHash -LiteralPath $package.File.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            })
        licenseConcluded = "CC0-1.0"
        licenseDeclared = "CC0-1.0"
        copyrightText = "NOASSERTION"
        externalRefs = @([ordered]@{
                referenceCategory = "PACKAGE-MANAGER"
                referenceType = "purl"
                referenceLocator = "pkg:nuget/$($package.Id)@$($package.Version)"
            })
    }
}
foreach ($dependency in $externalByKey.Values) {
    $spdx23Packages += [ordered]@{
        SPDXID = "SPDXRef-Dependency-$(Get-SafeId $dependency.Id)-$(Get-SafeId $dependency.Version)"
        name = $dependency.Id
        versionInfo = $dependency.Version
        downloadLocation = "https://www.nuget.org/packages/$($dependency.Id)/$($dependency.Version)"
        filesAnalyzed = $false
        licenseConcluded = "NOASSERTION"
        licenseDeclared = "NOASSERTION"
        copyrightText = "NOASSERTION"
        externalRefs = @([ordered]@{
                referenceCategory = "PACKAGE-MANAGER"
                referenceType = "purl"
                referenceLocator = "pkg:nuget/$($dependency.Id)@$($dependency.Version)"
            })
    }
}
$spdx23Relationships = @()
foreach ($package in $spdx23Packages | Select-Object -Skip 1) {
    $spdx23Relationships += [ordered]@{
        spdxElementId = "SPDXRef-ReleaseSet"
        relationshipType = if ($package.SPDXID -like "SPDXRef-Package-*") { "CONTAINS" } else { "DEPENDS_ON" }
        relatedSpdxElement = $package.SPDXID
    }
}
$spdx23 = [ordered]@{
    spdxVersion = "SPDX-2.3"
    dataLicense = "CC0-1.0"
    SPDXID = "SPDXRef-DOCUMENT"
    name = "OdfKit-$packageVersion-attestation-compat"
    documentNamespace = "$namespace/attestation-compat"
    creationInfo = [ordered]@{
        created = $created
        creators = @("Organization: OdfKit contributors", "Tool: eng/Test-ReleaseSbom.ps1")
    }
    documentDescribes = @("SPDXRef-ReleaseSet")
    packages = $spdx23Packages
    relationships = $spdx23Relationships
}
$spdx23Json = (($spdx23 | ConvertTo-Json -Depth 30) -replace "`r?`n", "`n") + "`n"

function Test-OrWrite([string]$path, [string]$expected, [string]$label) {
    if ($VerifyExisting) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "缺少待驗證的 $label：$path"
        }
        $actual = ([IO.File]::ReadAllText($path) -replace "`r?`n", "`n")
        if (-not [string]::Equals($actual, $expected, [StringComparison]::Ordinal)) {
            throw "$label 與目前提交、發布套件或解析相依不一致：$path"
        }
    }
    else {
        [IO.Directory]::CreateDirectory((Split-Path -Parent $path)) | Out-Null
        [IO.File]::WriteAllText($path, $expected, [Text.UTF8Encoding]::new($false))
    }
}

Test-OrWrite $spdx3Path $spdx3Json "SPDX 3.0.1 SBOM"
Test-OrWrite $spdx23Path $spdx23Json "SPDX 2.3 attestation 相容 SBOM"

if (-not [string]::IsNullOrWhiteSpace($Spdx3SchemaPath)) {
    $schema = [IO.Path]::GetFullPath((Join-Path $repoRoot $Spdx3SchemaPath))
    if (-not ($spdx3Json | Test-Json -SchemaFile $schema)) {
        throw "完整 release SBOM 未通過 SPDX 3.0.1 官方 JSON Schema。"
    }
}
if (-not [string]::IsNullOrWhiteSpace($Spdx23SchemaPath)) {
    $schema = [IO.Path]::GetFullPath((Join-Path $repoRoot $Spdx23SchemaPath))
    if (-not ($spdx23Json | Test-Json -SchemaFile $schema)) {
        throw "attestation 相容 SBOM 未通過 SPDX 2.3 官方 JSON Schema。"
    }
}

$mode = if ($VerifyExisting) { "驗證" } else { "產生" }
Write-Host "OK：已$mode完整 release SPDX 3.0.1 SBOM（$($internal.Count) 個發布套件、$($externalByKey.Count) 個解析相依）與 SPDX 2.3 attestation 相容檔。"
