#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$complianceDir = Join-Path $PSScriptRoot '..\OdfKit\Compliance'
$sourcePath = Join-Path $complianceDir 'OdfPackageValidator.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$sectionStarts = @(82, 195, 566, 837)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyDictionary<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'IOException|Stream|File\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'Enumerable\.|\.Linq|\.Select\(') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'SecurityException') { [void]$needed.Add('System.Security') }
    if ($Text -match 'XmlReader|XmlConvert|XmlDocument') { [void]$needed.Add('System.Xml') }
    if ($Text -match 'OdfPackage|OdfDocument|OdfNamespaces') { [void]$needed.Add('OdfKit.Core') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.IO', 'System.Linq', 'System.Security',
        'System.Xml', 'OdfKit.Core'
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
    $out.Add('public static partial class OdfPackageValidator')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$header = $lines[0..14]
$fields = $lines[15..30]
$validate = $lines[32..79]
$core = ($header + $fields + $validate) | ForEach-Object {
    $_ -replace '^public static class OdfPackageValidator', 'public static partial class OdfPackageValidator'
}
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfPackageValidator.cs: $($core.Count) lines"

$files = @(
    @{ Start = 82; File = 'OdfPackageValidator.MimeType.cs'; Region = 'MIME Type & Document Kind' }
    @{ Start = 195; File = 'OdfPackageValidator.Manifest.cs'; Region = 'Entry Paths & Manifest' }
    @{ Start = 566; File = 'OdfPackageValidator.XmlRoots.cs'; Region = 'Version Detection & XML Roots' }
    @{ Start = 837; File = 'OdfPackageValidator.Profile.cs'; Region = 'Profile Extension & Version' }
)
for ($i = 0; $i -lt $files.Count; $i++) {
    $start = $files[$i].Start - 1
    $end = if ($i + 1 -lt $files.Count) { $files[$i + 1].Start - 2 } else { 897 }
    $block = $lines[$start..$end]
    Write-PartialFile -Path (Join-Path $complianceDir $files[$i].File) -RegionName $files[$i].Region -BodyLines $block
    Write-Host "  $($files[$i].File): $($block.Count) body lines"
}

Write-Host 'Done.'