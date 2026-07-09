#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$presDir = Join-Path $PSScriptRoot '..\OdfKit\Presentation'
$animationPath = Join-Path $presDir 'OdfAnimation.cs'
$slideAnimPath = Join-Path $presDir 'OdfSlide.Animations.cs'
$transitionsPath = Join-Path $presDir 'PresentationDocument.Transitions.cs'

$lines = [System.Collections.Generic.List[string]]@(Get-Content -LiteralPath $animationPath -Encoding UTF8)

# OdfAnimation 獨立類別（至第一個空行後的 OdfSlide partial 之前）
$slideStart = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^public partial class OdfSlide') {
        $slideStart = $i
        break
    }
}
if ($slideStart -lt 0) { throw 'Could not find OdfSlide partial in OdfAnimation.cs' }

$presStart = -1
for ($i = $slideStart + 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^public partial class PresentationDocument') {
        $presStart = $i
        break
    }
}
if ($presStart -lt 0) { throw 'Could not find PresentationDocument partial in OdfAnimation.cs' }

$animationClass = $lines[0..($slideStart - 1)]
while ($animationClass.Count -gt 0 -and [string]::IsNullOrWhiteSpace($animationClass[$animationClass.Count - 1])) {
    $animationClass = $animationClass[0..($animationClass.Count - 2)]
}

$slidePartial = @(
    'using System;'
    'using OdfKit.Core;'
    'using OdfKit.DOM;'
    ''
    'namespace OdfKit.Presentation;'
    ''
    'public partial class OdfSlide'
    '{'
    '    #region Slide Animations'
    ''
) + $(if ($presStart -gt $slideStart + 2) { $lines[($slideStart + 2)..($presStart - 3)] } else { @() }) + @(
    ''
    '    #endregion'
    '}'
)

$transPartial = @(
    'using System;'
    'using OdfKit.Core;'
    'using OdfKit.DOM;'
    'using OdfKit.Styles;'
    ''
    'namespace OdfKit.Presentation;'
    ''
    'public partial class PresentationDocument'
    '{'
    '    #region Slide Transitions'
    ''
) + $lines[($presStart + 2)..($lines.Count - 2)] + @(
    ''
    '    #endregion'
    '}'
)

Set-Content -LiteralPath $animationPath -Value $animationClass -Encoding UTF8
Set-Content -LiteralPath $slideAnimPath -Value $slidePartial -Encoding UTF8
Set-Content -LiteralPath $transitionsPath -Value $transPartial -Encoding UTF8

Write-Host "Trimmed OdfAnimation.cs ($($animationClass.Count) lines)"
Write-Host "Created OdfSlide.Animations.cs ($($slidePartial.Count) lines)"
Write-Host "Created PresentationDocument.Transitions.cs ($($transPartial.Count) lines)"
Write-Host 'Done.'