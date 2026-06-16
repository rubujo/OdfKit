#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$imageDir = Join-Path $PSScriptRoot '..\OdfKit\Image'
$sourcePath = Join-Path $imageDir 'OdfImageDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    [void]$needed.Add('OdfKit.Styles')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'Stream|MemoryStream|File\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'OdfCompliance|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }

    $order = @('System', 'System.Collections.Generic', 'System.IO', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles')
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
    $out.Add('namespace OdfKit.Image;')
    $out.Add('')
    $out.Add('public partial class OdfImageDocument')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

function Write-TypeFile {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $out.Add('using System;')
    $out.Add('')
    $out.Add('namespace OdfKit.Image;')
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$infoBody = $lines[455..($lineCount - 1)]
Write-TypeFile -Path (Join-Path $imageDir 'OdfImageInfo.cs') -BodyLines $infoBody
Write-Host "  OdfImageInfo.cs: $($infoBody.Count) body lines"

$infraBody = $lines[263..452]
Write-PartialFile -Path (Join-Path $imageDir 'OdfImageDocument.Infrastructure.cs') -RegionName 'Image Document Infrastructure' -BodyLines $infraBody
Write-Host "  OdfImageDocument.Infrastructure.cs: $($infraBody.Count) body lines"

$core = $lines[0..261] | ForEach-Object {
    $_ -replace '^public class OdfImageDocument', 'public partial class OdfImageDocument'
}
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfImageDocument.cs: $($core.Count) lines"
Write-Host 'Done.'