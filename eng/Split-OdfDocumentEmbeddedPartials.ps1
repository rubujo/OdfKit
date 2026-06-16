#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('System.IO')
    [void]$needed.Add('System.Text')
    [void]$needed.Add('System.Threading')
    [void]$needed.Add('System.Threading.Tasks')
    [void]$needed.Add('OdfKit.Compliance')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    [void]$needed.Add('OdfKit.Styles')

    $order = @('System', 'System.IO', 'System.Text', 'System.Threading', 'System.Threading.Tasks', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    foreach ($usingLine in (Get-UsingsForBlock -Text ($BodyLines -join "`n"))) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Core;')
    $out.Add('')
    $out.Add('public abstract partial class OdfDocument')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$embeddedBody = $lines[370..448]
Write-PartialFile -Path (Join-Path $coreDir 'OdfDocument.Embedded.cs') -RegionName 'Embedded Documents' -BodyLines $embeddedBody
Write-Host "  OdfDocument.Embedded.cs: $($embeddedBody.Count) body lines"

$core = $lines[0..368] + '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfDocument.cs: $($core.Count) lines"
Write-Host 'Done.'