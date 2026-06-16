#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfTypedDomCoverage.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$files = @(
    @{ Start = 17; End = 215; File = 'OdfTypedDomCoverage.cs'; Region = $null; IsCore = $true }
    @{ Start = 217; End = ($lineCount - 2); File = 'OdfTypedDomCoverage.PropertyTypes.cs'; Region = 'Property Type Resolution' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'CultureInfo|InvariantCulture') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'Enumerable\.|\.Select\(|\.OrderBy\(') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'PropertyInfo|BindingFlags') { [void]$needed.Add('System.Reflection') }
    if ($Text -match 'OdfCompliance|OdfVersion|OdfSchema') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfPackage|OdfDocument|OdfNamespaces|OdfNodeFactory') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfElement|OdfNode') { [void]$needed.Add('OdfKit.DOM') }
    if ($Text -match 'OdfLength|OdfStyle|OdfTableDirection|OdfMediaType') { [void]$needed.Add('OdfKit.Styles') }

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

function Write-CoreFile {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines[0..14]) {
        if ($line -eq 'public static class OdfTypedDomCoverage') {
            $out.Add('public static partial class OdfTypedDomCoverage')
            $out.Add('{')
        }
        else {
            $out.Add($line)
        }
    }
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.DOM;')
    $out.Add('')
    $out.Add('public static partial class OdfTypedDomCoverage')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $files) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $domDir $block.File
    if ($block.IsCore) {
        Write-CoreFile -Path $path -BodyLines $body
        Write-Host "Core $($block.File): $($body.Count) body lines"
    }
    else {
        Write-PartialFile -Path $path -RegionName $block.Region -BodyLines $body
        Write-Host "  $($block.File): $($body.Count) body lines"
    }
}

Write-Host 'Done.'