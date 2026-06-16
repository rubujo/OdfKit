#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfPackage.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$regionMap = [ordered]@{
    'Factory Methods'                              = 'OdfPackage.Factory.cs'
    'Initialization & Loading'                     = 'OdfPackage.Loading.cs'
    'ZIP Path & Entry Sanitize (Zip Slip Protection)' = 'OdfPackage.ZipSanitize.cs'
    'Macro Sanitization'                           = 'OdfPackage.MacroSanitize.cs'
    'Public API'                                   = 'OdfPackage.PublicApi.cs'
    'Embedded Objects Extraction'                  = 'OdfPackage.EmbeddedObjects.cs'
    'Saving and Atomic Save'                       = 'OdfPackage.Saving.cs'
    'Dispose'                                      = 'OdfPackage.Dispose.cs'
}

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'Task\.|CancellationToken') {
        [void]$needed.Add('System.Threading')
        [void]$needed.Add('System.Threading.Tasks')
    }
    if ($Text -match 'Stream|MemoryStream|File\.|Path\.|ZipArchive') { [void]$needed.Add('System.IO') }
    if ($Text -match 'ZipArchive|Compression') { [void]$needed.Add('System.IO.Compression') }
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<|ConcurrentDictionary') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match '\.Linq|Enumerable\.') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'Security\.|Cryptography') {
        [void]$needed.Add('System.Security')
        [void]$needed.Add('System.Security.Cryptography')
    }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }
    if ($Text -match 'XmlReader|XmlWriter|XmlDocument') { [void]$needed.Add('System.Xml') }
    if ($Text -match 'XDocument|XElement') { [void]$needed.Add('System.Xml.Linq') }
    if ($Text -match 'CultureInfo') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'OdfCompliance|OdfValidator|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfNode|OdfNamespaces|OdfDocument') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'DefaultFormulaEvaluator|IOdfFormulaEvaluator') { [void]$needed.Add('OdfKit.Formula') }
    if ($Text -match 'OdfLength|OdfStyle|StyleEngine') { [void]$needed.Add('OdfKit.Styles') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.Globalization', 'System.IO', 'System.IO.Compression',
        'System.Linq', 'System.Security', 'System.Security.Cryptography', 'System.Text', 'System.Threading',
        'System.Threading.Tasks', 'System.Xml', 'System.Xml.Linq',
        'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Formula', 'OdfKit.Styles'
    )
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-FileWithUsings {
    param(
        [string]$Path,
        [string[]]$BodyLines,
        [switch]$SkipUsings
    )
    $out = [System.Collections.Generic.List[string]]::new()
    if (-not $SkipUsings) {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) {
            $out.Add($usingLine)
        }
        $out.Add('')
    }
    $out.AddRange($BodyLines)
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$coreEnd = 0
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\s*#region ') { $coreEnd = $i - 1; break }
}
$core = $lines[0..$coreEnd] | ForEach-Object {
    $_ -replace '^public sealed class OdfPackage', 'public sealed partial class OdfPackage'
}
$core += '}'
Write-FileWithUsings -Path $sourcePath -BodyLines $core -SkipUsings
Write-Host "Core OdfPackage.cs: $($core.Count) lines"

$classBody = $lines[($coreEnd + 1)..2441]
$currentRegion = $null
$currentLines = [System.Collections.Generic.List[string]]::new()

function Flush-Region {
    if ($null -eq $script:currentRegion) { return }
    $fileName = $regionMap[$script:currentRegion]
    if (-not $fileName) { throw "Unknown region: $($script:currentRegion)" }
    $body = @(
        'namespace OdfKit.Core;',
        '',
        'public sealed partial class OdfPackage',
        '{',
        "    #region $($script:currentRegion)",
        ''
    )
    $body += $script:currentLines
    $body += @('', "    #endregion", '}')
    Write-FileWithUsings -Path (Join-Path $coreDir $fileName) -BodyLines $body
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
    if ($null -ne $currentRegion) {
        $currentLines.Add($line)
    }
}
Flush-Region

$entryBlock = $lines[2444..($lines.Count - 1)]
$entryLines = [System.Collections.Generic.List[string]]::new()
$inRegion = $false
foreach ($line in $entryBlock) {
    if ($line -match '^#region\s+Package Entry Representation') { $inRegion = $true; continue }
    if ($line -match '^#endregion') { break }
    if ($inRegion) { $entryLines.Add($line) }
}
$entryBody = @('namespace OdfKit.Core;', '') + $entryLines
Write-FileWithUsings -Path (Join-Path $coreDir 'OdfPackageEntry.cs') -BodyLines $entryBody
Write-Host "OdfPackageEntry.cs: $($entryLines.Count) lines"

Write-Host 'Done.'