#Requires -Version 7.0
<#
.SYNOPSIS
    檢查 OdfLocalizer 例外字典在 12 語系之間的鍵值集合對等。
.DESCRIPTION
    業界在地化最佳實踐（gettext／ICU／.NET resx 閘門同理）：所有語系必須擁有相同的
    訊息鍵清單，禁止「只補 en／zh-TW」。以 en 為基準，比對其餘語系是否缺鍵或多餘鍵。

.PARAMETER FailOnIssues
    發現不一致時以非零結束碼退出（CI 用）。
#>
[CmdletBinding()]
param(
    [switch]$FailOnIssues
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$complianceDir = Join-Path $repoRoot 'OdfKit/Compliance'
$cultures = @('en', 'zh-TW', 'de', 'fr', 'nl', 'nb', 'pt', 'it', 'sk', 'da', 'ms', 'ko')
# 僅擷取訊息鍵（Err_／Warn_／Cli_／Diag_／Rule_），略過 map["en"] 等文化代碼鍵。
$keyPattern = [regex]'\["((?:Err|Warn|Cli|Diag|Rule)_[^"]+)"\]'

function Get-LocalizerKeys {
    param([string]$Path)
    $text = [System.IO.File]::ReadAllText($Path)
    $set = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($match in $keyPattern.Matches($text)) {
        [void]$set.Add($match.Groups[1].Value)
    }
    return $set
}

$sets = @{}
foreach ($culture in $cultures) {
    $path = Join-Path $complianceDir "OdfLocalizer.Exceptions.$culture.cs"
    if (-not (Test-Path -LiteralPath $path)) {
        throw "缺少語系例外字典：$path"
    }
    $sets[$culture] = Get-LocalizerKeys -Path $path
    Write-Host ("{0,-6} {1} keys" -f $culture, $sets[$culture].Count)
}

$baseline = $sets['en']
if ($baseline.Count -eq 0) {
    throw 'en 例外字典未解析到任何訊息鍵。'
}

$issues = [System.Collections.Generic.List[string]]::new()
foreach ($culture in $cultures) {
    if ($culture -eq 'en') { continue }
    $current = $sets[$culture]
    foreach ($key in ($baseline | Sort-Object)) {
        if (-not $current.Contains($key)) {
            $issues.Add("[$culture] 缺少鍵：$key")
        }
    }
    foreach ($key in ($current | Sort-Object)) {
        if (-not $baseline.Contains($key)) {
            $issues.Add("[$culture] 多餘鍵（en 無此鍵）：$key")
        }
    }
}

if ($issues.Count -eq 0) {
    Write-Host "PASS：12 語系訊息鍵與 en 完全對等（$($baseline.Count) keys）。"
    exit 0
}

Write-Host "FAIL：發現 $($issues.Count) 項鍵值對等問題："
$issues | Select-Object -First 50 | ForEach-Object { Write-Host "  $_" }
if ($issues.Count -gt 50) {
    Write-Host "  …另有 $($issues.Count - 50) 項未列出"
}

if ($FailOnIssues) {
    exit 1
}
exit 0
