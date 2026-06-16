#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$sheetDir = Join-Path $PSScriptRoot '..\OdfKit\Spreadsheet'
$sourcePath = Join-Path $sheetDir 'OdfTableSheet.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$sectionMap = [ordered]@{
    'Layout'             = @{ Start = 289; File = 'OdfTableSheet.Layout.cs' }
    'ConditionalFormats' = @{ Start = 410; File = 'OdfTableSheet.ConditionalFormats.cs' }
    'View'               = @{ Start = 662; File = 'OdfTableSheet.View.cs' }
    'Internals'          = @{ Start = 859; File = 'OdfTableSheet.Internals.cs' }
    'Visibility'         = @{ Start = 1275; File = 'OdfTableSheet.Visibility.cs' }
}

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'IEnumerable|ICollection|IEnumerator') { [void]$needed.Add('System.Collections') }
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'CultureInfo') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'OdfNode|OdfNamespaces') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfLength|OdfStyle|OdfBorder') { [void]$needed.Add('OdfKit.Styles') }
    if ($Text -match 'OdfChart|Sparkline') { [void]$needed.Add('OdfKit.Chart') }

    $order = @(
        'System', 'System.Collections', 'System.Collections.Generic', 'System.Globalization',
        'OdfKit.Chart', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles'
    )
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-FileWithUsings {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.AddRange($BodyLines)
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$coreEnd = 286
$core = $lines[0..$coreEnd] + '}'
Write-FileWithUsings -Path $sourcePath -BodyLines $core -SkipUsings
Write-Host "Core OdfTableSheet.cs: $($core.Count) lines"

$keys = @($sectionMap.Keys)
for ($i = 0; $i -lt $keys.Count; $i++) {
    $name = $keys[$i]
    $start = $sectionMap[$name].Start - 1
    $end = if ($i + 1 -lt $keys.Count) { $sectionMap[$keys[$i + 1]].Start - 2 } else { $lines.Count - 2 }
    $fileName = $sectionMap[$name].File
    $block = $lines[$start..$end]
    $body = @(
        'namespace OdfKit.Spreadsheet;',
        '',
        'public partial class OdfTableSheet',
        '{',
        "    #region $name",
        ''
    ) + $block + @('', '    #endregion', '}')
    Write-FileWithUsings -Path (Join-Path $sheetDir $fileName) -BodyLines $body
    Write-Host "  $fileName : $($block.Count) body lines"
}

Write-Host 'Done.'