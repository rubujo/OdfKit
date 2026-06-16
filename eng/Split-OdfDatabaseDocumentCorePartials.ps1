#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$dbDir = Join-Path $PSScriptRoot '..\OdfKit\Database'
$sourcePath = Join-Path $dbDir 'OdfDatabaseDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$blocks = @(
    @{ Start = 428; End = 525; File = 'OdfDatabaseDocument.Mutations.cs'; Region = 'Remove Operations' }
    @{ Start = 527; End = ($lineCount - 2); File = 'OdfDatabaseDocument.Infrastructure.cs'; Region = 'Defaults, Merge & Helpers' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'Stream|File\.|Path\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'OdfCompliance|OdfVersion|OdfVersionInfo') { [void]$needed.Add('OdfKit.Compliance') }

    $order = @('System', 'System.Collections.Generic', 'System.IO', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM')
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
    $out.Add('namespace OdfKit.Database;')
    $out.Add('')
    $out.Add('public partial class OdfDatabaseDocument')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$core = $lines[0..425]
$core[12] = $core[12] -replace '^public class OdfDatabaseDocument', 'public partial class OdfDatabaseDocument'
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfDatabaseDocument.cs: $($core.Count) lines"

foreach ($block in $blocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $dbDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Write-Host 'Done.'