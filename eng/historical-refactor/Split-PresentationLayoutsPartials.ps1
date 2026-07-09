#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$presDir = Join-Path $PSScriptRoot '..\OdfKit\Presentation'
$sourcePath = Join-Path $presDir 'PresentationLayouts.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

$typeBlocks = @(
    @{ Start = 10; End = 89; File = 'OdfPlaceholderType.cs' }
    @{ Start = 91; End = 224; File = 'OdfPlaceholderTemplate.cs' }
    @{ Start = 226; End = 309; File = 'OdfPresentationPageLayout.cs' }
    @{ Start = 311; End = 334; File = 'OdfPlaceholder.cs' }
    @{ Start = 336; End = $lineCount; File = 'OdfPresentationLayout.cs' }
)

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    [void]$needed.Add('OdfKit.DOM')
    if ($Text -match 'List<|Dictionary<|HashSet<') { [void]$needed.Add('System.Collections.Generic') }
    if ($Text -match 'OdfLength|OdfStyle|OdfColor') { [void]$needed.Add('OdfKit.Styles') }
    if ($Text -match 'OdfNamespaces|OdfNode|OdfPackage') { [void]$needed.Add('OdfKit.Core') }

    $order = @('System', 'System.Collections.Generic', 'OdfKit.Core', 'OdfKit.DOM', 'OdfKit.Styles')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    if ($Text -match 'OdfNamespaces\s*=') { $result += 'using OdfNamespaces = OdfKit.Core.OdfNamespaces;' }
    return $result
}

function Write-TypeFile {
    param([string]$Path, [string[]]$BodyLines)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Presentation;')
    $out.Add('')
    foreach ($line in $BodyLines) { $out.Add($line) }
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

foreach ($block in $typeBlocks) {
    $body = $lines[($block.Start - 1)..($block.End - 1)]
    $path = Join-Path $presDir $block.File
    Write-TypeFile -Path $path -BodyLines $body
    Write-Host "  $($block.File): $($body.Count) body lines"
}

Remove-Item -Path $sourcePath -Force
Write-Host 'Done.'