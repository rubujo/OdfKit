#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$BudgetPath = 'eng/performance-budgets.json',
    [string]$SamplePath,
    [string]$CandidatePath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$budgetFullPath = if ([IO.Path]::IsPathRooted($BudgetPath)) { $BudgetPath } else { Join-Path $root $BudgetPath }
$budget = Get-Content -LiteralPath $budgetFullPath -Raw | ConvertFrom-Json
$expectedScenarios = @(
    'OdsStreamWrite', 'OdsStreamRead', 'OdsDomRoundTrip',
    'OdtStreamWrite', 'OdtStreamRead', 'OdtDomRoundTrip',
    'OdpStructureWrite', 'OdpStructureRead', 'OdpMediaRoundTrip'
)

if ($budget.schemaVersion -ne 1) { throw '不支援的 performance budget schemaVersion。' }
if ($budget.status -notin @('collecting', 'active')) { throw 'performance budget status 必須為 collecting 或 active。' }
if ($budget.requiredSamples -lt 3) { throw 'performance budget 至少需要三份樣本。' }
if ($budget.allocationRegressionPercent -le 0 -or $budget.advisoryRegressionPercent -le 0) {
    throw 'performance budget regression 百分比必須為正值。'
}
$scenarioCount = @($budget.scenarios.PSObject.Properties).Count
if ($budget.status -eq 'collecting' -and $scenarioCount -ne 0) {
    throw 'collecting 階段不得預先填入未啟用的 scenario budget。'
}
if ($budget.status -eq 'active') {
    $activeScenarios = @($budget.scenarios.PSObject.Properties.Name | Sort-Object)
    if (($activeScenarios -join ',') -ne (@($expectedScenarios | Sort-Object) -join ',')) {
        throw 'active performance budget 必須完整包含九個標準情境。'
    }
    foreach ($property in @('runnerOs', 'runnerArchitecture', 'runtimeVersion', 'processorDescription', 'artifactName')) {
        if ([string]::IsNullOrWhiteSpace($budget.environment.$property)) {
            throw "active performance budget environment 缺少 $property。"
        }
    }
    foreach ($entry in $budget.scenarios.PSObject.Properties) {
        $value = $entry.Value
        if ($value.baselineElapsedMilliseconds -le 0 -or $value.baselineAllocatedBytes -lt 0 -or
            $value.baselinePeakWorkingSetBytes -le 0) {
            throw "active performance budget scenario 基準無效：$($entry.Name)"
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($SamplePath)) {
    $sampleFullPath = if ([IO.Path]::IsPathRooted($SamplePath)) { $SamplePath } else { Join-Path $root $SamplePath }
    $sample = Get-Content -LiteralPath $sampleFullPath -Raw | ConvertFrom-Json
    if ($sample.schemaVersion -ne 2) { throw '不支援的 performance sample schemaVersion。' }
    foreach ($property in @('commitSha', 'measuredAtUtc', 'workflowRunId', 'workflowRunAttempt', 'runnerOs', 'runnerArchitecture', 'runtimeVersion', 'processorDescription', 'artifactName')) {
        if ([string]::IsNullOrWhiteSpace($sample.metadata.$property)) {
            throw "performance sample metadata 缺少 $property。"
        }
    }
    [DateTimeOffset]::Parse($sample.metadata.measuredAtUtc, [Globalization.CultureInfo]::InvariantCulture) | Out-Null
    $measurements = @($sample.measurements)
    if ($measurements.Count -ne $expectedScenarios.Count) {
        throw 'performance sample 必須包含九個標準情境。'
    }
    foreach ($scenario in $expectedScenarios) {
        $measurement = @($measurements | Where-Object Scenario -eq $scenario)
        if ($measurement.Count -ne 1) { throw "performance sample 情境缺少或重複：$scenario" }
        $value = $measurement[0]
        if ($value.SchemaVersion -ne 1 -or $value.ElapsedMilliseconds -le 0 -or
            $value.AllocatedBytes -lt 0 -or $value.PeakWorkingSetBytes -le 0 -or
            $value.PackageBytes -le 0 -or $value.XmlBytes -le 0 -or $value.Checksum -eq 0) {
            throw "performance sample 情境數值無效：$scenario"
        }
    }

    if ($budget.status -eq 'active') {
        foreach ($property in @('runnerOs', 'runnerArchitecture', 'runtimeVersion', 'processorDescription', 'artifactName')) {
            if ($sample.metadata.$property -ne $budget.environment.$property) {
                throw "performance sample 與 active budget 的 $property 不一致。"
            }
        }
        foreach ($measurement in $measurements) {
            $baseline = $budget.scenarios.($measurement.Scenario)
            $maxAllocated = [double]$baseline.baselineAllocatedBytes * (1 + [double]$budget.allocationRegressionPercent / 100)
            if ([double]$measurement.AllocatedBytes -gt $maxAllocated) {
                throw "配置量回歸：$($measurement.Scenario) 超過 active budget。"
            }

            $advisoryRatio = 1 + [double]$budget.advisoryRegressionPercent / 100
            if ([double]$measurement.ElapsedMilliseconds -gt [double]$baseline.baselineElapsedMilliseconds * $advisoryRatio) {
                Write-Warning "耗時提醒：$($measurement.Scenario) 超過 active budget +$($budget.advisoryRegressionPercent)%。"
            }
            if ([double]$measurement.PeakWorkingSetBytes -gt [double]$baseline.baselinePeakWorkingSetBytes * $advisoryRatio) {
                Write-Warning "峰值工作集提醒：$($measurement.Scenario) 超過 active budget +$($budget.advisoryRegressionPercent)%。"
            }
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($CandidatePath)) {
    $candidateFullPath = if ([IO.Path]::IsPathRooted($CandidatePath)) { $CandidatePath } else { Join-Path $root $CandidatePath }
    $candidate = Get-Content -LiteralPath $candidateFullPath -Raw | ConvertFrom-Json
    if ($candidate.schemaVersion -ne 1 -or $candidate.status -ne 'candidate') {
        throw 'performance budget candidate schema 或狀態無效。'
    }
    if ($candidate.sampleCount -lt $budget.requiredSamples -or @($candidate.samples).Count -ne $candidate.sampleCount) {
        throw 'performance budget candidate 樣本數不足或不一致。'
    }
    if (@($candidate.scenarios.PSObject.Properties).Count -ne 9) {
        throw 'performance budget candidate 必須包含九個 scenario。'
    }
    foreach ($property in @('runnerOs', 'runnerArchitecture', 'runtimeVersion', 'processorDescription', 'artifactName')) {
        if ([string]::IsNullOrWhiteSpace($candidate.environment.$property)) {
            throw "performance budget candidate environment 缺少 $property。"
        }
    }
    $candidateScenarios = @($candidate.scenarios.PSObject.Properties.Name | Sort-Object)
    $expectedCandidateScenarios = @($expectedScenarios | Sort-Object)
    if (($candidateScenarios -join ',') -ne ($expectedCandidateScenarios -join ',')) {
        throw 'performance budget candidate scenario 集合不符。'
    }
    $candidateIdentities = @($candidate.samples | ForEach-Object { "$($_.workflowRunId)/$($_.workflowRunAttempt)" })
    if (@($candidateIdentities | Select-Object -Unique).Count -ne $candidateIdentities.Count) {
        throw 'performance budget candidate 含重複 workflow run 身分。'
    }
    foreach ($entry in $candidate.scenarios.PSObject.Properties) {
        $value = $entry.Value
        if ($value.sampleCount -ne $candidate.sampleCount -or $value.medianElapsedMilliseconds -le 0 -or
            $value.medianAllocatedBytes -lt 0 -or $value.medianPeakWorkingSetBytes -le 0 -or
            $value.allocationRegressionPercent -ne $budget.allocationRegressionPercent -or
            $value.advisoryRegressionPercent -ne $budget.advisoryRegressionPercent) {
            throw "performance budget candidate scenario 無效：$($entry.Name)"
        }
    }
}

Write-Host "Performance budget 驗證成功：$($budget.status)。"
