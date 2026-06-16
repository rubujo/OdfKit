#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfSigner.Utilities.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$files = @(
    @{ Start = 24; End = 274; File = 'OdfSigner.Utilities.DerCrl.cs'; Region = 'DER & CRL Utilities' }
    @{ Start = 276; End = 360; File = 'OdfSigner.Utilities.TsaNetwork.cs'; Region = 'TSA & Network Utilities' }
    @{ Start = 362; End = ($lineCount - 3); File = 'OdfSigner.Utilities.X509.cs'; Region = 'X509 & Reference Utilities' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'CultureInfo') { [void]$needed.Add('System.Globalization') }
    if ($Text -match 'Stream|MemoryStream|File\.|Directory\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'HttpClient|Task\.') {
        [void]$needed.Add('System.Net.Http')
        [void]$needed.Add('System.Threading.Tasks')
    }
    if ($Text -match 'BigInteger') { [void]$needed.Add('System.Numerics') }
    if ($Text -match 'BindingFlags|GetField|MethodInfo') { [void]$needed.Add('System.Reflection') }
    if ($Text -match 'Cryptograph|X509|SignedXml|RSA|ECDsa|AsymmetricAlgorithm') {
        [void]$needed.Add('System.Security.Cryptography')
        [void]$needed.Add('System.Security.Cryptography.Pkcs')
        [void]$needed.Add('System.Security.Cryptography.X509Certificates')
        [void]$needed.Add('System.Security.Cryptography.Xml')
    }
    if ($Text -match 'StringBuilder|Encoding\.|UTF8Encoding') { [void]$needed.Add('System.Text') }
    if ($Text -match 'XmlReader|XmlDocument|XmlWriter|XmlElement|XmlNode|XmlResolver') { [void]$needed.Add('System.Xml') }
    if ($Text -match 'OdfEncryption|OdfPackage|OdfKitDiagnostics') { [void]$needed.Add('OdfKit.Core') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.Globalization', 'System.IO', 'System.Net.Http',
        'System.Numerics', 'System.Reflection', 'System.Security.Cryptography', 'System.Security.Cryptography.Pkcs',
        'System.Security.Cryptography.X509Certificates', 'System.Security.Cryptography.Xml', 'System.Text',
        'System.Threading.Tasks', 'System.Xml', 'OdfKit.Core'
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
    $out.Add('public static partial class OdfSigner')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $files) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $coreDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host 'Removed OdfSigner.Utilities.cs'
Write-Host 'Done.'