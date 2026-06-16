#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'
$sourcePath = Join-Path $domDir 'OdfElement.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$sectionStarts = @(1110, 1657, 3301)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
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

function Write-FileWithUsings {
    param([string]$Path, [string[]]$BodyLines, [switch]$SkipUsings)
    $out = [System.Collections.Generic.List[string]]::new()
    if (-not $SkipUsings) {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
        $out.Add('')
    }
    $out.AddRange($BodyLines)
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$coreEnd = $sectionStarts[0] - 2
$core = $lines[0..$coreEnd] | ForEach-Object {
    $_ -replace '^public class OdfElement', 'public partial class OdfElement'
}
$core += '}'
Write-FileWithUsings -Path $sourcePath -BodyLines $core -SkipUsings
Write-Host "Core OdfElement.cs: $($core.Count) lines"

$files = @(
    @{ Start = 1110; End = 1656; File = 'OdfElement.AttributeValues.A.cs'; Region = 'Attribute Values (A)' }
    @{ Start = 1657; End = 3300; File = 'OdfElement.AttributeValues.B.cs'; Region = 'Attribute Values (B)' }
    @{ Start = 3301; File = 'OdfElement.EnumParsers.cs'; Region = 'Enum Parsers' }
)
for ($i = 0; $i -lt $files.Count; $i++) {
    $start = $files[$i].Start - 1
    $end = if ($null -ne $files[$i].End) { $files[$i].End - 1 } else { $lines.Count - 2 }
    $block = $lines[$start..$end]
    $body = @(
        'namespace OdfKit.DOM;',
        '',
        'public partial class OdfElement',
        '{',
        "    #region $($files[$i].Region)",
        ''
    ) + $block + @('', "    #endregion", '}')
    Write-FileWithUsings -Path (Join-Path $domDir $files[$i].File) -BodyLines $body
    Write-Host "  $($files[$i].File): $($block.Count) body lines"
}

Write-Host 'Done.'