#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$sheetDir = Join-Path $PSScriptRoot '..\OdfKit\Spreadsheet'
$sourcePath = Join-Path $sheetDir 'SpreadsheetDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$blocks = @(
    @{ Start = 151; End = 373; File = 'SpreadsheetDocument.Workbook.cs'; Region = 'Workbook & Sheet Management' }
    @{ Start = 375; End = ($lineCount - 1); File = 'SpreadsheetDocument.Features.cs'; Region = 'Named Ranges, Charts & Validation' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'Stream|MemoryStream|File\.|Path\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'IEnumerable|ICollection') { [void]$needed.Add('System.Collections') }
    if ($Text -match 'Color\.|System\.Drawing|OdfColor') { [void]$needed.Add('System.Drawing') }
    if ($Text -match 'CultureInfo') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'CancellationToken') { [void]$needed.Add('System.Threading') }
    if ($Text -match 'Task\.') { [void]$needed.Add('System.Threading.Tasks') }
    if ($Text -match 'Cryptography|SHA256') { [void]$needed.Add('System.Security.Cryptography') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }
    if ($Text -match 'OdfCompliance|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfChart|ChartDefinition') { [void]$needed.Add('OdfKit.Chart') }
    if ($Text -match 'OdfEncryption|OdfPackage|OdfDocument|OdfNode|OdfNamespaces|OdfVersionInfo|OdfMergeOptions') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfLength|OdfStyle|StyleEngine|OdfColor') { [void]$needed.Add('OdfKit.Styles') }

    $order = @(
        'System', 'System.Collections', 'System.Collections.Generic', 'System.Drawing', 'System.Globalization',
        'System.IO', 'System.Security.Cryptography', 'System.Text', 'System.Threading', 'System.Threading.Tasks',
        'OdfKit.Chart', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles'
    )
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Spreadsheet;')
    $out.Add('')
    $out.Add('public partial class SpreadsheetDocument')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$core = $lines[0..149]
$core[-1] = $core[-1]  # ensure last line kept
# Trim core to end before first split block - lines 0..149 (1-indexed: 1-150)
$core = $lines[0..149]
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core SpreadsheetDocument.cs: $($core.Count) lines"

foreach ($block in $blocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $sheetDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Write-Host 'Done.'