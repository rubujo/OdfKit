#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$dom = Join-Path $PSScriptRoot '..\OdfKit\DOM'

$signedPercentPattern = 'return OdfPercent\.TryParse\(value, allowNegative: true, out OdfPercent \w+\) \? \w+ : null;'
$signedPercentReplacement = 'return OdfElementDomainAttributeAccess.GetSignedPercent(value);'

$percentPattern = 'return OdfPercent\.TryParse\(value, out OdfPercent \w+\) \? \w+ : null;'
$percentReplacement = 'return OdfElementDomainAttributeAccess.GetPercent(value);'

$versionPattern = 'return OdfVersionInfo\.TryParseVersionString\(value, out OdfVersion \w+\) \? \w+ : null;'
$versionReplacement = 'return OdfElementDomainAttributeAccess.GetVersion(value);'

$domainParsePattern = 'return (\w+)\.TryParse\(value, out \w+ \w+\) \? \w+ : null;'
$domainParseReplacement = 'return OdfElementDomainAttributeAccess.GetNullable<$1>(value, $1.TryParse);'

$versionSetterPattern = 'SetAttributeValue\(localName, namespaceUri, OdfVersionInfo\.ToVersionString\(value\), prefix, version\);'
$versionSetterReplacement = 'SetAttributeValue(localName, namespaceUri, OdfElementDomainAttributeAccess.FormatVersion(value), prefix, version);'

$targets = Get-ChildItem -LiteralPath $dom -Filter 'OdfElement.Attribute*.cs'

foreach ($file in $targets) {
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $text = [regex]::Replace($text, $signedPercentPattern, $signedPercentReplacement)
    $text = [regex]::Replace($text, $percentPattern, $percentReplacement)
    $text = [regex]::Replace($text, $versionPattern, $versionReplacement)
    $text = [regex]::Replace($text, $domainParsePattern, $domainParseReplacement)
    $text = [regex]::Replace($text, $versionSetterPattern, $versionSetterReplacement)
    Set-Content -LiteralPath $file.FullName -Value $text -Encoding UTF8 -NoNewline
}

Write-Host 'OdfElementDomainAttributeAccess migration complete.'