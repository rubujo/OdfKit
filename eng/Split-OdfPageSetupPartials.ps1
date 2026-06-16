#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$textDir = Join-Path $PSScriptRoot '..\OdfKit\Text'
$sourcePath = Join-Path $textDir 'OdfPageSetup.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    [void]$needed.Add('OdfKit.Styles')

    $order = @('System', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles')
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
    $out.Add('namespace OdfKit.Text;')
    $out.Add('')
    $out.Add('public partial class OdfPageSetup')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$core = $lines[0..254] | ForEach-Object {
    $_ -replace '^public class OdfPageSetup', 'public partial class OdfPageSetup'
}
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfPageSetup.cs: $($core.Count) lines"

$infraBody = $lines[256..432]
Write-PartialFile -Path (Join-Path $textDir 'OdfPageSetup.Infrastructure.cs') -RegionName 'Page Layout Infrastructure' -BodyLines $infraBody
Write-Host "  OdfPageSetup.Infrastructure.cs: $($infraBody.Count) body lines"
Write-Host 'Done.'