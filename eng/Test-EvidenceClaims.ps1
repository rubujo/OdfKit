#Requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root 'docs/claims.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 2) { throw '不支援的 claims schemaVersion。' }
$ids = @{}
$levelsByDimension = @{
    PackageFidelity = @('round-trip-verified')
    SemanticApiDepth = @('semantic-contract-verified')
    InteropEvidence = @('interop-tested')
}
foreach ($claim in $manifest.claims) {
    if ([string]::IsNullOrWhiteSpace($claim.id)) { throw 'claim 缺少 id。' }
    if ($ids.ContainsKey($claim.id)) { throw "claim id 重複：$($claim.id)" }
    $ids[$claim.id] = $true
    if ($claim.dimension -notin @('PackageFidelity', 'SemanticApiDepth', 'InteropEvidence')) {
        throw "claim dimension 無效：$($claim.id)"
    }
    if ($claim.level -notin $levelsByDimension[$claim.dimension]) {
        throw "claim level 與 dimension 不相符：$($claim.id) -> $($claim.level)"
    }
    if (@($claim.evidence).Count -eq 0) { throw "claim 缺少證據：$($claim.id)" }
    foreach ($path in $claim.evidence) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $path))) {
            throw "claim 證據不存在：$($claim.id) -> $path"
        }
    }
    if ([string]::IsNullOrWhiteSpace($claim.limitations)) { throw "claim 缺少限制：$($claim.id)" }
}
Write-Host "Evidence claims 驗證成功：$($manifest.claims.Count) claims。"
