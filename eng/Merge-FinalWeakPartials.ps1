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
        Write-Host "Skip missing: $([IO.Path]::GetFileName($SourcePath))"
        return
    }

    $coreLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $CorePath -Encoding UTF8)
    $sourceLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $SourcePath -Encoding UTF8)
    $block = Get-RegionBlock -Lines $sourceLines -RegionMarker $RegionMarker

    if ($coreLines[$coreLines.Count - 1] -ne '}') {
        throw "Expected $([IO.Path]::GetFileName($CorePath)) to end with }"
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

function Merge-AllRegionsIntoCore {
    param(
        [string]$CorePath,
        [string[]]$SourcePaths
    )
    foreach ($sourcePath in $SourcePaths) {
        if (-not (Test-Path -LiteralPath $sourcePath)) { continue }
        $sourceLines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $sourcePath -Encoding UTF8)
        $regionStart = -1
        for ($i = 0; $i -lt $sourceLines.Count; $i++) {
            if ($sourceLines[$i] -match '#region') {
                $regionStart = $i
                break
            }
        }
        if ($regionStart -lt 0) { throw "No #region in $([IO.Path]::GetFileName($sourcePath))" }
        $marker = $sourceLines[$regionStart].Trim()
        Merge-RegionIntoCore -CorePath $CorePath -SourcePath $sourcePath -RegionMarker $marker
    }
}

Write-Host '=== TextDocument small partials -> core ==='
$textDir = Join-Path $kit 'Text'
$coreText = Join-Path $textDir 'TextDocument.cs'
$textSmall = @(
    'TextDocument.TableCoveredCells.cs'
    'TextDocument.CjkFontFallback.cs'
    'TextDocument.FieldIndicators.cs'
    'TextDocument.Merge.cs'
    'TextDocument.Toc.cs'
    'TextDocument.MathFormula.cs'
    'TextDocument.PageSetup.cs'
    'TextDocument.Sections.cs'
    'TextDocument.Comments.cs'
    'TextDocument.XmlHelpers.cs'
    'TextDocument.MailMerge.cs'
) | ForEach-Object { Join-Path $textDir $_ }
Merge-AllRegionsIntoCore -CorePath $coreText -SourcePaths $textSmall

Write-Host '=== OdfPackage weak partials ==='
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Core\OdfPackage.PublicApi.cs') `
    -SourcePath (Join-Path $kit 'Core\OdfPackage.EmbeddedObjects.cs') `
    -RegionMarker '#region Embedded Objects Extraction'
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Core\OdfPackage.MacroSanitize.cs') `
    -SourcePath (Join-Path $kit 'Core\OdfPackage.ZipSanitize.cs') `
    -RegionMarker '#region ZIP Path & Entry Sanitize (Zip Slip Protection)'
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Core\OdfPackage.cs') `
    -SourcePath (Join-Path $kit 'Core\OdfPackage.Factory.cs') `
    -RegionMarker '#region Factory Methods'

Write-Host '=== DefaultFormulaEvaluator weak partials ==='
$formulaDir = Join-Path $kit 'Formula'
Merge-RegionIntoCore `
    -CorePath (Join-Path $formulaDir 'DefaultFormulaEvaluator.cs') `
    -SourcePath (Join-Path $formulaDir 'DefaultFormulaEvaluator.Matrix.cs') `
    -RegionMarker '#region Matrix Functions'
Merge-RegionIntoCore `
    -CorePath (Join-Path $formulaDir 'DefaultFormulaEvaluator.Statistical.cs') `
    -SourcePath (Join-Path $formulaDir 'DefaultFormulaEvaluator.StatisticalAdditional.cs') `
    -RegionMarker '#region Additional Statistical Functions'
Merge-RegionIntoCore `
    -CorePath (Join-Path $formulaDir 'DefaultFormulaEvaluator.cs') `
    -SourcePath (Join-Path $formulaDir 'DefaultFormulaEvaluator.Lookup.cs') `
    -RegionMarker '#region Lookup Functions'

Write-Host '=== PresentationDocument / PackageValidator / OdfSigner ==='
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Presentation\PresentationDocument.Slides.cs') `
    -SourcePath (Join-Path $kit 'Presentation\PresentationDocument.Transitions.cs') `
    -RegionMarker '#region Slide Transitions'
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Compliance\OdfPackageValidator.cs') `
    -SourcePath (Join-Path $kit 'Compliance\OdfPackageValidator.Profile.cs') `
    -RegionMarker '#region Profile Extension & Version'
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Core\OdfSigner.Verification.Dsig.cs') `
    -SourcePath (Join-Path $kit 'Core\OdfSigner.Verification.Single.cs') `
    -RegionMarker '#region Verification - Single Signature'
Merge-RegionIntoCore `
    -CorePath (Join-Path $kit 'Core\OdfSigner.Verification.Dsig.cs') `
    -SourcePath (Join-Path $kit 'Core\OdfSigner.Verification.Entry.cs') `
    -RegionMarker '#region Verification'

Write-Host 'Done.'