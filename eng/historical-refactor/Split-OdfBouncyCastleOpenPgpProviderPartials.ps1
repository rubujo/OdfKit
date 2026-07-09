#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfBouncyCastleOpenPgpProvider.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$files = @(
    @{ Start = 123; End = 257; File = 'OdfBouncyCastleOpenPgpProvider.Encryption.cs'; Region = 'OpenPGP Encryption' }
    @{ Start = 259; End = 438; File = 'OdfBouncyCastleOpenPgpProvider.Decryption.cs'; Region = 'OpenPGP Decryption' }
    @{ Start = 440; End = 528; File = 'OdfBouncyCastleOpenPgpProvider.Ecdh.cs'; Region = 'ECDH Primitives' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('System.IO')
    [void]$needed.Add('System.Security.Cryptography')
    [void]$needed.Add('Org.BouncyCastle.Bcpg')
    [void]$needed.Add('Org.BouncyCastle.Bcpg.OpenPgp')
    [void]$needed.Add('Org.BouncyCastle.Crypto')
    if ($Text -match 'ECDHCBasicAgreement|Agreement') { [void]$needed.Add('Org.BouncyCastle.Crypto.Agreement') }
    if ($Text -match 'Pkcs1Encoding|Encodings') { [void]$needed.Add('Org.BouncyCastle.Crypto.Encodings') }
    if ($Text -match 'RsaBlindedEngine|ElGamalEngine|AesEngine|Rfc3394WrapEngine') { [void]$needed.Add('Org.BouncyCastle.Crypto.Engines') }
    if ($Text -match 'KeyPairGenerator|KeyGenerationParameters') { [void]$needed.Add('Org.BouncyCastle.Crypto.Generators') }
    if ($Text -match 'KeyParameter|ParametersWithRandom|ECKeyParameters|X25519') { [void]$needed.Add('Org.BouncyCastle.Crypto.Parameters') }
    if ($Text -match 'SecureRandom') { [void]$needed.Add('Org.BouncyCastle.Security') }

    $order = @(
        'System', 'System.IO', 'System.Security.Cryptography',
        'Org.BouncyCastle.Bcpg', 'Org.BouncyCastle.Bcpg.OpenPgp', 'Org.BouncyCastle.Crypto',
        'Org.BouncyCastle.Crypto.Agreement', 'Org.BouncyCastle.Crypto.Encodings', 'Org.BouncyCastle.Crypto.Engines',
        'Org.BouncyCastle.Crypto.Generators', 'Org.BouncyCastle.Crypto.Parameters', 'Org.BouncyCastle.Security'
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
    $out.Add('public sealed partial class OdfBouncyCastleOpenPgpProvider')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$coreEnd = 119
$core = $lines[0..($coreEnd - 1)] | ForEach-Object {
    $_ -replace '^public sealed class OdfBouncyCastleOpenPgpProvider', 'public sealed partial class OdfBouncyCastleOpenPgpProvider'
}
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfBouncyCastleOpenPgpProvider.cs: $($core.Count) lines"

foreach ($block in $files) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $coreDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

$extractBody = $lines[529..($lineCount - 2)]
Write-PartialFile -Path (Join-Path $coreDir 'OdfBouncyCastleOpenPgpProvider.SessionKeyPayload.cs') -RegionName 'Session Key Payload' -BodyLines $extractBody
Write-Host "  OdfBouncyCastleOpenPgpProvider.SessionKeyPayload.cs: $($extractBody.Count) body lines"
Write-Host 'Done.'