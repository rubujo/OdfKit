#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root 'docs/semantic-coverage.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$provenancePath = Join-Path $root 'docs/provenance/semantic-api-provenance.json'
$provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json

if ($manifest.schemaVersion -ne 3) { throw '不支援的 semantic coverage schemaVersion。' }
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

$mutationEvidence = $manifest.mutationEvidence
$mutationTestPath = Join-Path $root $mutationEvidence.test
if (-not (Test-Path -LiteralPath $mutationTestPath) -or
    -not $mutationEvidence.repeatedSaveLoad -or
    -not (Test-Path -LiteralPath (Join-Path $root $mutationEvidence.corpusDifferentialScript)) -or
    -not (Test-Path -LiteralPath (Join-Path $root $mutationEvidence.corpusManifest))) {
    throw 'semantic coverage mutation evidence 不完整。'
}
$mutationTestSource = Get-Content -LiteralPath $mutationTestPath -Raw
$mutationSymbols = @($mutationEvidence.randomOperationSequences)
if ($mutationSymbols.Count -ne 4) {
    throw 'semantic coverage mutation evidence 必須覆蓋四種主要格式。'
}
foreach ($symbol in $mutationSymbols) {
    if (-not $mutationTestSource.Contains($symbol, [StringComparison]::Ordinal)) {
        throw "semantic coverage mutation 測試符號不存在：$symbol"
    }
}

$requiredFormats = @('ODT', 'ODS', 'ODP', 'ODG')
$requiredOperations = @('Create', 'Get', 'Find', 'Set', 'Update', 'Remove', 'Clear', 'RoundTrip', 'Interop')
$requiredQualityDimensions = @('ExistingDocument', 'UnknownContentPreservation', 'LegacyVersions', 'DowngradeDiagnostics', 'InvalidInput')
$qualityEvidence = @($manifest.qualityEvidence)
foreach ($evidence in $qualityEvidence) {
    if ($evidence.dimension -notin $requiredQualityDimensions) {
        throw "semantic coverage 品質證據維度無效：$($evidence.dimension)"
    }
    $formats = @($evidence.formats)
    if ($formats.Count -eq 0 -or @($formats | Where-Object { $_ -notin $requiredFormats }).Count -gt 0) {
        throw "semantic coverage 品質證據格式無效：$($evidence.dimension)"
    }
    $testPath = Join-Path $root $evidence.test
    if (-not (Test-Path -LiteralPath $testPath)) {
        throw "semantic coverage 品質證據測試不存在：$($evidence.test)"
    }
    $testSource = Get-Content -LiteralPath $testPath -Raw
    if (-not $testSource.Contains($evidence.symbol, [StringComparison]::Ordinal)) {
        throw "semantic coverage 品質證據測試符號不存在：$($evidence.symbol)"
    }
}

$ids = @{}
foreach ($family in @($manifest.families)) {
    if ([string]::IsNullOrWhiteSpace($family.id)) { throw '語意族群缺少 id。' }
    if ($ids.ContainsKey($family.id)) { throw "語意族群 id 重複：$($family.id)" }
    $ids[$family.id] = $true
    if ($family.format -notin $requiredFormats) { throw "語意族群格式無效：$($family.id)" }
    if ($family.status -ne 'complete') { throw "語意族群尚未完成：$($family.id)" }
    foreach ($dimension in $requiredQualityDimensions) {
        $covered = @($qualityEvidence | Where-Object {
            $_.dimension -eq $dimension -and $family.format -in @($_.formats)
        }).Count -gt 0
        if (-not $covered) {
            throw "語意族群缺少品質證據：$($family.id) -> $dimension"
        }
    }
    $familyTopics = @($family.topics)
    if ($familyTopics.Count -eq 0) { throw "語意族群缺少 topics：$($family.id)" }
    if (@($familyTopics | Select-Object -Unique).Count -ne $familyTopics.Count) {
        throw "語意族群 topics 重複：$($family.id)"
    }
    if (@($family.specification).Count -eq 0) { throw "語意族群缺少規格來源：$($family.id)" }
    if ([string]::IsNullOrWhiteSpace($family.limitations)) { throw "語意族群缺少限制：$($family.id)" }

    foreach ($operation in $requiredOperations) {
        $status = $family.operations.$operation
        $allowed = if ($operation -eq 'Interop') { @('tested', 'not-applicable') } else { @('complete', 'not-applicable') }
        if ($status -notin $allowed) { throw "語意族群操作未完成：$($family.id) -> $operation" }
    }

    $coveredOperations = @{}
    $topicOperations = @{}
    $focusedTopics = @{}
    $interopPaths = @($family.interop)
    foreach ($topic in $familyTopics) {
        if ([string]::IsNullOrWhiteSpace($topic)) { throw "語意族群 topic 無效：$($family.id)" }
        $topicOperations[$topic] = @{}
    }
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
        $evidenceTopics = @($evidence.topics)
        if ($evidenceTopics.Count -eq 0) {
            throw "語意族群操作證據缺少 topics：$($family.id) -> $($evidence.symbol)"
        }
        foreach ($topic in $evidenceTopics) {
            if ($topic -notin $familyTopics) {
                throw "語意族群操作證據 topic 無效：$($family.id) -> $topic"
            }
            if ($familyTopics.Count -eq 1 -or $evidenceTopics.Count -lt $familyTopics.Count) {
                $focusedTopics[$topic] = $true
            }
        }
        foreach ($operation in @($evidence.operations)) {
            if ($operation -notin $requiredOperations) {
                throw "語意族群操作證據無效：$($family.id) -> $operation"
            }
            $coveredOperations[$operation] = $true
            if ($operation -eq 'Interop' -and $evidence.test -notin $interopPaths) {
                throw "語意族群 Interop 證據不是外部互通測試：$($family.id) -> $($evidence.test)"
            }
            foreach ($topic in $evidenceTopics) {
                $topicOperations[$topic][$operation] = $true
            }
        }
    }
    foreach ($operation in $requiredOperations) {
        if (-not $coveredOperations.ContainsKey($operation)) {
            throw "語意族群缺少逐操作測試證據：$($family.id) -> $operation"
        }
    }
    foreach ($topic in $familyTopics) {
        if (-not $focusedTopics.ContainsKey($topic)) {
            throw "語意 topic 缺少聚焦測試證據：$($family.id) -> $topic"
        }
        foreach ($operation in $requiredOperations) {
            if ($family.operations.$operation -ne 'not-applicable' -and
                -not $topicOperations[$topic].ContainsKey($operation)) {
                throw "語意 topic 缺少逐操作測試證據：$($family.id) -> $topic -> $operation"
            }
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

if ($provenance.schemaVersion -ne 1 -or
    $provenance.policy -ne 'clean-room-specification-and-observation-only') {
    throw 'semantic API provenance 契約無效。'
}
if (@($provenance.forbiddenSources).Count -eq 0) {
    throw 'semantic API provenance 缺少禁止來源。'
}
$provenanceIds = @($provenance.families | ForEach-Object id | Sort-Object)
$manifestIds = @($manifest.families | ForEach-Object id | Sort-Object)
if (($provenanceIds -join ',') -ne ($manifestIds -join ',')) {
    throw 'semantic API provenance 與 coverage manifest 的族群集合不一致。'
}
foreach ($record in @($provenance.families)) {
    if (@($record.specificationSources).Count -eq 0 -or
        @($record.fixtureSources).Count -eq 0 -or
        @($record.behaviorObservations).Count -eq 0 -or
        [string]::IsNullOrWhiteSpace($record.implementationBoundary)) {
        throw "semantic API provenance 記錄不完整：$($record.id)"
    }
    foreach ($fixture in @($record.fixtureSources)) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $fixture))) {
            throw "semantic API provenance fixture 不存在：$($record.id) -> $fixture"
        }
    }
}

Write-Host "Semantic coverage 驗證成功：$(@($manifest.families).Count) families。"
