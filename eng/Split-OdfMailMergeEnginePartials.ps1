#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$textDir = Join-Path $PSScriptRoot '..\OdfKit\Text'
$sourcePath = Join-Path $textDir 'OdfMailMergeEngine.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('System.Collections')
    [void]$needed.Add('System.Collections.Generic')
    [void]$needed.Add('System.Reflection')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    [void]$needed.Add('OdfKit.Formula')

    $order = @('System', 'System.Collections', 'System.Collections.Generic', 'System.Reflection', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Formula')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    foreach ($usingLine in (Get-UsingsForBlock -Text ($BodyLines -join "`n"))) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Text;')
    $out.Add('')
    $out.Add('public partial class OdfMailMergeEngine')
    $out.Add('{')
    $out.Add("    #region $RegionName")
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    $out.Add('')
    $out.Add('    #endregion')
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

$coreHeader = @(
    'using System;',
    'using System.Collections;',
    'using System.Collections.Generic;',
    'using System.Reflection;',
    'using OdfKit.Core;',
    'using OdfKit.DOM;',
    'using OdfKit.Formula;',
    '',
    'namespace OdfKit.Text;',
    '',
    '/// <summary>',
    '/// 表示 ODF 文件的郵件合併引擎。',
    '/// </summary>',
    '/// <param name="doc">目標文字文件</param>',
    'public partial class OdfMailMergeEngine(TextDocument doc)',
    '{',
    '    private readonly TextDocument _doc = doc ?? throw new ArgumentNullException(nameof(doc));',
    '    private readonly Dictionary<(Type, string), PropertyInfo?> _propertyCache = [];',
    ''
)
$coreBody = $lines[19..168]
$core = $coreHeader + $coreBody + '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfMailMergeEngine.cs: $($core.Count) lines"

$placeholderBody = $lines[170..($lineCount - 2)]
Write-PartialFile -Path (Join-Path $textDir 'OdfMailMergeEngine.Placeholders.cs') -RegionName 'Placeholder Replacement' -BodyLines $placeholderBody
Write-Host "  OdfMailMergeEngine.Placeholders.cs: $($placeholderBody.Count) body lines"
Write-Host 'Done.'