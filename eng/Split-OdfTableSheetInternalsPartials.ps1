#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$sheetDir = Join-Path $PSScriptRoot '..\OdfKit\Spreadsheet'
$sourcePath = Join-Path $sheetDir 'OdfTableSheet.Internals.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('System.Collections')
    [void]$needed.Add('System.Collections.Generic')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')

    $order = @('System', 'System.Collections', 'System.Collections.Generic', 'OdfKit.Core', 'OdfKit.DOM')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    foreach ($usingLine in (Get-UsingsForBlock -Text ($BodyLines -join "`n"))) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Spreadsheet;')
    $out.Add('')
    $out.Add('public partial class OdfTableSheet')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$rowsBody = $lines[12..282]
$out = [System.Collections.Generic.List[string]]::new()
foreach ($usingLine in (Get-UsingsForBlock -Text ($rowsBody -join "`n"))) { $out.Add($usingLine) }
$out.Add('')
$out.Add('namespace OdfKit.Spreadsheet;')
$out.Add('')
$out.Add('public partial class OdfTableSheet')
$out.Add('{')
$out.Add('    #region Internals')
$out.Add('')
foreach ($line in $rowsBody) { $out.Add($line) }
$out.Add('')
$out.Add('    #endregion')
$out.Add('}')
Set-Content -Path $sourcePath -Value $out -Encoding UTF8
Write-Host "Core OdfTableSheet.Internals.cs: $($out.Count) lines"

$cellAccessBody = $lines[284..426]
Write-PartialFile -Path (Join-Path $sheetDir 'OdfTableSheet.Internals.CellAccess.cs') -RegionName 'Cell & Column Access' -BodyLines $cellAccessBody
Write-Host "  OdfTableSheet.Internals.CellAccess.cs: $($cellAccessBody.Count) body lines"
Write-Host 'Done.'