param(
    [string[]] $ManifestPath = @(
        "tools/OdfSchemaGenerator/oasis-odf14-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf13-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf12-schema.json",
        "tools/OdfSchemaGenerator/oasis-odf11-schema.json"
    )
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RequiredManifestValue {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Manifest,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $property = $Manifest.PSObject.Properties[$Name]
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string] $property.Value)) {
        throw "Schema generation manifest is missing required property: $Name"
    }

    return [string] $property.Value
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

    $manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
    $schemaPath = Join-Path $RepoRoot (Get-RequiredManifestValue -Manifest $manifest -Name "schemaPath")
    $outputPath = Join-Path $RepoRoot (Get-RequiredManifestValue -Manifest $manifest -Name "outputPath")
    $format = Get-RequiredManifestValue -Manifest $manifest -Name "format"
    $className = Get-RequiredManifestValue -Manifest $manifest -Name "className"
    $sourceUrl = Get-RequiredManifestValue -Manifest $manifest -Name "sourceUrl"
    $sourceDate = Get-RequiredManifestValue -Manifest $manifest -Name "sourceDate"
    $version = Get-RequiredManifestValue -Manifest $manifest -Name "version"

    if (-not (Test-Path -LiteralPath $schemaPath)) {
        throw "Schema source file not found: $schemaPath"
    }

    $outputDirectory = Split-Path -Parent $outputPath
    if (-not (Test-Path -LiteralPath $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
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
        $version,
        "--output",
        $outputPath,
        $schemaPath
    )

    & dotnet @arguments
}

$repoRoot = Split-Path -Parent $PSScriptRoot

foreach ($path in $ManifestPath) {
    Invoke-SchemaGeneration -RepoRoot $repoRoot -ManifestRelativePath $path
}
