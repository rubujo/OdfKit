#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$coreDir = Join-Path $PSScriptRoot '..\OdfKit\Core'
$sourcePath = Join-Path $coreDir 'OdfEncryption.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$files = @(
    @{ Start = 95; File = 'OdfEncryption.Entry.cs'; Region = 'Entry Encryption & Decryption' }
    @{ Start = 374; File = 'OdfEncryption.Package.cs'; Region = 'Package Encryption & Decryption' }
    @{ Start = 624; File = 'OdfEncryption.Algorithms.cs'; Region = 'Hash & Cipher Primitives' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'IOException|Stream|MemoryStream|ZipArchive|File\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'ZipArchive|Compression') { [void]$needed.Add('System.IO.Compression') }
    if ($Text -match 'Enumerable\.|\.Linq') { [void]$needed.Add('System.Linq') }
    if ($Text -match 'SecurityException') { [void]$needed.Add('System.Security') }
    if ($Text -match 'Cryptography|Aes|Sha|HMAC|Rijndael') { [void]$needed.Add('System.Security.Cryptography') }
    if ($Text -match 'StringBuilder|Encoding\.') { [void]$needed.Add('System.Text') }
    if ($Text -match 'BlowfishEngine|AesEngine|Org\.BouncyCastle\.Crypto\.Engines') { [void]$needed.Add('Org.BouncyCastle.Crypto.Engines') }
    if ($Text -match 'Pkcs5S2|Generators') { [void]$needed.Add('Org.BouncyCastle.Crypto.Generators') }
    if ($Text -match 'GcmBlockCipher|CbcBlockCipher|Modes') { [void]$needed.Add('Org.BouncyCastle.Crypto.Modes') }
    if ($Text -match 'PaddedBufferedBlockCipher|Paddings') { [void]$needed.Add('Org.BouncyCastle.Crypto.Paddings') }
    if ($Text -match 'KeyParameter|Parameters') { [void]$needed.Add('Org.BouncyCastle.Crypto.Parameters') }
    if ($Text -match 'OdfPackage|OdfEncryption') { [void]$needed.Add('OdfKit.Core') }

    $order = @(
        'System', 'System.Collections.Generic', 'System.IO', 'System.IO.Compression', 'System.Linq',
        'System.Security', 'System.Security.Cryptography', 'System.Text',
        'Org.BouncyCastle.Crypto.Engines', 'Org.BouncyCastle.Crypto.Generators',
        'Org.BouncyCastle.Crypto.Modes', 'Org.BouncyCastle.Crypto.Paddings', 'Org.BouncyCastle.Crypto.Parameters'
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
    $out.Add('public static partial class OdfEncryption')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$header = $lines[0..19]
$constants = $lines[20..53]
$pbkdf2 = $lines[54..92]
$core = ($header + $constants + $pbkdf2) | ForEach-Object {
    $_ -replace '^public static class OdfEncryption', 'public static partial class OdfEncryption'
}
$core += '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfEncryption.cs: $($core.Count) lines"

for ($i = 0; $i -lt $files.Count; $i++) {
    $start = $files[$i].Start - 1
    $end = if ($i + 1 -lt $files.Count) { $files[$i + 1].Start - 2 } else { ($lineCount - 3) }
    $block = $lines[$start..$end]
    Write-PartialFile -Path (Join-Path $coreDir $files[$i].File) -RegionName $files[$i].Region -BodyLines $block
    Write-Host "  $($files[$i].File): $($block.Count) body lines"
}

Write-Host 'Done.'