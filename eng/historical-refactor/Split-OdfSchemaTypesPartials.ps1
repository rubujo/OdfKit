#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$complianceDir = Join-Path $PSScriptRoot '..\OdfKit\Compliance'
$sourcePath = Join-Path $complianceDir 'OdfSchemaTypes.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$typeBlocks = @(
    @{ Start = 7; End = 159; File = 'OdfSchemaEnums.cs' }
    @{ Start = 160; End = 214; File = 'OdfQualifiedName.cs' }
    @{ Start = 215; End = 248; File = 'OdfElementDefinition.cs' }
    @{ Start = 249; End = 282; File = 'OdfAttributeDefinition.cs' }
    @{ Start = 283; End = 341; File = 'OdfSchemaNameClass.cs' }
    @{ Start = 342; End = 372; File = 'OdfSchemaPatternDefinition.cs' }
    @{ Start = 373; End = 390; File = 'OdfSchemaDatatypeParameter.cs' }
    @{ Start = 391; End = 504; File = 'OdfSchemaPatternNode.cs' }
    @{ Start = 505; End = $lineCount; File = 'OdfSchemaSet.cs'; IsCore = $true }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<|ReadOnlyCollection<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'ReadOnlyCollection|ReadOnlyDictionary') { [void]$needed.Add('System.Collections.ObjectModel') }
    if ($Text -match 'IEquatable|IComparable') { [void]$needed.Add('System') }

    $order = @('System', 'System.Collections.Generic', 'System.Collections.ObjectModel')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-TypeFile {
    param([string]$Path, [string[]]$BodyLines, [switch]$IsCore)
    $out = [System.Collections.Generic.List[string]]::new()
    if ($IsCore) {
        foreach ($line in $lines[0..4]) { $out.Add($line) }
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
        Remove-Item -Path $sourcePath -Force
        Write-Host "Core $($block.File): $($body.Count) body lines"
    }
    else {
        Write-TypeFile -Path $path -BodyLines $body
        Write-Host "  $($block.File): $($body.Count) body lines"
    }
}

Write-Host 'Done.'