#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root 'docs/semantic-coverage.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

if ($manifest.schemaVersion -ne 1) { throw '不支援的 semantic coverage schemaVersion。' }
if ($manifest.odfVersion -ne '1.4') { throw 'semantic coverage 必須以 ODF 1.4 為主模型。' }
if ($manifest.legacyVersionPolicy -ne 'normalize-to-1.4-preserve-unknown') {
    throw 'semantic coverage 的舊版本相容政策無效。'
}

$legacyEvidence = $manifest.legacyVersionEvidence
if (@($legacyEvidence.versions) -join ',' -ne '1.1,1.2,1.3') {
    throw 'semantic coverage 舊版本證據必須覆蓋 ODF 1.1～1.3。'
}
if (@($legacyEvidence.formats | Sort-Object) -join ',' -ne 'ODG,ODP,ODS,ODT') {
    throw 'semantic coverage 舊版本證據必須覆蓋四種主要格式。'
}
$legacyTestPath = Join-Path $root $legacyEvidence.test
if (-not (Test-Path -LiteralPath $legacyTestPath)) {
    throw 'semantic coverage 舊版本測試不存在。'
}
$legacyTestSource = Get-Content -LiteralPath $legacyTestPath -Raw
if (-not $legacyTestSource.Contains($legacyEvidence.symbol, [StringComparison]::Ordinal)) {
    throw 'semantic coverage 舊版本測試符號不存在。'
}

$requiredFormats = @('ODT', 'ODS', 'ODP', 'ODG')
$requiredOperations = @('Create', 'Get', 'Find', 'Set', 'Update', 'Remove', 'Clear', 'RoundTrip', 'Interop')
$ids = @{}
foreach ($family in @($manifest.families)) {
    if ([string]::IsNullOrWhiteSpace($family.id)) { throw '語意族群缺少 id。' }
    if ($ids.ContainsKey($family.id)) { throw "語意族群 id 重複：$($family.id)" }
    $ids[$family.id] = $true
    if ($family.format -notin $requiredFormats) { throw "語意族群格式無效：$($family.id)" }
    if ($family.status -ne 'complete') { throw "語意族群尚未完成：$($family.id)" }
    if (@($family.topics).Count -eq 0) { throw "語意族群缺少 topics：$($family.id)" }
    if (@($family.specification).Count -eq 0) { throw "語意族群缺少規格來源：$($family.id)" }
    if ([string]::IsNullOrWhiteSpace($family.limitations)) { throw "語意族群缺少限制：$($family.id)" }

    foreach ($operation in $requiredOperations) {
        $status = $family.operations.$operation
        $allowed = if ($operation -eq 'Interop') { @('tested', 'not-applicable') } else { @('complete', 'not-applicable') }
        if ($status -notin $allowed) { throw "語意族群操作未完成：$($family.id) -> $operation" }
    }

    $coveredOperations = @{}
    foreach ($evidence in @($family.operationEvidence)) {
        if ([string]::IsNullOrWhiteSpace($evidence.test) -or [string]::IsNullOrWhiteSpace($evidence.symbol)) {
            throw "語意族群操作證據缺少 test 或 symbol：$($family.id)"
        }
        $testPath = Join-Path $root $evidence.test
        if (-not (Test-Path -LiteralPath $testPath)) {
            throw "語意族群操作測試不存在：$($family.id) -> $($evidence.test)"
        }
        $testSource = Get-Content -LiteralPath $testPath -Raw
        if (-not $testSource.Contains($evidence.symbol, [StringComparison]::Ordinal)) {
            throw "語意族群操作測試符號不存在：$($family.id) -> $($evidence.symbol)"
        }
        foreach ($operation in @($evidence.operations)) {
            if ($operation -notin $requiredOperations) {
                throw "語意族群操作證據無效：$($family.id) -> $operation"
            }
            $coveredOperations[$operation] = $true
        }
    }
    foreach ($operation in $requiredOperations) {
        if (-not $coveredOperations.ContainsKey($operation)) {
            throw "語意族群缺少逐操作測試證據：$($family.id) -> $operation"
        }
    }

    foreach ($evidenceGroup in @('implementation', 'tests', 'interop')) {
        $paths = @($family.$evidenceGroup)
        if ($paths.Count -eq 0) { throw "語意族群缺少 $evidenceGroup 證據：$($family.id)" }
        foreach ($path in $paths) {
            if (-not (Test-Path -LiteralPath (Join-Path $root $path))) {
                throw "語意族群證據不存在：$($family.id) -> $path"
            }
        }
    }
}

foreach ($format in $requiredFormats) {
    if (@($manifest.families | Where-Object format -eq $format).Count -eq 0) {
        throw "semantic coverage 缺少格式：$format"
    }
}

Write-Host "Semantic coverage 驗證成功：$(@($manifest.families).Count) families。"
