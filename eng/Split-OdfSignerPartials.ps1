#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfSigner.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

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
    if ($Text -match 'Cryptography|X509|SignedXml|RSA|ECDsa|AsymmetricAlgorithm') {
        [void]$needed.Add('System.Security.Cryptography')
        [void]$needed.Add('System.Security.Cryptography.Pkcs')
        [void]$needed.Add('System.Security.Cryptography.X509Certificates')
        [void]$needed.Add('System.Security.Cryptography.Xml')
    }
    if ($Text -match 'StringBuilder|Encoding\.|UTF8Encoding') { [void]$needed.Add('System.Text') }
    if ($Text -match 'XmlReader|XmlDocument|XmlWriter|XmlElement|XmlNode|XmlResolver') { [void]$needed.Add('System.Xml') }
    if ($Text -match 'OdfCompliance|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfPackage|OdfKitDiagnostics|OdfVersionInfo') { [void]$needed.Add('OdfKit.Core') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.Globalization', 'System.IO', 'System.Net.Http',
        'System.Numerics', 'System.Reflection', 'System.Security.Cryptography', 'System.Security.Cryptography.Pkcs',
        'System.Security.Cryptography.X509Certificates', 'System.Security.Cryptography.Xml', 'System.Text',
        'System.Threading.Tasks', 'System.Xml', 'OdfKit.Compliance', 'OdfKit.Core'
    )
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param(
        [string]$Path,
        [string]$RegionName,
        [string[]]$BodyLines,
        [switch]$IsStaticPartial,
        [switch]$SkipRegion
    )
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    if ($IsStaticPartial) {
        $out.Add('namespace OdfKit.Core;')
        $out.Add('')
        $out.Add('public static partial class OdfSigner')
        $out.Add('{')
        if (-not $SkipRegion) {
            $out.Add("    #region $RegionName")
            $out.Add('')
        }
        $out.AddRange($BodyLines)
        if (-not $SkipRegion) {
            $out.Add('')
            $out.Add('    #endregion')
        }
        $out.Add('}')
    }
    else {
        $out.Add('namespace OdfKit.Core;')
        $out.Add('')
        $out.AddRange($BodyLines)
    }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$core = ($lines[0..45] + $lines[376..405]) | ForEach-Object {
    $_ -replace '^public static class OdfSigner', 'public static partial class OdfSigner'
}
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfSigner.cs: $($core.Count) lines"

$partials = @(
    @{ Start = 48; End = 375; File = 'OdfSigner.Signing.cs'; Region = 'Signing' }
    @{ Start = 408; End = 958; File = 'OdfSigner.Verification.cs'; Region = 'Verification' }
    @{ Start = 961; End = 1519; File = 'OdfSigner.Utilities.cs'; Region = '輔助類別與 ASN.1/DER/CRL/TSA 工具' }
)
foreach ($p in $partials) {
    $block = $lines[($p.Start - 1)..($p.End - 1)]
    Write-PartialFile -Path (Join-Path $coreDir $p.File) -RegionName $p.Region -BodyLines $block -IsStaticPartial
    Write-Host "  $($p.File): $($block.Count) body lines"
}

$resolverBody = $lines[1523..1614]
Write-PartialFile -Path (Join-Path $coreDir 'OdfPackageXmlResolver.cs') -BodyLines $resolverBody
Write-Host "  OdfPackageXmlResolver.cs: $($resolverBody.Count) body lines"

$xadesBody = $lines[1616..1715]
Write-PartialFile -Path (Join-Path $coreDir 'XadesSignedXml.cs') -BodyLines $xadesBody
Write-Host "  XadesSignedXml.cs: $($xadesBody.Count) body lines"

$derBody = $lines[1717..($lines.Count - 1)]
Write-PartialFile -Path (Join-Path $coreDir 'DerNode.cs') -BodyLines $derBody
Write-Host "  DerNode.cs: $($derBody.Count) body lines"

Write-Host 'Done.'