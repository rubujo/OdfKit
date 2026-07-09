#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$textDir = Join-Path $PSScriptRoot '..\OdfKit\Text'
$sourcePath = Join-Path $textDir 'TextDocument.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)

$regionMap = [ordered]@{
    'Document Elements Addition API'      = 'TextDocument.Elements.cs'
    'Page Setup & Mirrored Layouts'       = 'TextDocument.PageSetup.cs'
    'TOC (Table of Contents)'             = 'TextDocument.Toc.cs'
    'Search & Replace with Actions/Regex' = 'TextDocument.SearchReplace.cs'
    'MailMerge Implementation'            = 'TextDocument.MailMerge.cs'
    'Mathematical Formulas (MathML)'      = 'TextDocument.MathFormula.cs'
    'CJK Font Fallback'                   = 'TextDocument.CjkFontFallback.cs'
    'Comments / Annotations'              = 'TextDocument.Comments.cs'
    'Dynamic Page / Field Indicators'     = 'TextDocument.FieldIndicators.cs'
    'Multi-Column Sections Layouts'       = 'TextDocument.Sections.cs'
    'Tracked Changes (Accept/Reject)'     = 'TextDocument.TrackChanges.cs'
    'HTML Fragment Parsing'               = 'TextDocument.HtmlFragment.cs'
    'Table covered cells omissions'       = 'TextDocument.TableCoveredCells.cs'
    'Document Merging Logic Override'     = 'TextDocument.Merge.cs'
    'XML Helper'                          = 'TextDocument.XmlHelpers.cs'
    '表單控制項（Form Controls）'         = 'TextDocument.FormControls.cs'
}

$nestedTypeFiles = @(
    @{ Start = 2526; File = 'OdfPageEnums.cs' }
    @{ Start = 2554; File = 'OdfPageSetup.cs' }
    @{ Start = 2982; File = 'OdfPageStyle.cs' }
    @{ Start = 2993; File = 'OdfTextBody.cs' }
    @{ Start = 3060; File = 'OdfParagraphCollection.cs' }
    @{ Start = 3123; File = 'OdfHeadingCollection.cs' }
    @{ Start = 3187; File = 'OdfListCollection.cs' }
    @{ Start = 3250; File = 'OdfTextTableCollection.cs' }
    @{ Start = 3314; File = 'OdfTextImageCollection.cs' }
    @{ Start = 3414; File = 'OdfTextTableInfo.cs' }
    @{ Start = 3473; File = 'OdfDocumentMetadata.cs' }
    @{ Start = 3536; File = 'OdfParagraph.cs' }
    @{ Start = 3997; File = 'OdfTextLayoutEnums.cs' }
    @{ Start = 4054; File = 'OdfFloatingTextBox.cs' }
    @{ Start = 4082; File = 'OdfHeading.cs' }
    @{ Start = 4099; File = 'OdfTextRun.cs' }
    @{ Start = 4313; File = 'OdfSection.cs' }
    @{ Start = 4394; File = 'OdfTable.cs' }
    @{ Start = 4632; File = 'OdfListEnums.cs' }
    @{ Start = 4666; File = 'OdfList.cs' }
    @{ Start = 4839; File = 'OdfListItem.cs' }
    @{ Start = 4920; File = 'OdfImage.cs' }
    @{ Start = 5065; File = 'OdfTableCell.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'Regex|MatchCollection') { [void]$needed.Add('System.Text.RegularExpressions') }
    if ($Text -match 'Task\.|CancellationToken') {
        [void]$needed.Add('System.Threading')
        [void]$needed.Add('System.Threading.Tasks')
    }
    if ($Text -match 'Stream|MemoryStream|File\.|Path\.') { [void]$needed.Add('System.IO') }
    if ($Text -match 'IEnumerable|ICollection|IEnumerator') { [void]$needed.Add('System.Collections') }
    if ($Text -match 'List<|Dictionary<|HashSet<|IReadOnlyList<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'XDocument|XElement|XAttribute') { [void]$needed.Add('System.Xml.Linq') }
    if ($Text -match 'Color\.|System\.Drawing|PointF') { [void]$needed.Add('System.Drawing') }
    if ($Text -match 'OdfCompliance|OdfValidator|OdfVersion') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfPackage|OdfDocument|OdfNode|OdfNamespaces|OdfDocumentFactory|OdfDocumentKind') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfFormControl') { [void]$needed.Add('OdfKit.Forms') }
    if ($Text -match 'OdfLength|OdfStyle|StyleEngine|OdfWritingMode|OdfColor|OdfBorder') { [void]$needed.Add('OdfKit.Styles') }
    if ($Text -match 'OdfFormulaObject|FormulaDocument') { [void]$needed.Add('OdfKit.Formula') }

    $order = @(
        'System', 'System.Collections', 'System.Collections.Generic', 'System.Drawing', 'System.IO',
        'System.Text.RegularExpressions', 'System.Threading', 'System.Threading.Tasks', 'System.Xml.Linq',
        'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Formula', 'OdfKit.Forms', 'OdfKit.Styles'
    )
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-FileWithUsings {
    param(
        [string]$Path,
        [string[]]$BodyLines,
        [switch]$SkipUsings
    )
    $out = [System.Collections.Generic.List[string]]::new()
    if (-not $SkipUsings) {
        $text = $BodyLines -join "`n"
        foreach ($usingLine in (Get-UsingsForBlock -Text $text)) {
            $out.Add($usingLine)
        }
        $out.Add('')
    }
    $out.AddRange($BodyLines)
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

# --- Core: keep original usings (lines 1 to first blank before namespace) ---
$nsIndex = 0
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^namespace ') { $nsIndex = $i; break }
}
$coreEnd = 0
for ($i = $nsIndex + 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\s*#region ') { $coreEnd = $i - 1; break }
}
$core = $lines[0..$coreEnd] | ForEach-Object {
    $_ -replace '^public class TextDocument', 'public partial class TextDocument'
}
$core += '}'
Write-FileWithUsings -Path $sourcePath -BodyLines $core -SkipUsings
Write-Host "Core TextDocument.cs: $($core.Count) lines"

# --- Region partials ---
$classBody = $lines[($coreEnd + 1)..2522]
$currentRegion = $null
$currentLines = [System.Collections.Generic.List[string]]::new()

function Flush-Region {
    if ($null -eq $script:currentRegion) { return }
    $fileName = $regionMap[$script:currentRegion]
    if (-not $fileName) { throw "Unknown region: $($script:currentRegion)" }
    $body = @(
        'namespace OdfKit.Text;',
        '',
        'public partial class TextDocument',
        '{',
        "    #region $($script:currentRegion)",
        ''
    )
    $body += $script:currentLines
    $body += @('', "    #endregion", '}')
    Write-FileWithUsings -Path (Join-Path $textDir $fileName) -BodyLines $body
    Write-Host "  $($fileName): $($script:currentLines.Count) body lines"
    $script:currentLines.Clear()
}

foreach ($line in $classBody) {
    if ($line -match '^\s*#region\s+(.+)$') {
        Flush-Region
        $currentRegion = $Matches[1].Trim()
        continue
    }
    if ($line -match '^\s*#endregion') {
        Flush-Region
        $currentRegion = $null
        continue
    }
    if ($null -ne $currentRegion) {
        $currentLines.Add($line)
    }
}
Flush-Region

# --- Nested types ---
for ($i = 0; $i -lt $nestedTypeFiles.Count; $i++) {
    $start = $nestedTypeFiles[$i].Start - 1
    $end = if ($i + 1 -lt $nestedTypeFiles.Count) { $nestedTypeFiles[$i + 1].Start - 2 } else { $lines.Count - 1 }
    $fileName = $nestedTypeFiles[$i].File
    $block = $lines[$start..$end]
    while ($block.Count -gt 0 -and [string]::IsNullOrWhiteSpace($block[-1])) {
        $block = $block[0..($block.Count - 2)]
    }
    $body = @('namespace OdfKit.Text;', '') + $block
    Write-FileWithUsings -Path (Join-Path $textDir $fileName) -BodyLines $body
    Write-Host "Nested $fileName : $($block.Count) lines"
}

Write-Host 'Done.'