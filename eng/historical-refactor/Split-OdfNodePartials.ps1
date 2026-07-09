#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfNode.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$regionMap = [ordered]@{
    'DOM Tree Manipulation' = 'OdfNode.Tree.cs'
    'Attributes Helper'     = 'OdfNode.Attributes.cs'
    'Clone & Import Node'   = 'OdfNode.Clone.cs'
}

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }
    if ($Text -match 'XElement|XAttribute|XDocument|XNamespace') { [void]$needed.Add('System.Xml.Linq') }
    if ($Text -match 'OdfCompliance|OdfVersion|OdfSchema') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfPackage|OdfDocument|OdfNamespaces') { [void]$needed.Add('OdfKit.Core') }

    $order = @('System', 'System.Collections.Generic', 'System.Text', 'System.Xml.Linq', 'OdfKit.Compliance', 'OdfKit.Core')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-NodeTypeFile {
    param([string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $out.Add('using System;')
    $out.Add('using System.Collections.Generic;')
    $out.Add('')
    $out.Add('namespace OdfKit.DOM;')
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path (Join-Path $domDir 'OdfNodeType.cs') -Value $out -Encoding UTF8
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.DOM;')
    $out.Add('')
    $out.Add('public partial class OdfNode')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$nodeTypeBody = $lines[7..79]
Write-NodeTypeFile -BodyLines $nodeTypeBody
Write-Host "  OdfNodeType.cs: $($nodeTypeBody.Count) body lines"

$classLines = $lines[80..($lines.Count - 1)]
$coreEnd = 0
$lastRegionEnd = 0
for ($i = 0; $i -lt $classLines.Count; $i++) {
    if ($classLines[$i] -match '^\s*#region ' -and $coreEnd -eq 0) { $coreEnd = $i - 1 }
    if ($classLines[$i] -match '^\s*#endregion') { $lastRegionEnd = $i }
}

$header = $lines[0..5]
$coreBody = $classLines[0..$coreEnd] | ForEach-Object {
    $_ -replace '^public class OdfNode', 'public partial class OdfNode'
}
$interRegionLines = [System.Collections.Generic.List[string]]::new()
$inRegion = $false
for ($i = $coreEnd + 1; $i -le $lastRegionEnd; $i++) {
    $line = $classLines[$i]
    if ($line -match '^\s*#region\s+') { $inRegion = $true; continue }
    if ($line -match '^\s*#endregion') { $inRegion = $false; continue }
    if (-not $inRegion) { $interRegionLines.Add($line) }
}

$core = $header + $coreBody + $interRegionLines
if ($lastRegionEnd -gt 0 -and $lastRegionEnd + 1 -lt $classLines.Count) {
    $core += $classLines[($lastRegionEnd + 1)..($classLines.Count - 1)]
}
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfNode.cs: $($core.Count) lines"

$regionBody = $classLines[($coreEnd + 1)..$lastRegionEnd]
$currentRegion = $null
$currentLines = [System.Collections.Generic.List[string]]::new()

function Flush-Region {
    if ($null -eq $script:currentRegion) { return }
    $fileName = $regionMap[$script:currentRegion]
    if (-not $fileName) { throw "Unknown region: $($script:currentRegion)" }
    Write-PartialFile -Path (Join-Path $domDir $fileName) -RegionName $script:currentRegion -BodyLines $script:currentLines
    Write-Host "  $fileName : $($script:currentLines.Count) body lines"
    $script:currentLines.Clear()
}

foreach ($line in $regionBody) {
    if ($line -match '^\s*#region\s+(.+)$') {
        Flush-Region
        $currentRegion = $Matches[1].Trim()
        continue
    }
    if ($line -match '^\s*#endregion') {
        Flush-Region
        $currentRegion = $null
        continue
    }
    if ($null -ne $currentRegion) { $currentLines.Add($line) }
}
Flush-Region
Write-Host 'Done.'