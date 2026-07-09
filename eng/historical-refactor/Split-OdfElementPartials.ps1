#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfElement.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$regionMap = [ordered]@{
    'Text Wrappers'     = 'OdfElement.TextWrappers.cs'
    'Table Wrappers'    = 'OdfElement.TableWrappers.cs'
    'Draw Wrappers'     = 'OdfElement.DrawWrappers.cs'
    'Style Wrappers'    = 'OdfElement.StyleWrappers.cs'
    'Office Wrappers'   = 'OdfElement.OfficeWrappers.cs'
    'Manifest Wrappers' = 'OdfElement.ManifestWrappers.cs'
}

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'Stream|MemoryStream|File\.|Path\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'CultureInfo') { [void]$needed.Add('System.Globalization') }
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

$core = $lines[0..5422]
Write-FileWithUsings -Path $sourcePath -BodyLines $core -SkipUsings
Write-Host "Core OdfElement.cs: $($core.Count) lines"

$regionBody = $lines[5424..($lines.Count - 1)]
$currentRegion = $null
$currentLines = [System.Collections.Generic.List[string]]::new()

function Flush-Region {
    if ($null -eq $script:currentRegion) { return }
    $fileName = $regionMap[$script:currentRegion]
    if (-not $fileName) { throw "Unknown region: $($script:currentRegion)" }
    $body = @('namespace OdfKit.DOM;', '') + @("#region $($script:currentRegion)", '') + $script:currentLines + @('', '#endregion')
    Write-FileWithUsings -Path (Join-Path $domDir $fileName) -BodyLines $body
    Write-Host "  $fileName : $($script:currentLines.Count) body lines"
    $script:currentLines.Clear()
}

foreach ($line in $regionBody) {
    if ($line -match '^#region\s+(.+)$') {
        Flush-Region
        $currentRegion = $Matches[1].Trim()
        continue
    }
    if ($line -match '^#endregion') {
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