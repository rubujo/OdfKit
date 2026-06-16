#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfTypedDomCoverage.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$lineCount = $lines.Count
$typeBlocks = @(
    @{ Start = 12; End = 844; File = 'OdfTypedDomCoverage.cs'; IsCore = $true }
    @{ Start = 845; End = 1002; File = 'OdfTypedDomCoverageReport.cs' }
    @{ Start = 1003; End = 1055; File = 'OdfTypedDomChildElementRelationCoverage.cs' }
    @{ Start = 1056; End = $lineCount; File = 'OdfTypedDomElementCoverage.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<|IReadOnlyDictionary<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'CultureInfo|InvariantCulture') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'Enumerable\.|\.Select\(|\.OrderBy\(|\.GroupBy\(|\.Sum\(|\.Count\(') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'PropertyInfo|MethodInfo|Assembly|BindingFlags') { [void]$needed.Add('System.Reflection') }
    if ($Text -match 'OdfCompliance|OdfVersion|OdfSchema') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfPackage|OdfDocument|OdfNamespaces') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfElement|OdfNode|OdfNodeFactory') { [void]$needed.Add('OdfKit.DOM') }
    if ($Text -match 'OdfLength|OdfStyle') { [void]$needed.Add('OdfKit.Styles') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.Globalization', 'System.Linq', 'System.Reflection',
        'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles'
    )
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-TypeFile {
    param(
        [string]$Path,
        [string[]]$BodyLines,
        [switch]$IsCore
    )
    $out = [System.Collections.Generic.List[string]]::new()
    if ($IsCore) {
        foreach ($line in $lines[0..9]) { $out.Add($line) }
        $out.Add('')
    }
    else {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
        $out.Add('')
        $out.Add('namespace OdfKit.DOM;')
        $out.Add('')
    }
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $domDir $block.File
    if ($block.IsCore) {
        Write-TypeFile -Path $path -BodyLines $body -IsCore
        Write-Host "Core $($block.File): $($body.Count) body lines"
    }
    else {
        Write-TypeFile -Path $path -BodyLines $body
        Write-Host "  $($block.File): $($body.Count) body lines"
    }
}

Write-Host 'Done.'