param(
    [string] $ManifestPath = "tools/OdfSchemaGenerator/oasis-odf14-schema.json"
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

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestFullPath = Join-Path $repoRoot $ManifestPath
if (-not (Test-Path -LiteralPath $manifestFullPath)) {
    throw "Schema generation manifest not found: $manifestFullPath"
}

$manifest = Get-Content -LiteralPath $manifestFullPath -Raw | ConvertFrom-Json
$schemaPath = Join-Path $repoRoot (Get-RequiredManifestValue -Manifest $manifest -Name "schemaPath")
$outputPath = Join-Path $repoRoot (Get-RequiredManifestValue -Manifest $manifest -Name "outputPath")
$format = Get-RequiredManifestValue -Manifest $manifest -Name "format"
$className = Get-RequiredManifestValue -Manifest $manifest -Name "className"
$sourceUrl = Get-RequiredManifestValue -Manifest $manifest -Name "sourceUrl"
$sourceDate = Get-RequiredManifestValue -Manifest $manifest -Name "sourceDate"

if (-not (Test-Path -LiteralPath $schemaPath)) {
    throw "Schema source file not found: $schemaPath"
}

$outputDirectory = Split-Path -Parent $outputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$generatorProject = Join-Path $repoRoot "tools/OdfSchemaGenerator/OdfSchemaGenerator.csproj"
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
    "--output",
    $outputPath,
    $schemaPath
)

& dotnet @arguments
