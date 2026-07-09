#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$regionMap = [ordered]@{
    'Package Lifecycle & Persistence'              = 'OdfDocument.Lifecycle.cs'
    'High-Level Digital Signatures'              = 'OdfDocument.Signatures.cs'
    'Metadata API (meta.xml)'                      = 'OdfDocument.Metadata.cs'
    'Zoom & View Settings (settings.xml)'          = 'OdfDocument.ViewSettings.cs'
    'Web Streaming APIs'                           = 'OdfDocument.Streaming.cs'
    'Document Merging API'                         = 'OdfDocument.Merging.cs'
    'Helper Methods'                               = 'OdfDocument.Helpers.cs'
    'Statistics & Document Structure Diagnostics'  = 'OdfDocument.Statistics.cs'
    'Internal Merging Helpers'                     = 'OdfDocument.MergingInternals.cs'
}

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'Stream|MemoryStream|File\.|Path\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'X509Certificate|Cryptography') { [void]$needed.Add('System.Security.Cryptography.X509Certificates') }
    if ($Text -match 'StringBuilder|Encoding\.|UTF8Encoding') { [void]$needed.Add('System.Text') }
    if ($Text -match 'CancellationToken') { [void]$needed.Add('System.Threading') }
    if ($Text -match 'Task\.|ValueTask') { [void]$needed.Add('System.Threading.Tasks') }
    if ($Text -match 'XmlReader|XmlWriter|XmlDocument') { [void]$needed.Add('System.Xml') }
    if ($Text -match 'OdfCompliance|OdfVersion|OdfValidator') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfLength|OdfStyle|StyleEngine') { [void]$needed.Add('OdfKit.Styles') }
    if ($Text -match 'Presentation\.|Spreadsheet\.|Chart\.|Formula\.') { [void]$needed.Add('OdfKit.Presentation') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.IO', 'System.Security.Cryptography.X509Certificates',
        'System.Text', 'System.Threading', 'System.Threading.Tasks', 'System.Xml',
        'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles'
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
    $out.Add('namespace OdfKit.Core;')
    $out.Add('')
    $out.Add('public abstract partial class OdfDocument')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    $out.AddRange($BodyLines)
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$coreEnd = 0
$lastRegionEnd = 0
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\s*#region ' -and $coreEnd -eq 0) { $coreEnd = $i - 1 }
    if ($lines[$i] -match '^\s*#endregion') { $lastRegionEnd = $i }
}

$core = $lines[0..$coreEnd] | ForEach-Object {
    $_ -replace '^public abstract class OdfDocument', 'public abstract partial class OdfDocument'
}
if ($lastRegionEnd -gt 0 -and $lastRegionEnd + 1 -lt $lines.Count - 1) {
    $core += $lines[($lastRegionEnd + 1)..($lines.Count - 2)]
}
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfDocument.cs: $($core.Count) lines"

$classBody = $lines[($coreEnd + 1)..$lastRegionEnd]
$currentRegion = $null
$currentLines = [System.Collections.Generic.List[string]]::new()

function Flush-Region {
    if ($null -eq $script:currentRegion) { return }
    $fileName = $regionMap[$script:currentRegion]
    if (-not $fileName) { throw "Unknown region: $($script:currentRegion)" }
    Write-PartialFile -Path (Join-Path $coreDir $fileName) -RegionName $script:currentRegion -BodyLines $script:currentLines
    Write-Host "  $fileName : $($script:currentLines.Count) body lines"
    $script:currentLines.Clear()
}

foreach ($line in $classBody) {
    if ($line -match '^\s*#region\s+(.+)$') {
        Flush-Region
        $currentRegion = $Matches[1].Trim()
        continue
    }
    if ($line -match '^\s*#endregion') {
        Flush-Region
        $currentRegion = $null
        continue
    }
    if ($null -ne $currentRegion) { $currentLines.Add($line) }
}
Flush-Region
Write-Host 'Done.'