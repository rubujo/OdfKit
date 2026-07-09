#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$stylesDir = Join-Path $PSScriptRoot '..\OdfKit\Styles'
$sourcePath = Join-Path $stylesDir 'OdfStyleEngine.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('System.Text')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }

    $order = @('System', 'System.Collections.Generic', 'System.Text', 'OdfKit.Core', 'OdfKit.DOM')
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
    $out.Add('namespace OdfKit.Styles;')
    $out.Add('')
    $out.Add('public partial class OdfStyleEngine')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$coreBody = $lines[0..194] | ForEach-Object {
    $_ -replace '^public class OdfStyleEngine', 'public partial class OdfStyleEngine'
}
$helperBody = $lines[507..($lineCount - 2)]
$core = $coreBody + $helperBody + '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfStyleEngine.cs: $($core.Count) lines"

$localBody = $lines[198..504]
Write-PartialFile -Path (Join-Path $stylesDir 'OdfStyleEngine.LocalStyles.cs') -RegionName 'Local Styles & Deduplication' -BodyLines $localBody
Write-Host "  OdfStyleEngine.LocalStyles.cs: $($localBody.Count) body lines"
Write-Host 'Done.'