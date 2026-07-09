#Requires -Version 7.0
<#
.SYNOPSIS
    於 12 語系 OdfLocalizer.Exceptions.*.cs 同步新增一則訊息鍵（腳手架）。
.DESCRIPTION
    業界在地化 DX 慣例：新增鍵時一次寫入所有語系表，避免只補 en／zh-TW。
    其他語系預設填入 -EnMessage（可後續潤飾）；zh-TW 使用 -ZhTwMessage（若省略則同英文）。

.PARAMETER Key
    訊息鍵，建議 Err_類別_簡稱／Warn_*／Cli_*／Diag_*。
.PARAMETER EnMessage
    英文訊息（可含 {0} 格式化預留位置）。
.PARAMETER ZhTwMessage
    正體中文（臺灣）訊息；省略時暫用英文。
.EXAMPLE
    pwsh eng/Add-LocalizerKey.ps1 `
      -Key Err_OdfPackage_Example `
      -EnMessage "Example failed: {0}." `
      -ZhTwMessage "範例失敗：{0}。"

.EXAMPLE
    # 僅預覽（PowerShell 內建 -WhatIf，來自 SupportsShouldProcess）
    pwsh eng/Add-LocalizerKey.ps1 -Key Err_OdfPackage_Example -EnMessage "x" -WhatIf
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(Err|Warn|Cli|Diag|Rule)_[A-Za-z0-9_]+$')]
    [string]$Key,

    [Parameter(Mandatory = $true)]
    [string]$EnMessage,

    [string]$ZhTwMessage
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$complianceDir = Join-Path $repoRoot 'OdfKit/Compliance'
$cultures = @('en', 'zh-TW', 'de', 'fr', 'nl', 'nb', 'pt', 'it', 'sk', 'da', 'ms', 'ko')

if ([string]::IsNullOrWhiteSpace($ZhTwMessage)) {
    $ZhTwMessage = $EnMessage
}

function Escape-CsString {
    param([string]$Value)
    return $Value.Replace('\', '\\').Replace('"', '\"')
}

function Get-MessageForCulture {
    param([string]$Culture)
    switch ($Culture) {
        'en' { return $EnMessage }
        'zh-TW' { return $ZhTwMessage }
        default { return $EnMessage }
    }
}

$utf8Bom = New-Object System.Text.UTF8Encoding $true
$updated = 0

foreach ($culture in $cultures) {
    $path = Join-Path $complianceDir "OdfLocalizer.Exceptions.$culture.cs"
    if (-not (Test-Path -LiteralPath $path)) {
        throw "缺少語系檔：$path"
    }

    $text = [System.IO.File]::ReadAllText($path)
    if ($text -match [regex]::Escape("[`"$Key`"]")) {
        Write-Host "SKIP  $culture（鍵已存在）"
        continue
    }

    $message = Escape-CsString (Get-MessageForCulture -Culture $culture)
    $entry = "            [`"$Key`"] = `"$message`","

    # 插入於字典初始化區塊結尾（最後一個 ]; 之前的最後一個項目後）
    # 以 map["culture"] = new(...) { ... }; 結構為準：找最後一個 "], 後接空白與 }; 的近鄰
    $pattern = '(?ms)(map\["' + [regex]::Escape($culture) + '"\]\s*=\s*new[^{]*\{)(.*?)(\r?\n\s*\};)'
    $m = [regex]::Match($text, $pattern)
    if (-not $m.Success) {
        throw "無法定位 $culture 字典初始化區塊：$path"
    }

    $body = $m.Groups[2].Value
    if ($body.TrimEnd() -notmatch ',\s*$') {
        # 確保前一列以逗號結尾（C# collection initializer 允許最後一項無逗號，但我們統一加）
        $bodyTrim = $body.TrimEnd()
        if ($bodyTrim.Length -gt 0 -and -not $bodyTrim.EndsWith(',')) {
            $body = $bodyTrim + ',' + ($body.Substring($bodyTrim.Length))
        }
    }

    $newBody = $body.TrimEnd() + "`r`n" + $entry + "`r`n"
    $newText = $text.Substring(0, $m.Groups[2].Index) + $newBody + $text.Substring($m.Groups[2].Index + $m.Groups[2].Length)

    if (-not $PSCmdlet.ShouldProcess($path, "Add key $Key")) {
        Write-Host "SKIP  $culture（-WhatIf 或未確認）"
        continue
    }

    [System.IO.File]::WriteAllText($path, $newText, $utf8Bom)
    Write-Host "OK    $culture"
    $updated++
}

Write-Host ""
Write-Host "完成：更新 $updated 個語系檔。建議接著執行："
Write-Host "  pwsh eng/Test-LocalizerKeyParity.ps1 -FailOnIssues"
Write-Host "  並將非 en／zh-TW 的訊息潤飾為目標語言（目前預設暫用英文）。"
