#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$astDir = Join-Path $PSScriptRoot '..\OdfKit\Formula\AST'
$sourcePath = Join-Path $astDir 'AstNode.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$typeBlocks = @(
    @{ Start = 7; End = 42; File = 'AstNode.cs'; IsCore = $true }
    @{ Start = 44; End = 189; File = 'AstNode.ValueAndReferenceNodes.cs' }
    @{ Start = 191; End = 340; File = 'AstNode.OperatorNodes.cs' }
    @{ Start = 342; End = 425; File = 'AstNode.CallAndExpressionNodes.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Spreadsheet')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'OdfFormulaError|DefaultFormulaEvaluator') { [void]$needed.Add('OdfKit.Formula') }

    $order = @('System', 'System.Collections.Generic', 'OdfKit.Formula', 'OdfKit.Spreadsheet')
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
        $out.Add('using System;')
        $out.Add('using System.Collections.Generic;')
        $out.Add('using OdfKit.Spreadsheet;')
        $out.Add('')
        $out.Add('namespace OdfKit.Formula.AST;')
        $out.Add('')
    }
    else {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
        $out.Add('')
        $out.Add('namespace OdfKit.Formula.AST;')
        $out.Add('')
    }
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $astDir $block.File
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