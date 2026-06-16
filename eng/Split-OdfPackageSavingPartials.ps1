#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfPackage.Saving.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$splitLine = 543

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'Task\.|CancellationToken') {
        [void]$needed.Add('System.Threading')
        [void]$needed.Add('System.Threading.Tasks')
    }
    if ($Text -match 'Stream|MemoryStream|File\.|Path\.|ZipArchive') { [void]$needed.Add('System.IO') }
    if ($Text -match 'ZipArchive|Compression') { [void]$needed.Add('System.IO.Compression') }
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match '\.Linq|Enumerable\.') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }
    if ($Text -match 'XmlReader|XmlWriter|XmlDocument') { [void]$needed.Add('System.Xml') }
    if ($Text -match 'CultureInfo') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'OdfCompliance|OdfValidator|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'DefaultFormulaEvaluator|IOdfFormulaEvaluator') { [void]$needed.Add('OdfKit.Formula') }
    if ($Text -match 'OdfLength|OdfStyle|StyleEngine|OdfFontResolver') { [void]$needed.Add('OdfKit.Styles') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.Globalization', 'System.IO', 'System.IO.Compression',
        'System.Linq', 'System.Text', 'System.Threading', 'System.Threading.Tasks', 'System.Xml',
        'OdfKit.Compliance', 'OdfKit.DOM', 'OdfKit.Formula', 'OdfKit.Styles'
    )
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param(
        [string]$Path,
        [string]$RegionName,
        [string[]]$BodyLines,
        [switch]$SkipUsings
    )
    $out = [System.Collections.Generic.List[string]]::new()
    if (-not $SkipUsings) {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
        $out.Add('')
    }
    $out.Add('namespace OdfKit.Core;')
    $out.Add('')
    $out.Add('public sealed partial class OdfPackage')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    $out.AddRange($BodyLines)
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$regionLine = 22
$coreBody = $lines[($regionLine + 1)..($splitLine - 2)]
$core = $lines[0..$regionLine] + $coreBody + @('    #endregion', '}')
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfPackage.Saving.cs: $($core.Count) lines"

$internalsBody = $lines[($splitLine - 1)..($lines.Count - 3)]
Write-PartialFile -Path (Join-Path $coreDir 'OdfPackage.Saving.Internals.cs') -RegionName 'Saving and Atomic Save - Internals' -BodyLines $internalsBody
Write-Host "  OdfPackage.Saving.Internals.cs: $($internalsBody.Count) body lines"
Write-Host 'Done.'