#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$drawDir = Join-Path $PSScriptRoot '..\OdfKit\Drawing'
$sourcePath = Join-Path $drawDir 'OdfDrawPage.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$blocks = @(
    @{ Start = 68; End = 377; File = 'OdfDrawPage.Creation.cs'; Region = 'Drawing Object Creation' }
    @{ Start = 379; End = 434; File = 'OdfDrawPage.Helpers.cs'; Region = 'Drawing Helpers' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.Core')
    [void]$needed.Add('OdfKit.DOM')
    [void]$needed.Add('OdfKit.Presentation')
    [void]$needed.Add('OdfKit.Styles')
    if ($Text -match 'List<|Dictionary<|HashSet<|IEnumerable<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'IEnumerator|IEnumerable') { [void]$needed.Add('System.Collections') }
    if ($Text -match 'PointF|System\.Drawing') { [void]$needed.Add('System.Drawing') }

    $order = @('System', 'System.Collections', 'System.Collections.Generic', 'System.Drawing', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Presentation', 'OdfKit.Styles')
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
    $out.Add('namespace OdfKit.Drawing;')
    $out.Add('')
    $out.Add('public partial class OdfDrawPage')
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
    'using System.Collections.Generic;',
    'using OdfKit.Core;',
    'using OdfKit.DOM;',
    'using OdfKit.Presentation;',
    'using OdfKit.Styles;',
    '',
    'namespace OdfKit.Drawing;',
    '',
    '/// <summary>',
    '/// 表示 ODF 繪圖頁面（Drawing Page）的類別。',
    '/// </summary>',
    '/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>',
    '/// <param name="doc">所屬的繪圖文件執行個體</param>',
    'public partial class OdfDrawPage(OdfNode node, DrawingDocument doc)',
    '{'
)
$coreBody = $lines[17..65]
$core = $coreHeader + $coreBody + '}'
Set-Content -Path $sourcePath -Value $core -Encoding UTF8
Write-Host "Core OdfDrawPage.cs: $($core.Count) lines"

foreach ($block in $blocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    Write-PartialFile -Path (Join-Path $drawDir $block.File) -RegionName $block.Region -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Write-Host 'Done.'