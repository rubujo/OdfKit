#Requires -Version 7.0
<#
.SYNOPSIS
    依 OASIS schema manifest 產生官方 schema provider 與／或 typed DOM wrappers。
.DESCRIPTION
    預設重產 ODF 1.0～1.4 schema provider，以及 ODF 1.4 typed DOM wrappers
    （OdfKit/DOM/Generated）。DOM wrappers 禁止手改；ctor／多載形狀僅能改
    tools/OdfSchemaGenerator 後重跑本腳本。

.PARAMETER ManifestPath
    Manifest 相對路徑清單。省略時使用內建預設（五版 schema + DOM wrappers）。
#>
param(
    [string[]] $ManifestPath = @(
        "tools/OdfSchemaGenerator/oasis-odf14-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf13-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf12-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf11-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf10-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf14-manifest-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf14-dsig-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf13-manifest-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf13-dsig-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf12-manifest-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf12-dsig-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf11-manifest-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf10-manifest-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf14-dom-wrappers.json"
    )
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-OptionalManifestValue {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Manifest,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $property = $Manifest.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    $value = [string] $property.Value
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value
}

function Get-RequiredManifestValue {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Manifest,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $value = Get-OptionalManifestValue -Manifest $Manifest -Name $Name
    if ($null -eq $value) {
        throw "Schema generation manifest is missing required property: $Name"
    }

    return $value
}

function Invoke-SchemaGeneration {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepoRoot,

        [Parameter(Mandatory = $true)]
        [string] $ManifestRelativePath
    )

    $manifestFullPath = Join-Path $RepoRoot $ManifestRelativePath
    if (-not (Test-Path -LiteralPath $manifestFullPath)) {
        throw "Schema generation manifest not found: $manifestFullPath"
    }

    Write-Host "Generating from $ManifestRelativePath …"
    $manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
    $schemaPath = Join-Path $RepoRoot (Get-RequiredManifestValue -Manifest $manifest -Name "schemaPath")
    $format = Get-RequiredManifestValue -Manifest $manifest -Name "format"
    $className = Get-RequiredManifestValue -Manifest $manifest -Name "className"
    $sourceUrl = Get-RequiredManifestValue -Manifest $manifest -Name "sourceUrl"
    $sourceDate = Get-RequiredManifestValue -Manifest $manifest -Name "sourceDate"
    $version = Get-RequiredManifestValue -Manifest $manifest -Name "version"
    $outputPathRel = Get-OptionalManifestValue -Manifest $manifest -Name "outputPath"
    $outputDirectoryRel = Get-OptionalManifestValue -Manifest $manifest -Name "outputDirectory"

    if (-not (Test-Path -LiteralPath $schemaPath)) {
        throw "Schema source file not found: $schemaPath"
    }

    if ($null -ne $outputPathRel -and $null -ne $outputDirectoryRel) {
        throw "Manifest $ManifestRelativePath must not set both outputPath and outputDirectory."
    }

    if ($null -eq $outputPathRel -and $null -eq $outputDirectoryRel) {
        throw "Manifest $ManifestRelativePath must set outputPath or outputDirectory."
    }

    $generatorProject = Join-Path $RepoRoot "tools/OdfSchemaGenerator/OdfSchemaGenerator.csproj"
    $arguments = @(
        "run",
        "--project",
        $generatorProject,
        "--",
        "--format",
        $format,
        "--class-name",
        $className,
        "--source-url",
        $sourceUrl,
        "--source-date",
        $sourceDate,
        "--version",
        $version
    )

    if ($null -ne $outputDirectoryRel) {
        if (-not [string]::Equals($format, "dom-wrappers", [StringComparison]::OrdinalIgnoreCase)) {
            throw "outputDirectory is only valid when format is dom-wrappers (manifest: $ManifestRelativePath)."
        }

        $outputDirectory = Join-Path $RepoRoot $outputDirectoryRel
        if (-not (Test-Path -LiteralPath $outputDirectory)) {
            New-Item -ItemType Directory -Path $outputDirectory | Out-Null
        }

        $arguments += @("--output-directory", $outputDirectory)
    }
    else {
        $outputPath = Join-Path $RepoRoot $outputPathRel
        $parent = Split-Path -Parent $outputPath
        if (-not (Test-Path -LiteralPath $parent)) {
            New-Item -ItemType Directory -Path $parent | Out-Null
        }

        $arguments += @("--output", $outputPath)
    }

    $arguments += $schemaPath
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "OdfSchemaGenerator failed for $ManifestRelativePath (exit $LASTEXITCODE)."
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot

foreach ($path in $ManifestPath) {
    Invoke-SchemaGeneration -RepoRoot $repoRoot -ManifestRelativePath $path
}

Write-Host "Schema generation complete."
