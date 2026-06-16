#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$formulaDir = Join-Path $PSScriptRoot '..\OdfKit\Formula'
$sourcePath = Join-Path $formulaDir 'DefaultFormulaEvaluator.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$regionMap = [ordered]@{
    'Logical Functions'                = 'DefaultFormulaEvaluator.Logical.cs'
    'String Functions'                 = 'DefaultFormulaEvaluator.String.cs'
    'Statistical Functions'            = 'DefaultFormulaEvaluator.Statistical.cs'
    'Lookup Functions'                 = 'DefaultFormulaEvaluator.Lookup.cs'
    'Math Functions'                   = 'DefaultFormulaEvaluator.Math.cs'
    'Additional String Functions'      = 'DefaultFormulaEvaluator.StringAdditional.cs'
    'Additional Statistical Functions' = 'DefaultFormulaEvaluator.StatisticalAdditional.cs'
    'Database and Financial Functions' = 'DefaultFormulaEvaluator.DatabaseFinancial.cs'
    'Date and Time Functions'          = 'DefaultFormulaEvaluator.DateTime.cs'
    'Matrix Functions'                 = 'DefaultFormulaEvaluator.Matrix.cs'
    'Helpers & Coercion'               = 'DefaultFormulaEvaluator.Helpers.cs'
}

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'OdfNode|OdfNamespaces|OdfPackage') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'AstNode|IOdfFormulaEvaluator|OdfFormula') { [void]$needed.Add('OdfKit.Formula.AST') }
    if ($Text -match 'OdfTableSheet|SpreadsheetDocument|OdfCell') { [void]$needed.Add('OdfKit.Spreadsheet') }

    $order = @(
        'System', 'System.Collections.Generic',
        'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Formula.AST', 'OdfKit.Spreadsheet'
    )
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-FileWithUsings {
    param(
        [string]$Path,
        [string[]]$BodyLines,
        [switch]$SkipUsings
    )
    $out = [System.Collections.Generic.List[string]]::new()
    if (-not $SkipUsings) {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) {
            $out.Add($usingLine)
        }
        $out.Add('')
    }
    $out.AddRange($BodyLines)
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$classStart = 0
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^public class DefaultFormulaEvaluator') { $classStart = $i; break }
}
$coreEnd = 0
for ($i = $classStart; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\s*#region ') { $coreEnd = $i - 1; break }
}

$preamble = $lines[0..($classStart - 1)]
$classCore = $lines[$classStart..$coreEnd] | ForEach-Object {
    $_ -replace '^public class DefaultFormulaEvaluator', 'public partial class DefaultFormulaEvaluator'
}
$classCore += '}'
$core = $preamble + $classCore
Write-FileWithUsings -Path $sourcePath -BodyLines $core -SkipUsings
Write-Host "Core DefaultFormulaEvaluator.cs: $($core.Count) lines"

$classEnd = 0
for ($i = $coreEnd + 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^}$' -and $i + 1 -lt $lines.Count -and $lines[$i + 1] -match '^$|^internal class') {
        $classEnd = $i
        break
    }
}
if ($classEnd -eq 0) {
    for ($i = $lines.Count - 1; $i -ge $coreEnd; $i--) {
        if ($lines[$i] -eq '}') { $classEnd = $i; break }
    }
}

$classBody = $lines[($coreEnd + 1)..($classEnd - 1)]
$currentRegion = $null
$currentLines = [System.Collections.Generic.List[string]]::new()

function Flush-Region {
    if ($null -eq $script:currentRegion) { return }
    $fileName = $regionMap[$script:currentRegion]
    if (-not $fileName) { throw "Unknown region: $($script:currentRegion)" }
    $body = @(
        'namespace OdfKit.Formula;',
        '',
        'public partial class DefaultFormulaEvaluator',
        '{',
        "    #region $($script:currentRegion)",
        ''
    )
    $body += $script:currentLines
    $body += @('', "    #endregion", '}')
    Write-FileWithUsings -Path (Join-Path $formulaDir $fileName) -BodyLines $body
    Write-Host "  $fileName : $($script:currentLines.Count) body lines"
    $script:currentLines.Clear()
}

foreach ($line in $classBody) {
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
    if ($null -ne $currentRegion) {
        $currentLines.Add($line)
    }
}
Flush-Region

Write-Host 'Done.'