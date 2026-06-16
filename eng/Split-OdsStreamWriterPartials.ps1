#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$sheetDir = Join-Path $PSScriptRoot '..\OdfKit\Spreadsheet'
$sourcePath = Join-Path $sheetDir 'OdsStreamWriter.cs'
$lines = [System.Collections.Generic.List[string]]@(Get-Content -Path $sourcePath -Encoding UTF8)
$lineCount = $lines.Count

function Get-UsingsForBlock {
    param([string]$Text)
    $needed = [System.Collections.Generic.HashSet[string]]::new()
    [void]$needed.Add('System')
    if ($Text -match 'Stream|MemoryStream|ZipArchive') { [void]$needed.Add('System.IO') }
    if ($Text -match 'ZipArchive|Compression') { [void]$needed.Add('System.IO.Compression') }
    if ($Text -match 'StringBuilder|Encoding\.|UTF8Encoding') { [void]$needed.Add('System.Text') }
    if ($Text -match 'XmlWriter|XmlReader') { [void]$needed.Add('System.Xml') }
    if ($Text -match 'OdfVersion|OdfCompliance') { [void]$needed.Add('OdfKit.Compliance') }
    if ($Text -match 'OdfNamespaces|OdfPackage') { [void]$needed.Add('OdfKit.Core') }
    if ($Text -match 'OdfLength|OdfStyle') { [void]$needed.Add('OdfKit.Styles') }

    $order = @('System', 'System.IO', 'System.IO.Compression', 'System.Text', 'System.Xml', 'OdfKit.Compliance', 'OdfKit.Core', 'OdfKit.Styles')
    $result = @()
    foreach ($u in $order) {
        if ($needed.Contains($u)) { $result += "using $u;" }
    }
    return $result
}

function Write-PartialFile {
    param([string]$Path, [string]$RegionName, [string[]]$BodyLines, [switch]$IncludeClassDoc)
    $out = [System.Collections.Generic.List[string]]::new()
    $text = $BodyLines -join "`n"
    foreach ($usingLine in (Get-UsingsForBlock -Text $text)) { $out.Add($usingLine) }
    $out.Add('')
    $out.Add('namespace OdfKit.Spreadsheet;')
    $out.Add('')
    if ($IncludeClassDoc) {
        $out.Add('/// <summary>')
        $out.Add('/// 提供以資料流方式寫入 ODS 試算表文件的功能，以支援高效能、低記憶體耗用的寫入作業。')
        $out.Add('/// </summary>')
    }
    $out.Add('public partial class OdsStreamWriter')
    $out.Add('{')
    if ($RegionName) {
        $out.Add("    #region $RegionName")
        $out.Add('')
    }
    foreach ($line in $BodyLines) { $out.Add($line) }
    if ($RegionName) {
        $out.Add('')
        $out.Add('    #endregion')
    }
    $out.Add('}')
    Set-Content -Path $Path -Value $out -Encoding UTF8
}

# Core: class fields, ctor, public sheet API, dispose (lines 15-301, 420-461)
$coreBody = $lines[14..300] + $lines[419..460]
$coreBody = $coreBody | ForEach-Object { $_ -replace '^public class OdsStreamWriter', 'public partial class OdsStreamWriter' }
Write-PartialFile -Path $sourcePath -RegionName 'Stream Writing' -BodyLines $coreBody -IncludeClassDoc
Write-Host "Core OdsStreamWriter.cs: $($coreBody.Count) body lines"

$packageBody = $lines[302..417]
Write-PartialFile -Path (Join-Path $sheetDir 'OdsStreamWriter.PackageEntries.cs') -RegionName 'Package Entries' -BodyLines $packageBody
Write-Host "  OdsStreamWriter.PackageEntries.cs: $($packageBody.Count) body lines"

$wrapperBody = $lines[463..($lineCount - 1)]
$wrapperOut = [System.Collections.Generic.List[string]]::new()
$wrapperOut.Add('using System;')
$wrapperOut.Add('using System.IO;')
$wrapperOut.Add('')
$wrapperOut.Add('namespace OdfKit.Spreadsheet;')
$wrapperOut.Add('')
$wrapperOut.AddRange($wrapperBody)
Set-Content -Path (Join-Path $sheetDir 'NonSeekableStreamWrapper.cs') -Value $wrapperOut -Encoding UTF8
Write-Host "  NonSeekableStreamWrapper.cs: $($wrapperBody.Count) body lines"
Write-Host 'Done.'