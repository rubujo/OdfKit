#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfPackage.Loading.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$blocks = @(
    @{ Start = 21; End = 189; File = 'OdfPackage.Loading.Initialize.cs' }
    @{ Start = 191; End = 474; File = 'OdfPackage.Loading.FlatXml.cs' }
    @{ Start = 476; End = ($lineCount - 3); File = 'OdfPackage.Loading.Manifest.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'Stream|MemoryStream|File\.|Path\.|ZipArchive') { [void]$needed.Add('System.IO') }
    if ($Text -match 'ZipArchive|Compression') { [void]$needed.Add('System.IO.Compression') }
    if ($Text -match 'SecurityException|SecureString') { [void]$needed.Add('System.Security') }
    if ($Text -match 'Cryptography|Aes|Sha') { [void]$needed.Add('System.Security.Cryptography') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }
    if ($Text -match 'XmlReader|XmlWriter|XmlDocument|DtdProcessing') { [void]$needed.Add('System.Xml') }
    if ($Text -match 'XDocument|XElement') { [void]$needed.Add('System.Xml.Linq') }
    if ($Text -match 'CultureInfo') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'OdfCompliance|OdfValidator|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'CryptographicException') { [void]$needed.Add('System.Security.Cryptography') }
    if ($Text -match 'OdfEncryption|OdfPackage|OdfNode|OdfNamespaces') { [void]$needed.Add('OdfKit.Core') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.Globalization', 'System.IO', 'System.IO.Compression',
        'System.Security', 'System.Security.Cryptography', 'System.Text', 'System.Xml', 'System.Xml.Linq',
        'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM'
    )
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Core;')
    $out.Add('')
    $out.Add('public sealed partial class OdfPackage')
    $out.Add('{')
    $out.Add('    #region Initialization & Loading')
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $blocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $coreDir $block.File
    Write-PartialFile -Path $path -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host 'Done.'