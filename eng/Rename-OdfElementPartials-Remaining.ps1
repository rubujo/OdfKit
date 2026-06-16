#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$domDir = Join-Path $PSScriptRoot '..\OdfKit\DOM'

$renames = @(
    @{ Old = 'OdfElement.AttributeValues.B3a.cs'; New = 'OdfElement.AttributeValues.LineAndFont.cs'; Region = 'Attribute Values - Line & Font' }
    @{ Old = 'OdfElement.AttributeValues.B3b.cs'; New = 'OdfElement.AttributeValues.StyleFormTableAndMedia.cs'; Region = 'Attribute Values - Style, Form, Table & Media' }
    @{ Old = 'OdfElement.EnumParsers.A1.cs'; New = 'OdfElement.EnumParsers.LineAndFont.cs'; Region = 'Enum Parsers - Line & Font' }
    @{ Old = 'OdfElement.EnumParsers.A2.cs'; New = 'OdfElement.EnumParsers.StyleXLinkAndTable.cs'; Region = 'Enum Parsers - Style, XLink & Table' }
    @{ Old = 'OdfElement.EnumParsers.B1.cs'; New = 'OdfElement.EnumParsers.PresentationEffectAndTransition.cs'; Region = 'Enum Parsers - Presentation Effect & Transition' }
    @{ Old = 'OdfElement.EnumParsers.B2.cs'; New = 'OdfElement.EnumParsers.PresentationTransitionStyles.cs'; Region = 'Enum Parsers - Presentation Transition Styles' }
    @{ Old = 'OdfElement.EnumParsers.C1.cs'; New = 'OdfElement.EnumParsers.CalendarFoAndDraw3d.cs'; Region = 'Enum Parsers - Calendar, FO & Draw 3D' }
    @{ Old = 'OdfElement.EnumParsers.C2.cs'; New = 'OdfElement.EnumParsers.TableBorderAndText.cs'; Region = 'Enum Parsers - Table Border & Text' }
    @{ Old = 'OdfElement.EnumParsers.D1.cs'; New = 'OdfElement.EnumParsers.TextNumberingAndAnimation.cs'; Region = 'Enum Parsers - Text Numbering & Animation' }
    @{ Old = 'OdfElement.EnumParsers.D2.cs'; New = 'OdfElement.EnumParsers.TextKindStyleFormAndTable.cs'; Region = 'Enum Parsers - Text Kind, Style, Form & Table' }
    @{ Old = 'OdfElement.AttributeAccessors.A.cs'; New = 'OdfElement.AttributeAccessors.XLinkPresentationAndTable.cs'; Region = 'Attribute Accessors - XLink, Presentation & Table' }
    @{ Old = 'OdfElement.AttributeAccessors.B.cs'; New = 'OdfElement.AttributeAccessors.FoDrawAndPosition.cs'; Region = 'Attribute Accessors - FO, Draw & Position' }
)

function Update-Region {
    param([string]$Path, [string]$RegionName)
    $content = Get-Content -LiteralPath $Path -Encoding UTF8 -Raw
    $updated = $content -replace '#region [^\r\n]+', "#region $RegionName"
    Set-Content -LiteralPath $Path -Value $updated.TrimEnd() -Encoding UTF8 -NoNewline
    Add-Content -LiteralPath $Path -Value '' -Encoding UTF8
}

foreach ($item in $renames) {
    $oldPath = Join-Path $domDir $item.Old
    $newPath = Join-Path $domDir $item.New
    if (-not (Test-Path -LiteralPath $oldPath)) {
        if (Test-Path -LiteralPath $newPath) {
            Write-Host "Skip (already renamed): $($item.New)"
            Update-Region -Path $newPath -RegionName $item.Region
            continue
        }
        Write-Warning "Missing: $($item.Old)"
        continue
    }
    if (Test-Path -LiteralPath $newPath) { Remove-Item -LiteralPath $newPath -Force }
    $tracked = git ls-files --error-unmatch -- $oldPath 2>$null
    if ($LASTEXITCODE -eq 0) {
        git mv -- $oldPath $newPath | Out-Null
    }
    else {
        Move-Item -LiteralPath $oldPath -Destination $newPath
    }
    Update-Region -Path $newPath -RegionName $item.Region
    Write-Host "Renamed: $($item.Old) -> $($item.New)"
}

Write-Host 'Done.'