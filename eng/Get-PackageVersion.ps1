#Requires -Version 7.0
<#
.SYNOPSIS
    讀取 eng/OdfKit.Package.props 中的套件版本號。
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $repoRoot "eng/OdfKit.Package.props"
if (-not (Test-Path -LiteralPath $propsPath)) {
    throw "找不到 $propsPath"
}

$match = Select-String -Path $propsPath -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
if ($null -eq $match) {
    throw "無法從 OdfKit.Package.props 解析 <Version>"
}

return $match.Matches[0].Groups[1].Value