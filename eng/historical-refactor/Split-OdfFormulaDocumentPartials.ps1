#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$formulaDir = Join-Path $PSScriptRoot '..\OdfKit\Formula'
$sourcePath = Join-Path $formulaDir 'OdfFormulaDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$typeBlocks = @(
    @{ Start = 340; End = 364; File = 'OdfMathTokenKind.cs' }
    @{ Start = 366; End = 410; File = 'OdfMathToken.cs' }
)

function Write-TypeFile {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $out.Add('using System;')
    $out.Add('')
    $out.Add('namespace OdfKit.Formula;')
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('System.Collections.Generic')
    [void]$needed.Add('OdfKit.Compliance')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')

    $order = @('System', 'System.Collections.Generic', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM')
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
    $out.Add('namespace OdfKit.Formula;')
    $out.Add('')
    $out.Add('public partial class OdfFormulaDocument')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-TypeFile -Path (Join-Path $formulaDir $block.File) -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

$infraBody = $lines[194..336]
Write-PartialFile -Path (Join-Path $formulaDir 'OdfFormulaDocument.Infrastructure.cs') -RegionName 'Formula Document Infrastructure' -BodyLines $infraBody
Write-Host "  OdfFormulaDocument.Infrastructure.cs: $($infraBody.Count) body lines"

$core = $lines[0..192] | ForEach-Object {
    $_ -replace '^public class OdfFormulaDocument', 'public partial class OdfFormulaDocument'
}
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfFormulaDocument.cs: $($core.Count) lines"
Write-Host 'Done.'