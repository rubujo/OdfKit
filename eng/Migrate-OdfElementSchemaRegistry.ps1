#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$dom = Join-Path $PSScriptRoot '..\OdfKit\DOM'

$map = [ordered]@{
    'OdfElement.EnumParsers.CalendarFoAndDraw3d.cs'             = 'OdfElementSchemaRegistry.CalendarFoAndDraw3d.cs'
    'OdfElement.EnumParsers.LineAndFont.cs'                     = 'OdfElementSchemaRegistry.LineAndFont.cs'
    'OdfElement.EnumParsers.PresentationEffectAndTransition.cs'  = 'OdfElementSchemaRegistry.PresentationEffectAndTransition.cs'
    'OdfElement.EnumParsers.PresentationTransitionStyles.cs'    = 'OdfElementSchemaRegistry.PresentationTransitionStyles.cs'
    'OdfElement.EnumParsers.StyleXLinkAndTable.cs'              = 'OdfElementSchemaRegistry.StyleXLinkAndTable.cs'
    'OdfElement.EnumParsers.TableBorderAndText.cs'              = 'OdfElementSchemaRegistry.TableBorderAndText.cs'
    'OdfElement.EnumParsers.TextKindStyleFormAndTable.cs'       = 'OdfElementSchemaRegistry.TextKindStyleFormAndTable.cs'
    'OdfElement.EnumParsers.TextNumberingAndAnimation.cs'       = 'OdfElementSchemaRegistry.TextNumberingAndAnimation.cs'
}

foreach ($entry in $map.GetEnumerator()) {
    $src = Join-Path $dom $entry.Key
    $dst = Join-Path $dom $entry.Value
    $text = Get-Content -LiteralPath $src -Raw -Encoding UTF8
    $text = $text.Replace('public partial class OdfElement', 'internal static partial class OdfElementSchemaRegistry')
    $text = $text.Replace('#region Enum Parsers', '#region Schema Registry')
    $text = $text.Replace('private static', 'internal static')
    if ($text -notmatch 'ODF 元素 schema 枚舉 token 靜態註冊表') {
        $text = $text.Replace(
            "namespace OdfKit.DOM;`r`n`r`ninternal static partial class OdfElementSchemaRegistry",
            "namespace OdfKit.DOM;`r`n`r`n/// <summary>`r`n/// ODF 元素 schema 枚舉 token 靜態註冊表（部分檔案）。`r`n/// </summary>`r`ninternal static partial class OdfElementSchemaRegistry")
    }

    Set-Content -LiteralPath $dst -Value $text -Encoding UTF8 -NoNewline
    Remove-Item -LiteralPath $src -Force
}

$hub = @'
﻿using System;

namespace OdfKit.DOM;

/// <summary>
/// ODF 元素 schema 枚舉 token 靜態註冊表（聚合根）。
/// </summary>
internal static partial class OdfElementSchemaRegistry
{
}
'@
Set-Content -LiteralPath (Join-Path $dom 'OdfElementSchemaRegistry.cs') -Value $hub -Encoding UTF8 -NoNewline

$targets = @(
    (Get-ChildItem -LiteralPath $dom -Filter 'OdfElement.Attribute*.cs').FullName
    (Join-Path $dom 'OdfElement.TypedAttributes.Complex.cs')
)

$tryParsePattern = '(?<!(?:DateTime|OdfLineWidth|OdfVersionInfo)\.)\bTryParse([A-Z][A-Za-z0-9]+)\('
$formatPattern = '(?<!(?:OdfVersionInfo)\.)\bFormat([A-Z][A-Za-z0-9]+)\('

foreach ($file in $targets) {
    $text = Get-Content -LiteralPath $file -Raw -Encoding UTF8
    $text = [regex]::Replace($text, $tryParsePattern, 'OdfElementSchemaRegistry.TryParse$1(')
    $text = [regex]::Replace($text, $formatPattern, 'OdfElementSchemaRegistry.Format$1(')
    Set-Content -LiteralPath $file -Value $text -Encoding UTF8 -NoNewline
}

Write-Host 'OdfElementSchemaRegistry migration complete.'