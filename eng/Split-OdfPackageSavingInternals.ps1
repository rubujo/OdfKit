#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfPackage.Saving.Internals.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('System.IO')
    [void]$needed.Add('System.IO.Compression')
    [void]$needed.Add('System.Text')
    [void]$needed.Add('System.Xml')
    [void]$needed.Add('System.Xml.Linq')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match '\.Linq|\.Any\(|\.All\(|\.Select\(|\.Count\(|\.FirstOrDefault\(|\.Where\(') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'OdfStyleEngine|OdfFontResolver') { [void]$needed.Add('OdfKit.Styles') }
    if ($Text -match 'DefaultFormulaEvaluator|OdfFormula') { [void]$needed.Add('OdfKit.Formula') }

    $order = @('System', 'System.IO', 'System.IO.Compression', 'System.Linq', 'System.Text', 'System.Xml', 'System.Xml.Linq', 'OdfKit.DOM', 'OdfKit.Formula', 'OdfKit.Styles')
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
    $out.Add('namespace OdfKit.Core;')
    $out.Add('')
    $out.Add('public sealed partial class OdfPackage')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$hooksBody = $lines[17..129]
$archiveBody = $lines[131..426]

Write-PartialFile -Path $sourcePath -RegionName 'Saving and Atomic Save - Internals' -BodyLines $hooksBody
Write-Host "  OdfPackage.Saving.Internals.cs: $($hooksBody.Count) body lines"

Write-PartialFile -Path (Join-Path $coreDir 'OdfPackage.Saving.ArchiveWriting.cs') -RegionName 'Archive and Flat XML Writing' -BodyLines $archiveBody
Write-Host "  OdfPackage.Saving.ArchiveWriting.cs: $($archiveBody.Count) body lines"
Write-Host 'Done.'