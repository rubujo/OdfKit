#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$SamplePath,
    [string]$OutputPath = 'artifacts/performance/performance-budget-candidate.json'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$budgetPath = Join-Path $PSScriptRoot 'performance-budgets.json'
$validatorPath = Join-Path $PSScriptRoot 'Test-PerformanceBudgets.ps1'
$budget = Get-Content -LiteralPath $budgetPath -Raw | ConvertFrom-Json
if ($SamplePath.Count -lt $budget.requiredSamples) {
    throw "候選預算至少需要 $($budget.requiredSamples) 份樣本。"
}

function Get-Median {
    param([double[]]$Values)

    $sorted = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) {
        return [double]$sorted[$middle]
    }
    return ([double]$sorted[$middle - 1] + [double]$sorted[$middle]) / 2
}

$samples = [System.Collections.Generic.List[object]]::new()
$identities = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($path in $SamplePath) {
    & $validatorPath -SamplePath $path
    $fullPath = if ([IO.Path]::IsPathRooted($path)) { $path } else { Join-Path $root $path }
    $sample = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json
    $identity = "$($sample.metadata.workflowRunId)/$($sample.metadata.workflowRunAttempt)"
    if (-not $identities.Add($identity)) { throw "performance sample 執行身分重複：$identity" }
    $samples.Add($sample)
}

$first = $samples[0]
foreach ($sample in $samples) {
    foreach ($property in @('runnerOs', 'runnerArchitecture', 'runtimeVersion', 'processorDescription', 'artifactName')) {
        if ($sample.metadata.$property -ne $first.metadata.$property) {
            throw "performance samples 的 $property 不一致。"
        }
    }
}

$scenarioNames = @($first.measurements | Select-Object -ExpandProperty Scenario)
$scenarioBudgets = [ordered]@{}
foreach ($scenario in $scenarioNames) {
    $values = @($samples | ForEach-Object { $_.measurements | Where-Object Scenario -eq $scenario })
    $scenarioBudgets[$scenario] = [ordered]@{
        sampleCount = $values.Count
        medianElapsedMilliseconds = [Math]::Round((Get-Median @($values.ElapsedMilliseconds)), 6)
        medianAllocatedBytes = [long][Math]::Round((Get-Median @($values.AllocatedBytes)))
        medianPeakWorkingSetBytes = [long][Math]::Round((Get-Median @($values.PeakWorkingSetBytes)))
        allocationRegressionPercent = $budget.allocationRegressionPercent
        advisoryRegressionPercent = $budget.advisoryRegressionPercent
    }
}

$candidate = [ordered]@{
    schemaVersion = 1
    status = 'candidate'
    requiredSamples = $budget.requiredSamples
    sampleCount = $samples.Count
    environment = [ordered]@{
        runnerOs = $first.metadata.runnerOs
        runnerArchitecture = $first.metadata.runnerArchitecture
        runtimeVersion = $first.metadata.runtimeVersion
        processorDescription = $first.metadata.processorDescription
        artifactName = $first.metadata.artifactName
    }
    samples = @($samples | ForEach-Object {
        [ordered]@{
            commitSha = $_.metadata.commitSha
            measuredAtUtc = ([DateTimeOffset]$_.metadata.measuredAtUtc).ToUniversalTime().ToString("O", [Globalization.CultureInfo]::InvariantCulture)
            workflowRunId = $_.metadata.workflowRunId
            workflowRunAttempt = $_.metadata.workflowRunAttempt
        }
    })
    scenarios = $scenarioBudgets
}

$outputFullPath = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $root $OutputPath }
New-Item -ItemType Directory -Path (Split-Path -Parent $outputFullPath) -Force | Out-Null
$candidate | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputFullPath -Encoding utf8
Write-Host "已產生效能預算候選：$outputFullPath"
