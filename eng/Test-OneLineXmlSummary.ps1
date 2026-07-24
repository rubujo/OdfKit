#Requires -Version 7.0
<#
.SYNOPSIS
    掃描手寫 C# 是否含一行式 XML summary（專案禁止）。
.DESCRIPTION
    比對 `/// <summary>…</summary>` 寫在同一行的模式。預設僅 report；
    加上 -FailOnIssues 時若有命中則 exit 1。略過 bin/obj/Generated。
.PARAMETER FailOnIssues
    發現任一命中時以非零結束碼失敗。
#>
[CmdletBinding()]
param(
    [switch]$FailOnIssues
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pattern = '///\s*<summary>.+</summary>'
$roots = @(
    (Join-Path $repoRoot 'OdfKit'),
    (Join-Path $repoRoot 'OdfKit.Extensions.Html'),
    (Join-Path $repoRoot 'OdfKit.Extensions.Imaging'),
    (Join-Path $repoRoot 'OdfKit.Extensions.Ooxml'),
    (Join-Path $repoRoot 'OdfKit.Extensions.Pdf'),
    (Join-Path $repoRoot 'OdfKit.Extensions.Rdf'),
    (Join-Path $repoRoot 'OdfKit.Extensions.Rendering'),
    (Join-Path $repoRoot 'OdfKit.Extensions.Collaboration'),
    (Join-Path $repoRoot 'OdfKit.Extensions.Scripting'),
    (Join-Path $repoRoot 'OdfKit.Extensions.Html.WebFonts'),
    (Join-Path $repoRoot 'OdfKit.WebFonts.Abstractions'),
    (Join-Path $repoRoot 'OdfKit.WebFonts.Build'),
    (Join-Path $repoRoot 'OdfKit.WebFonts.Data.SqlServer'),
    (Join-Path $repoRoot 'OdfKit.WebFonts.Encoding.Legacy'),
    (Join-Path $repoRoot 'OdfKit.WebFonts.Hosting.AspNetCore'),
    (Join-Path $repoRoot 'OdfKit.WebFonts.Hosting.SystemWeb'),
    (Join-Path $repoRoot 'OdfKit.WebFonts.OpenType'),
    (Join-Path $repoRoot 'OdfKit.WebFonts.Profiles'),
    (Join-Path $repoRoot 'OdfKit.WebFonts.Windows'),
    (Join-Path $repoRoot 'OdfKit.WebFonts.Worker'),
    (Join-Path $repoRoot 'OdfKit.WebFonts.Sidecar'),
    (Join-Path $repoRoot 'tools/OdfKit.Cli')
)

$hits = @()
foreach ($root in $roots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    Get-ChildItem -LiteralPath $root -Recurse -Filter '*.cs' -File |
        Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj|Generated)[\\/]' -and
            $_.Name -notlike '*.g.cs'
        } |
        ForEach-Object {
            Select-String -LiteralPath $_.FullName -Pattern $pattern |
                ForEach-Object {
                    $hits += [pscustomobject]@{
                        Path = $_.Path.Substring($repoRoot.Length).TrimStart('\', '/')
                        Line = $_.LineNumber
                        Text = $_.Line.Trim()
                    }
                }
        }
}

if ($hits.Count -eq 0) {
    Write-Host 'OK：未發現一行式 <summary>。'
    exit 0
}

Write-Host "發現 $($hits.Count) 處一行式 <summary>："
$hits | ForEach-Object { Write-Host ("  {0}:{1}: {2}" -f $_.Path, $_.Line, $_.Text) }

if ($FailOnIssues) {
    exit 1
}
exit 0
