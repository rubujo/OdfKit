#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$complianceDir = Join-Path $PSScriptRoot '..\OdfKit\Compliance'
$sourcePath = Join-Path $complianceDir 'OdfSchemaPatternValidator.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$sectionStarts = @(58, 252, 977, 1502, 1829)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'CultureInfo|NumberStyles') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'BigInteger') { [void]$needed.Add('System.Numerics') }
    if ($Text -match 'Regex') { [void]$needed.Add('System.Text.RegularExpressions') }
    if ($Text -match 'XmlConvert|XmlException|XmlDateTimeSerializationMode') { [void]$needed.Add('System.Xml') }
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

$core = ($lines[0..56]) | ForEach-Object {
    $_ -replace '^public static class OdfSchemaPatternValidator', 'public static partial class OdfSchemaPatternValidator'
}
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfSchemaPatternValidator.cs: $($core.Count) lines"

$files = @(
    @{ Start = 58; File = 'OdfSchemaPatternValidator.ElementMatching.cs'; Region = 'Element Matching' }
    @{ Start = 252; File = 'OdfSchemaPatternValidator.Attributes.cs'; Region = 'Attribute Patterns' }
    @{ Start = 977; File = 'OdfSchemaPatternValidator.Content.cs'; Region = 'Content Matching' }
    @{ Start = 1502; File = 'OdfSchemaPatternValidator.NameClasses.cs'; Region = 'Name Classes & Lists' }
    @{ Start = 1829; File = 'OdfSchemaPatternValidator.DataTypes.cs'; Region = 'Data Types & Facets' }
)
for ($i = 0; $i -lt $files.Count; $i++) {
    $start = $files[$i].Start - 1
    $end = if ($i + 1 -lt $files.Count) { $files[$i + 1].Start - 2 } else { 2523 }
    $block = $lines[$start..$end]
    Write-PartialFile -Path (Join-Path $complianceDir $files[$i].File) -RegionName $files[$i].Region -BodyLines $block
    Write-Host "  $($files[$i].File): $($block.Count) body lines"
}

$resultBody = $lines[2526..($lines.Count - 1)]
$resultOut = [System.Collections.Generic.List[string]]::new()
$resultOut.Add('using System;')
$resultOut.Add('using System.Collections.Generic;')
$resultOut.Add('')
$resultOut.Add('namespace OdfKit.Compliance;')
$resultOut.Add('')
foreach ($line in $resultBody) { $resultOut.Add($line) }
Set-Content -Path (Join-Path $complianceDir 'OdfSchemaPatternValidationResult.cs') -Value $resultOut -Encoding UTF8
Write-Host "  OdfSchemaPatternValidationResult.cs: $($resultBody.Count) body lines"
Write-Host 'Done.'