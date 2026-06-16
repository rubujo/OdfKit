#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$dbDir = Join-Path $PSScriptRoot '..\OdfKit\Database'
$sourcePath = Join-Path $dbDir 'OdfDatabaseDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$typeBlocks = @(
    @{ Start = 10; End = 730; File = 'OdfDatabaseDocument.cs'; IsCore = $true }
    @{ Start = 731; End = 748; File = 'OdfDatabaseTableInfo.cs' }
    @{ Start = 749; End = 789; File = 'OdfDatabaseQueryInfo.cs' }
    @{ Start = 790; End = $lineCount; File = 'OdfDatabaseDataSourceSettingInfo.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'Stream|File\.|Path\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'OdfCompliance|OdfVersion|OdfSchema') { [void]$needed.Add('OdfKit.Compliance') }

    $order = @('System', 'System.Collections.Generic', 'System.IO', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM')
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
        foreach ($line in $lines[0..7]) { $out.Add($line) }
        $out.Add('')
    }
    else {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
        $out.Add('')
        $out.Add('namespace OdfKit.Database;')
        $out.Add('')
    }
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $dbDir $block.File
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