#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$stylesDir = Join-Path $PSScriptRoot '..\OdfKit\Styles'
$sourcePath = Join-Path $stylesDir 'OdfFontResolver.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

function Write-TypeFile {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $out.Add('using System;')
    $out.Add('using System.Collections.Generic;')
    $out.Add('using System.IO;')
    $out.Add('using System.Text;')
    $out.Add('')
    $out.Add('namespace OdfKit.Styles;')
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$ttfBody = $lines[279..416] | ForEach-Object {
    $_ -replace 'internal static class TtfReader', 'internal static class TtfFontNameReader'
}
Write-TypeFile -Path (Join-Path $stylesDir 'TtfFontNameReader.cs') -BodyLines $ttfBody
Write-Host "  TtfFontNameReader.cs: $($ttfBody.Count) body lines"

$core = $lines[0..275]
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfFontResolver.cs: $($core.Count) lines"
Write-Host 'Done.'