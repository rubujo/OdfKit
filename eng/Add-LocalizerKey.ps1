#Requires -Version 7.0
<#
.SYNOPSIS
    於 17 語系 exceptions JSON 同步新增一則訊息鍵，並重產 C# 字典。
.DESCRIPTION
    v0.0.1 在地化產線：編輯 `OdfKit/Compliance/i18n/exceptions.*.json`，
    再以 eng/Generate-LocalizerExceptionsFromJson.ps1 產生 .cs。

.PARAMETER Key
    訊息鍵（Err_*／Warn_*／Cli_*／Diag_*／Rule_*）。
.PARAMETER EnMessage
    英文訊息。
.PARAMETER ZhTwMessage
    正體中文（臺灣）訊息；省略時暫用英文。
.PARAMETER JaMessage
    日文訊息。
.PARAMETER EsMessage
    西班牙文訊息。
.PARAMETER CsMessage
    捷克文訊息。
.PARAMETER PlMessage
    波蘭文訊息。
.PARAMETER PtBrMessage
    巴西葡萄牙文訊息。
.EXAMPLE
    pwsh eng/Add-LocalizerKey.ps1 -Key Err_Example_Failed -EnMessage "Failed: {0}." -ZhTwMessage "失敗：{0}。"
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(Err|Warn|Cli|Diag|Rule)_[A-Za-z0-9_]+$')]
    [string]$Key,

    [Parameter(Mandatory = $true)]
    [string]$EnMessage,

    [string]$ZhTwMessage,

    [Parameter(Mandatory = $true)]
    [string]$JaMessage,

    [Parameter(Mandatory = $true)]
    [string]$EsMessage,

    [Parameter(Mandatory = $true)]
    [string]$CsMessage,

    [Parameter(Mandatory = $true)]
    [string]$PlMessage,

    [Parameter(Mandatory = $true)]
    [string]$PtBrMessage
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$i18nDir = Join-Path $repoRoot 'OdfKit/Compliance/i18n'
$cultures = @('en', 'zh-TW', 'da', 'de', 'fr', 'it', 'ko', 'ms', 'nb', 'nl', 'pt', 'sk', 'ja', 'es', 'cs', 'pl', 'pt-BR')
$utf8Bom = New-Object System.Text.UTF8Encoding $true

if ([string]::IsNullOrWhiteSpace($ZhTwMessage)) {
    $ZhTwMessage = $EnMessage
}

function Get-MessageForCulture {
    param([string]$Culture)
    switch ($Culture) {
        'en' { return $EnMessage }
        'zh-TW' { return $ZhTwMessage }
        'ja' { return $JaMessage }
        'es' { return $EsMessage }
        'cs' { return $CsMessage }
        'pl' { return $PlMessage }
        'pt-BR' { return $PtBrMessage }
        default { return $EnMessage }
    }
}

function Read-OrderedJson {
    param([string]$Path)
    $raw = [System.IO.File]::ReadAllText($Path)
    $obj = $raw | ConvertFrom-Json
    $map = [ordered]@{}
    foreach ($prop in $obj.PSObject.Properties) {
        $map[$prop.Name] = [string]$prop.Value
    }
    return $map
}

function Write-OrderedJson {
    param(
        [string]$Path,
        [System.Collections.Specialized.OrderedDictionary]$Map
    )
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('{')
    $keys = @($Map.Keys)
    for ($i = 0; $i -lt $keys.Count; $i++) {
        $k = $keys[$i]
        $kJson = ConvertTo-Json -InputObject $k
        $vJson = ConvertTo-Json -InputObject ([string]$Map[$k])
        $comma = if ($i -lt $keys.Count - 1) { ',' } else { '' }
        [void]$sb.AppendLine("  ${kJson}: ${vJson}${comma}")
    }
    [void]$sb.AppendLine('}')
    [System.IO.File]::WriteAllText($Path, $sb.ToString(), $utf8Bom)
}

$updated = 0
foreach ($culture in $cultures) {
    $jsonPath = Join-Path $i18nDir "exceptions.$culture.json"
    if (-not (Test-Path -LiteralPath $jsonPath)) {
        throw "缺少 JSON：$jsonPath"
    }

    $map = Read-OrderedJson -Path $jsonPath
    if ($map.Contains($Key)) {
        Write-Host "SKIP  $culture（鍵已存在於 JSON）"
        continue
    }

    if (-not $PSCmdlet.ShouldProcess($jsonPath, "Add key $Key")) {
        Write-Host "SKIP  $culture（-WhatIf 或未確認）"
        continue
    }

    $map[$Key] = Get-MessageForCulture -Culture $culture
    Write-OrderedJson -Path $jsonPath -Map $map
    Write-Host "OK    $culture JSON"
    $updated++
}

if ($updated -eq 0) {
    Write-Host '無 JSON 變更（鍵可能已存在）。'
    exit 0
}

if ($PSCmdlet.ShouldProcess('Generate-LocalizerExceptionsFromJson.ps1', 'Regenerate C# dictionaries')) {
    & (Join-Path $PSScriptRoot 'Generate-LocalizerExceptionsFromJson.ps1')
    if (-not $?) {
        throw "重產 C# 失敗。"
    }
}

Write-Host ''
Write-Host "完成：更新 $updated 個 JSON 並重產 C#。請潤飾非 en／zh-TW 後再提交。"
Write-Host '  pwsh eng/Test-LocalizerKeyParity.ps1 -FailOnIssues'
