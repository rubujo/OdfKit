#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$complianceDir = Join-Path $PSScriptRoot '..\OdfKit\Compliance'
$sourcePath = Join-Path $complianceDir 'OdfValidationReport.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$typeBlocks = @(
    @{ Start = 9; End = 260; File = 'OdfValidationReport.cs'; IsCore = $true }
    @{ Start = 262; End = 367; File = 'OdfValidationIssue.cs' }
    @{ Start = 369; End = 452; File = 'OdfValidationReportJsonModel.cs' }
    @{ Start = 454; End = $lineCount; File = 'OdfValidationIssueJsonModel.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<|IEnumerable<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match '\.Linq|\.Any\(|\.All\(|\.Select\(|\.Count\(') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'StringBuilder') { [void]$needed.Add('System.Text') }

    $order = @('System', 'System.Collections.Generic', 'System.Linq', 'System.Text')
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
        $out.Add('#pragma warning restore CS1591')
        $out.Add('using System;')
        $out.Add('using System.Collections.Generic;')
        $out.Add('using System.Linq;')
        $out.Add('using System.Text;')
        $out.Add('')
        $out.Add('namespace OdfKit.Compliance;')
        $out.Add('')
    }
    else {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
        $out.Add('')
        $out.Add('namespace OdfKit.Compliance;')
        $out.Add('')
    }
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $complianceDir $block.File
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