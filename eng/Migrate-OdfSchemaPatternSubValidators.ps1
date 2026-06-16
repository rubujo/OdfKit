#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$complianceDir = Join-Path $PSScriptRoot '..\OdfKit\Compliance'

function Update-FileLines {
    param([string]$Path, [scriptblock]$Transform)
    $lines = Get-Content -LiteralPath $Path -Encoding UTF8
    $newLines = & $Transform $lines
    Set-Content -LiteralPath $Path -Value $newLines -Encoding UTF8
}

$validatorFiles = Get-ChildItem -LiteralPath $complianceDir -Filter 'OdfSchemaPatternValidator*.cs'
foreach ($file in $validatorFiles) {
    Update-FileLines -Path $file.FullName -Transform {
        param($lines)
        $lines | ForEach-Object {
            $_ -replace '\bMatchContext\b', 'OdfSchemaPatternMatchContext'
        }
    }
}

Update-FileLines -Path (Join-Path $complianceDir 'OdfSchemaPatternValidator.cs') -Transform {
    param($lines)
    $lines | ForEach-Object {
        $_ -replace 'var context = new OdfSchemaPatternMatchContext\(schema\);', 'var context = new OdfSchemaPatternMatchContext(schema);'
    }
}

$attributeFiles = @(
    'OdfSchemaPatternValidator.Attributes.Matching.cs',
    'OdfSchemaPatternValidator.Attributes.Validation.cs'
)
foreach ($name in $attributeFiles) {
    $path = Join-Path $complianceDir $name
    Update-FileLines -Path $path -Transform {
        param($lines)
        $lines | ForEach-Object {
            $_ = $_ -replace 'public static partial class OdfSchemaPatternValidator', 'internal static partial class OdfSchemaPatternAttributeMatcher'
            $_ = $_ -replace 'MatchAttributeValueNodes\(', 'OdfSchemaPatternValidator.MatchAttributeValueNodes('
            $_ = $_ -replace 'MatchesNameClassNode\(', 'OdfSchemaPatternValidator.MatchesNameClassNode('
            $_
        }
    }
}

$contentFiles = @(
    'OdfSchemaPatternValidator.Content.Sequence.cs',
    'OdfSchemaPatternValidator.Content.Repetition.cs',
    'OdfSchemaPatternValidator.Content.Interleave.cs'
)
foreach ($name in $contentFiles) {
    $path = Join-Path $complianceDir $name
    Update-FileLines -Path $path -Transform {
        param($lines)
        $lines | ForEach-Object {
            $_ = $_ -replace 'public static partial class OdfSchemaPatternValidator', 'internal static partial class OdfSchemaPatternContentMatcher'
            $_ = $_ -replace 'IsSimpleTextNode\(', 'OdfSchemaPatternValidator.IsSimpleTextNode('
            $_ = $_ -replace 'MatchesDataValue\(', 'OdfSchemaPatternValidator.MatchesDataValue('
            $_ = $_ -replace 'MatchesLiteralValue\(', 'OdfSchemaPatternValidator.MatchesLiteralValue('
            $_ = $_ -replace 'MatchesListValue\(', 'OdfSchemaPatternValidator.MatchesListValue('
            $_ = $_ -replace 'MatchesElementNode\(', 'OdfSchemaPatternValidator.MatchesElementNode('
            $_ = $_ -replace 'MatchesAttributeNode\(', 'OdfSchemaPatternAttributeMatcher.MatchesAttributeNode('
            $_
        }
    }
}

$elementPath = Join-Path $complianceDir 'OdfSchemaPatternValidator.ElementMatching.cs'
$elementLines = Get-Content -LiteralPath $elementPath -Encoding UTF8
$moveStart = ($elementLines | Select-String -Pattern 'private static bool ContentAllowsDirectText\(' | Select-Object -First 1).LineNumber - 1
$moveEnd = ($elementLines | Select-String -Pattern 'private static bool MatchesReference\(' | Select-Object -First 1).LineNumber - 2
$movedBlock = $elementLines[$moveStart..$moveEnd]
$newElementLines = [System.Collections.Generic.List[string]]::new()
$newElementLines.AddRange($elementLines[0..($moveStart - 1)])
$newElementLines.AddRange($elementLines[($moveEnd + 1)..($elementLines.Count - 1)])

$replacements = @{
    'MatchContentNode\(' = 'OdfSchemaPatternContentMatcher.MatchContentNode('
    'GetAttributeNodes\(' = 'OdfSchemaPatternAttributeMatcher.GetAttributeNodes('
    'MatchesAttributePatterns\(' = 'OdfSchemaPatternAttributeMatcher.MatchesAttributePatterns('
    'IsAttributeNameClassPattern\(' = 'OdfSchemaPatternAttributeMatcher.IsAttributeNameClassPattern('
    'StripAttributePatterns\(' = 'OdfSchemaPatternAttributeMatcher.StripAttributePatterns('
    'ContentAllowsDirectText\(' = 'OdfSchemaPatternContentMatcher.ContentAllowsDirectText('
    'MatchSequence\(' = 'OdfSchemaPatternContentMatcher.MatchSequence('
    'MatchesNameClassNode\(' = 'OdfSchemaPatternValidator.MatchesNameClassNode('
}
for ($i = 0; $i -lt $newElementLines.Count; $i++) {
    $line = $newElementLines[$i]
    foreach ($key in $replacements.Keys) {
        $line = $line -replace $key, $replacements[$key]
    }
    $newElementLines[$i] = $line
}
Set-Content -LiteralPath $elementPath -Value $newElementLines -Encoding UTF8

$repetitionPath = Join-Path $complianceDir 'OdfSchemaPatternValidator.Content.Repetition.cs'
$repLines = Get-Content -LiteralPath $repetitionPath -Encoding UTF8
$repOut = [System.Collections.Generic.List[string]]::new()
$inserted = $false
foreach ($line in $repLines) {
    if (-not $inserted -and $line -match '#region Content Matching - Repetition') {
        $repOut.Add($line)
        $repOut.Add('')
        foreach ($movedLine in $movedBlock) {
            $ml = $movedLine -replace 'private static', 'internal static'
            $ml = $ml -replace 'ContentAllowsDirectText\(', 'ContentAllowsDirectText('
            $ml = $ml -replace 'ReferenceAllowsDirectText\(', 'ReferenceAllowsDirectText('
            $repOut.Add($ml)
        }
        $repOut.Add('')
        $inserted = $true
        continue
    }
    $repOut.Add($line)
}
Set-Content -LiteralPath $repetitionPath -Value $repOut -Encoding UTF8

$internalizeMap = @{
    (Join-Path $complianceDir 'OdfSchemaPatternValidator.Attributes.Matching.cs') = @(
        'MatchesAttributePatterns', 'MatchesAttributeNode'
    )
    (Join-Path $complianceDir 'OdfSchemaPatternValidator.Attributes.Validation.cs') = @(
        'GetAttributeNodes', 'StripAttributePatterns', 'IsAttributeNameClassPattern'
    )
    (Join-Path $complianceDir 'OdfSchemaPatternValidator.Content.Sequence.cs') = @(
        'MatchSequence', 'MatchContentNode'
    )
    (Join-Path $complianceDir 'OdfSchemaPatternValidator.Content.Repetition.cs') = @(
        'ContentAllowsDirectText', 'ReferenceAllowsDirectText'
    )
    (Join-Path $complianceDir 'OdfSchemaPatternValidator.ElementMatching.cs') = @(
        'MatchesElementNode'
    )
    (Join-Path $complianceDir 'OdfSchemaPatternValidator.NameClasses.cs') = @(
        'MatchAttributeValueNodes', 'MatchesNameClassNode', 'IsSimpleTextNode', 'MatchesListValue'
    )
    (Join-Path $complianceDir 'OdfSchemaPatternValidator.DataTypes.Matching.cs') = @(
        'MatchesDataValue', 'MatchesLiteralValue'
    )
}
foreach ($entry in $internalizeMap.GetEnumerator()) {
    Update-FileLines -Path $entry.Key -Transform {
        param($lines)
        $methodNames = $entry.Value
        $lines | ForEach-Object {
            $line = $_
            foreach ($method in $methodNames) {
                $line = $line -replace "private static .*\b$method\b", { param($m) $m.Value -replace 'private static', 'internal static' }
                if ($line -match "private static .*\b$method\b") {
                    $line = $line -replace 'private static', 'internal static'
                }
            }
            $line
        }
    }
}

$facetsPath = Join-Path $complianceDir 'OdfSchemaPatternValidator.DataTypes.Facets.cs'
$facetsLines = Get-Content -LiteralPath $facetsPath -Encoding UTF8
$trimEnd = ($facetsLines | Select-String -Pattern 'private sealed class OdfSchemaPatternMatchContext' | Select-Object -First 1).LineNumber - 2
if ($trimEnd -lt 0) {
    $trimEnd = ($facetsLines | Select-String -Pattern 'private sealed class MatchContext' | Select-Object -First 1).LineNumber - 2
}
if ($trimEnd -ge 0) {
    Set-Content -LiteralPath $facetsPath -Value $facetsLines[0..$trimEnd] -Encoding UTF8
}

$hubFiles = @{
    'OdfSchemaPatternAttributeMatcher.cs' = '屬性模式比對子驗證器（聚合根）。'
    'OdfSchemaPatternContentMatcher.cs' = '內容模式比對子驗證器（聚合根）。'
}
foreach ($entry in $hubFiles.GetEnumerator()) {
    $hubPath = Join-Path $complianceDir $entry.Key
    if (-not (Test-Path -LiteralPath $hubPath)) {
        @(
            'using System;',
            '',
            'namespace OdfKit.Compliance;',
            '',
            '/// <summary>',
            "/// $($entry.Value)",
            '/// </summary>',
            "internal static partial class $($entry.Key.Replace('.cs',''))",
            '{',
            '}'
        ) | Set-Content -LiteralPath $hubPath -Encoding UTF8
    }
}

Write-Host 'Migrate-OdfSchemaPatternSubValidators 完成。'