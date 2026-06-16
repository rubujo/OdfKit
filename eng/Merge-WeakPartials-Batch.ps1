#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$kit = Join-Path $PSScriptRoot '..\OdfKit'

function Get-RegionBlock {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$RegionMarker
    )
    $regionStart = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match [regex]::Escape($RegionMarker)) {
            $regionStart = $i
            break
        }
    }
    if ($regionStart -lt 0) { throw "Could not find region: $RegionMarker" }

    $regionEnd = -1
    for ($i = $regionStart + 1; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '#endregion') {
            $regionEnd = $i
            break
        }
    }
    if ($regionEnd -lt 0) { throw "Could not find #endregion after $RegionMarker" }
    return ,$Lines[$regionStart..$regionEnd]
}

function Merge-RegionIntoCore {
    param(
        [string]$CorePath,
        [string]$SourcePath,
        [string]$RegionMarker
    )
    if (-not (Test-Path -LiteralPath $SourcePath)) {
        Write-Host "Skip missing: $SourcePath"
        return
    }

    $coreLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $CorePath -Encoding UTF8)
    $sourceLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $SourcePath -Encoding UTF8)
    $block = Get-RegionBlock -Lines $sourceLines -RegionMarker $RegionMarker

    if ($coreLines[$coreLines.Count - 1] -ne '}') {
        throw "Expected $CorePath to end with }"
    }
    $coreLines.RemoveAt($coreLines.Count - 1)
    [void]$coreLines.Add('')
    foreach ($line in $block) { [void]$coreLines.Add($line) }
    [void]$coreLines.Add('')
    [void]$coreLines.Add('}')

    Set-Content -LiteralPath $CorePath -Value $coreLines -Encoding UTF8
    Remove-Item -LiteralPath $SourcePath -Force
    Write-Host "Merged $([IO.Path]::GetFileName($SourcePath)) -> $([IO.Path]::GetFileName($CorePath))"
}

function Ensure-Using {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$UsingLine
    )
    if ($Lines -notcontains $UsingLine) {
        $insertAt = 0
        for ($i = 0; $i -lt $Lines.Count; $i++) {
            if ($Lines[$i] -match '^using ') { $insertAt = $i }
        }
        $Lines.Insert($insertAt + 1, $UsingLine)
    }
}

# 1. OdfTypedDomCoverage: Primitive + Extended -> PropertyTypes
$propTypesPath = Join-Path $kit 'DOM\OdfTypedDomCoverage.PropertyTypes.cs'
$primitivePath = Join-Path $kit 'DOM\OdfTypedDomCoverage.PropertyTypes.Primitive.cs'
$extendedPath = Join-Path $kit 'DOM\OdfTypedDomCoverage.PropertyTypes.Extended.cs'
$propLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $propTypesPath -Encoding UTF8)
Ensure-Using -Lines $propLines -UsingLine 'using OdfKit.Styles;'
$primitiveLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $primitivePath -Encoding UTF8)
$extendedLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $extendedPath -Encoding UTF8)
$primitiveBlock = Get-RegionBlock -Lines $primitiveLines -RegionMarker '#region Primitive Property Types'
$extendedBlock = Get-RegionBlock -Lines $extendedLines -RegionMarker '#region Extended Property Types'
if ($propLines[$propLines.Count - 1] -ne '}') { throw 'PropertyTypes.cs must end with }' }
$propLines.RemoveAt($propLines.Count - 1)
[void]$propLines.Add('')
foreach ($line in $primitiveBlock) { [void]$propLines.Add($line) }
[void]$propLines.Add('')
foreach ($line in $extendedBlock) { [void]$propLines.Add($line) }
[void]$propLines.Add('')
[void]$propLines.Add('}')
Set-Content -LiteralPath $propTypesPath -Value $propLines -Encoding UTF8
Remove-Item -LiteralPath $primitivePath, $extendedPath -Force
Write-Host 'Merged OdfTypedDomCoverage PropertyTypes partials'

# 2. OdfSchemaPatternValidator: Candidates -> Validation
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Compliance\OdfSchemaPatternValidator.Attributes.Validation.cs') `
    -SourcePath (Join-Path $kit 'Compliance\OdfSchemaPatternValidator.Attributes.Validation.Candidates.cs') `
    -RegionMarker '#region Attribute Patterns - Candidate Matching'

# 3. OdfBouncyCastleOpenPgpProvider: SessionKeyPayload -> core
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Core\OdfBouncyCastleOpenPgpProvider.cs') `
    -SourcePath (Join-Path $kit 'Core\OdfBouncyCastleOpenPgpProvider.SessionKeyPayload.cs') `
    -RegionMarker '#region Session Key Payload'

# 4. OdfDocument: ViewSettings -> core
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Core\OdfDocument.cs') `
    -SourcePath (Join-Path $kit 'Core\OdfDocument.ViewSettings.cs') `
    -RegionMarker '#region Zoom & View Settings'

# 5. OdfDocument: MergingInternals -> Merging
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Core\OdfDocument.Merging.cs') `
    -SourcePath (Join-Path $kit 'Core\OdfDocument.MergingInternals.cs') `
    -RegionMarker '#region Internal Merging Helpers'

# 6. OdfDocument: Statistics -> Metadata
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Core\OdfDocument.Metadata.cs') `
    -SourcePath (Join-Path $kit 'Core\OdfDocument.Statistics.cs') `
    -RegionMarker '#region Statistics & Document Structure Diagnostics'

# 7. OdfElement: Clone -> core
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'DOM\OdfElement.cs') `
    -SourcePath (Join-Path $kit 'DOM\OdfElement.Clone.cs') `
    -RegionMarker '#region Clone'

# 8. OdfDocument: Streaming -> Lifecycle（儲存與串流 API 同屬生命週期）
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Core\OdfDocument.Lifecycle.cs') `
    -SourcePath (Join-Path $kit 'Core\OdfDocument.Streaming.cs') `
    -RegionMarker '#region Web Streaming APIs'

Write-Host 'Done.'