#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfElement.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$splitLine = 487

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<|IEnumerable<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'CultureInfo|NumberStyles') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'Stream|MemoryStream') { [void]$needed.Add('System.IO') }
    if ($Text -match 'OdfCompliance|OdfValidator|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfPackage|OdfNode|OdfNamespaces') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfLength|OdfStyle|StyleEngine') { [void]$needed.Add('OdfKit.Styles') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.Globalization', 'System.IO',
        'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles'
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
    $out.Add('namespace OdfKit.DOM;')
    $out.Add('')
    $out.Add('public partial class OdfElement')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    $out.AddRange($BodyLines)
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$coreEnd = $splitLine - 2
$core = $lines[0..$coreEnd] + '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfElement.cs: $($core.Count) lines"

$accessorBody = $lines[($splitLine - 1)..($lines.Count - 2)]
Write-PartialFile -Path (Join-Path $domDir 'OdfElement.AttributeAccessors.cs') -RegionName 'Attribute Accessors' -BodyLines $accessorBody
Write-Host "  OdfElement.AttributeAccessors.cs: $($accessorBody.Count) body lines"
Write-Host 'Done.'