#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$sheetDir = Join-Path $PSScriptRoot '..\OdfKit\Spreadsheet'
$sourcePath = Join-Path $sheetDir 'SpreadsheetDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$tableSheetRegionMap = [ordered]@{
    '列印設定' = 'OdfTableSheet.PrintSettings.cs'
    '列欄群組' = 'OdfTableSheet.RowColumnGroups.cs'
    '圖表'     = 'OdfTableSheet.Charts.cs'
}

$nestedTypeFiles = @(
    @{ Start = 739; End = 833; File = 'OdfWorksheetCollection.cs' }
    @{ Start = 2785; File = 'OdfFrozenPanes.cs' }
    @{ Start = 2833; File = 'OdfNamedRangeInfo.cs' }
    @{ Start = 2857; File = 'OdfNamedExpressionInfo.cs' }
    @{ Start = 2881; File = 'OdfRowCollection.cs' }
    @{ Start = 2905; File = 'OdfSheetRow.cs' }
    @{ Start = 2961; File = 'OdfRowCellCollection.cs' }
    @{ Start = 2988; File = 'OdfColumnCollection.cs' }
    @{ Start = 3012; File = 'OdfSheetColumn.cs' }
    @{ Start = 3062; File = 'OdfRangeCollection.cs' }
    @{ Start = 3097; File = 'OdfCellRangeSelection.cs' }
    @{ Start = 3295; File = 'OdfRichTextRun.cs' }
    @{ Start = 3314; File = 'OdfRichText.cs' }
    @{ Start = 3340; File = 'OdfCellAnnotation.cs' }
    @{ Start = 3355; File = 'OdfCellCollection.cs' }
    @{ Start = 3387; File = 'OdfCell.cs' }
    @{ Start = 4017; File = 'OdfIconSetType.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'Regex|MatchCollection') { [void]$needed.Add('System.Text.RegularExpressions') }
    if ($Text -match 'Task\.|CancellationToken') {
        [void]$needed.Add('System.Threading')
        [void]$needed.Add('System.Threading.Tasks')
    }
    if ($Text -match 'Stream|MemoryStream|File\.|Path\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'IEnumerable|ICollection|IEnumerator') { [void]$needed.Add('System.Collections') }
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'XDocument|XElement|XAttribute') { [void]$needed.Add('System.Xml.Linq') }
    if ($Text -match 'Color\.|System\.Drawing|PointF') { [void]$needed.Add('System.Drawing') }
    if ($Text -match 'CultureInfo|DateTimeFormat') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'OdfCompliance|OdfValidator|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfPackage|OdfDocument|OdfNode|OdfNamespaces|OdfDocumentFactory|OdfDocumentKind') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfChart|ChartDocument') { [void]$needed.Add('OdfKit.Chart') }
    if ($Text -match 'OdfLength|OdfStyle|StyleEngine|OdfWritingMode|OdfColor|OdfBorder') { [void]$needed.Add('OdfKit.Styles') }

    $order = @(
        'System', 'System.Collections', 'System.Collections.Generic', 'System.Drawing', 'System.Globalization',
        'System.IO', 'System.Text.RegularExpressions', 'System.Threading', 'System.Threading.Tasks', 'System.Xml.Linq',
        'OdfKit.Chart', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles'
    )
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-FileWithUsings {
    param(
        [string]$Path,
        [string[]]$BodyLines,
        [switch]$SkipUsings
    )
    $out = [System.Collections.Generic.List[string]]::new()
    if (-not $SkipUsings) {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) {
            $out.Add($usingLine)
        }
        $out.Add('')
    }
    $out.AddRange($BodyLines)
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

# --- SpreadsheetDocument core (lines 1..737) ---
$core = $lines[0..736] | ForEach-Object {
    $_ -replace '^public class SpreadsheetDocument', 'public partial class SpreadsheetDocument'
}
Write-FileWithUsings -Path $sourcePath -BodyLines $core -SkipUsings
Write-Host "Core SpreadsheetDocument.cs: $($core.Count) lines"

# --- OdfTableSheet partial split (lines 835..2783) ---
$tableSheetLines = $lines[834..2782]
$coreEndRel = 0
for ($i = 0; $i -lt $tableSheetLines.Count; $i++) {
    if ($tableSheetLines[$i] -match '^\s*#region ') { $coreEndRel = $i - 1; break }
}
$tableCore = $tableSheetLines[0..$coreEndRel] | ForEach-Object {
    $_ -replace '^public class OdfTableSheet', 'public partial class OdfTableSheet'
}
$tableCore += '}'
$body = @('namespace OdfKit.Spreadsheet;', '') + $tableCore
Write-FileWithUsings -Path (Join-Path $sheetDir 'OdfTableSheet.cs') -BodyLines $body
Write-Host "Core OdfTableSheet.cs: $($tableCore.Count) lines"

$classBody = $tableSheetLines[($coreEndRel + 1)..($tableSheetLines.Count - 2)]
$currentRegion = $null
$currentLines = [System.Collections.Generic.List[string]]::new()

function Flush-TableSheetRegion {
    if ($null -eq $script:currentRegion) { return }
    $fileName = $tableSheetRegionMap[$script:currentRegion]
    if (-not $fileName) { throw "Unknown OdfTableSheet region: $($script:currentRegion)" }
    $regionBody = @(
        'namespace OdfKit.Spreadsheet;',
        '',
        'public partial class OdfTableSheet',
        '{',
        "    #region $($script:currentRegion)",
        ''
    )
    $regionBody += $script:currentLines
    $regionBody += @('', "    #endregion", '}')
    Write-FileWithUsings -Path (Join-Path $sheetDir $fileName) -BodyLines $regionBody
    Write-Host "  $fileName : $($script:currentLines.Count) body lines"
    $script:currentLines.Clear()
}

foreach ($line in $classBody) {
    if ($line -match '^\s*#region\s+(.+)$') {
        Flush-TableSheetRegion
        $currentRegion = $Matches[1].Trim()
        continue
    }
    if ($line -match '^\s*#endregion') {
        Flush-TableSheetRegion
        $currentRegion = $null
        continue
    }
    if ($null -ne $currentRegion) {
        $currentLines.Add($line)
    }
}
Flush-TableSheetRegion

# --- Other nested types ---
for ($i = 0; $i -lt $nestedTypeFiles.Count; $i++) {
    $start = $nestedTypeFiles[$i].Start - 1
    $end = if ($null -ne $nestedTypeFiles[$i].End) {
        $nestedTypeFiles[$i].End - 1
    }
    elseif ($i + 1 -lt $nestedTypeFiles.Count) {
        $nestedTypeFiles[$i + 1].Start - 2
    }
    else {
        $lines.Count - 1
    }
    $fileName = $nestedTypeFiles[$i].File
    $block = $lines[$start..$end]
    while ($block.Count -gt 0 -and [string]::IsNullOrWhiteSpace($block[-1])) {
        $block = $block[0..($block.Count - 2)]
    }
    $nestedBody = @('namespace OdfKit.Spreadsheet;', '') + $block
    Write-FileWithUsings -Path (Join-Path $sheetDir $fileName) -BodyLines $nestedBody
    Write-Host "Nested $fileName : $($block.Count) lines"
}

Write-Host 'Done.'