#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$dbDir = Join-Path $PSScriptRoot '..\OdfKit\Database'
$sourcePath = Join-Path $dbDir 'OdfDatabaseDocument.cs'
$mutationsPath = Join-Path $dbDir 'OdfDatabaseDocument.Mutations.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$mutationLines = [System.Collections.Generic.List[string]]@(Get-Content -Path $mutationsPath -Encoding UTF8)

$addBody = $lines[279..425]

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }

    $order = @('System', 'System.Collections.Generic', 'OdfKit.Core', 'OdfKit.DOM')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

$core = $lines[0..277] + '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfDatabaseDocument.cs: $($core.Count) lines"

$out = [System.Collections.Generic.List[string]]::new()
$removeBody = $mutationLines[10..($mutationLines.Count - 2)]
$text = ($addBody + $removeBody) -join "`n"
foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
$out.Add('')
$out.Add('namespace OdfKit.Database;')
$out.Add('')
$out.Add('public partial class OdfDatabaseDocument')
$out.Add('{')
$out.Add('    #region Add Operations')
$out.Add('')
foreach ($line in $addBody) { $out.Add($line) }
$out.Add('')
$out.Add('    #endregion')
$out.Add('')
foreach ($line in $removeBody) { $out.Add($line) }
$out.Add('}')
Set-Content -Path $mutationsPath -Value $out -Encoding UTF8
Write-Host "  OdfDatabaseDocument.Mutations.cs: add $($addBody.Count) + remove body lines"
Write-Host 'Done.'