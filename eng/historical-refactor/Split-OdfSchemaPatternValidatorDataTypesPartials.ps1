#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$complianceDir = Join-Path $PSScriptRoot '..\OdfKit\Compliance'
$sourcePath = Join-Path $complianceDir 'OdfSchemaPatternValidator.DataTypes.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$files = @(
    @{ Start = 15; End = 339; File = 'OdfSchemaPatternValidator.DataTypes.Matching.cs'; Region = 'Data Types - Matching' }
    @{ Start = 341; End = 478; File = 'OdfSchemaPatternValidator.DataTypes.Primitives.cs'; Region = 'Data Types - Primitives' }
    @{ Start = 480; End = ($lineCount - 3); File = 'OdfSchemaPatternValidator.DataTypes.Facets.cs'; Region = 'Data Types - Facets' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'CultureInfo|NumberStyles') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'BigInteger') { [void]$needed.Add('System.Numerics') }
    if ($Text -match 'Regex') { [void]$needed.Add('System.Text.RegularExpressions') }
    if ($Text -match 'XmlConvert|XmlException') { [void]$needed.Add('System.Xml') }
    if ($Text -match 'XElement|XAttribute') { [void]$needed.Add('System.Xml.Linq') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.Globalization', 'System.Numerics',
        'System.Text.RegularExpressions', 'System.Xml', 'System.Xml.Linq'
    )
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
    $out.Add('namespace OdfKit.Compliance;')
    $out.Add('')
    $out.Add('public static partial class OdfSchemaPatternValidator')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $files) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $complianceDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host 'Done.'