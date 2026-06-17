#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$dom = Join-Path $PSScriptRoot '..\OdfKit\DOM'

$enumTokenPattern = 'return OdfElementSchemaRegistry\.TryParseEnumToken\(value, out (\w+) \w+\) \? \w+ : null;'
$enumTokenReplacement = 'return OdfElementEnumAttributeAccess.GetEnumToken<$1>(value);'

$schemaParsePattern = 'return OdfElementSchemaRegistry\.(TryParse(?!EnumToken)\w+)\(value, out (\w+) \w+\) \? \w+ : null;'
$schemaParseReplacement = 'return OdfElementEnumAttributeAccess.GetNullable<$2>(value, OdfElementSchemaRegistry.$1);'

$targets = Get-ChildItem -LiteralPath $dom -Filter 'OdfElement.Attribute*.cs'

foreach ($file in $targets) {
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    $text = [regex]::Replace($text, $enumTokenPattern, $enumTokenReplacement)
    $text = [regex]::Replace($text, $schemaParsePattern, $schemaParseReplacement)
    Set-Content -LiteralPath $file.FullName -Value $text -Encoding UTF8 -NoNewline
}

Write-Host 'OdfElementEnumAttributeAccess migration complete.'