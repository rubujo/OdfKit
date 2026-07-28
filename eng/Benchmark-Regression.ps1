#Requires -Version 7.0
<#
.SYNOPSIS
    執行核心 DOM 與 ODS 串流微基準並與基準線比對（PERF-3c）。
.DESCRIPTION
    以短迭代分別執行 DomInsert 與 OdsStreamWriter 基準，解析 Mean 與配置量，
    再與 eng/baselines/performance-baselines.json 比對。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER UpdateBaseline
    以本次量測更新所選基準線（僅限本機調整基準時使用）。
.PARAMETER Filter
    選用的 BenchmarkDotNet 篩選器；省略時驗證全部受保護基準。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$UpdateBaseline,
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$benchmarkProject = Join-Path $repoRoot "OdfKit.Benchmarks/OdfKit.Benchmarks.csproj"
$baselinePath = Join-Path $PSScriptRoot "baselines/performance-baselines.json"
$definitions = @(
    [PSCustomObject]@{
        Key = "DomInsertBenchmarks.SequentialInsertAfter"
        Filter = "*DomInsert*"
        Method = "SequentialInsertAfter"
    },
    [PSCustomObject]@{
        Key = "OdsStreamWriterBenchmarks.WriteRows"
        Filter = "*OdsStreamWriterBenchmarks*"
        Method = "WriteRows"
    },
    [PSCustomObject]@{
        Key = "StandardOdtBenchmarks.WriteStreaming"
        Filter = "*StandardOdtBenchmarks*"
        Method = "WriteStreaming"
    },
    [PSCustomObject]@{
        Key = "StandardPackageOpenBenchmarks.OpenOdt"
        Filter = "*StandardPackageOpenBenchmarks*"
        Method = "OpenOdt"
    },
    [PSCustomObject]@{
        Key = "CollaborationOperationBenchmarks.Replay_10kTextOperations"
        Filter = "*CollaborationOperationBenchmarks*"
        Method = "Replay_10kTextOperations"
    },
    [PSCustomObject]@{
        Key = "FindReplaceBenchmarks.ReplaceText"
        Filter = "*FindReplaceBenchmarks*"
        Method = "ReplaceText"
    },
    [PSCustomObject]@{
        Key = "FormulaEvaluationBenchmarks.FullRecalculation10000"
        Filter = "*FormulaEvaluationBenchmarks.FullRecalculation*"
        Method = "FullRecalculation"
        ParameterValue = 10000
        InProcess = $true
    },
    [PSCustomObject]@{
        Key = "FormulaEvaluationBenchmarks.IncrementalOnePercentRecalculation10000"
        Filter = "*FormulaEvaluationBenchmarks.IncrementalOnePercentRecalculation*"
        Method = "IncrementalOnePercentRecalculation"
        InProcess = $true
    }
)

if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $definitions = @($definitions | Where-Object { $_.Filter -eq $Filter -or $_.Key -like $Filter })
    if ($definitions.Count -eq 0) {
        throw "沒有符合篩選器的受保護效能基準：$Filter"
    }
}

function Convert-TimeToNanoseconds {
    param([double]$Value, [string]$Unit)
    switch ($Unit) {
        "ns" { return [long]$Value }
        "us" { return [long]($Value * 1000) }
        "µs" { return [long]($Value * 1000) }
        "ms" { return [long]($Value * 1000000) }
        "s" { return [long]($Value * 1000000000) }
        default { throw "不支援的時間單位：$Unit" }
    }
}

function Convert-SizeToBytes {
    param([double]$Value, [string]$Unit)
    switch ($Unit) {
        "B" { return [long]$Value }
        "KB" { return [long]($Value * 1KB) }
        "MB" { return [long]($Value * 1MB) }
        "GB" { return [long]($Value * 1GB) }
        default { throw "不支援的配置量單位：$Unit" }
    }
}

function Invoke-ProtectedBenchmark {
    param([PSCustomObject]$Definition)

    Write-Host "執行 BenchmarkDotNet（filter: $($Definition.Filter)）…"
    $benchmarkArgs = @(
        'run',
        '--project', $benchmarkProject,
        '-c', $Configuration
    )
    if ($Definition.InProcess) {
        $benchmarkArgs += '--no-build'
    }
    $benchmarkArgs += @(
        '--',
        '--filter', $Definition.Filter,
        '--job', 'short',
        '--warmupCount', '3',
        '--iterationCount', '8'
    )
    if ($Definition.InProcess) {
        $benchmarkArgs += '--inProcess'
    }
    $output = dotnet @benchmarkArgs 2>&1 | Out-String

    Write-Host $output

    $escapedMethod = [regex]::Escape($Definition.Method)
    $rowPrefix = "\|\s*$escapedMethod\s*\|"
    if ($null -ne $Definition.ParameterValue) {
        $rowPrefix += "\s*$($Definition.ParameterValue)\s*\|"
    }

    if ($output -notmatch "$rowPrefix\s*([\d.,]+)\s*(ns|us|µs|ms|s)\s*\|") {
        throw "無法從 BenchmarkDotNet 輸出解析 $($Definition.Method) Mean。"
    }

    $mean = [double]::Parse($Matches[1].Replace(',', ''), [Globalization.CultureInfo]::InvariantCulture)
    $meanNanoseconds = Convert-TimeToNanoseconds -Value $mean -Unit $Matches[2]
    $allocatedBytes = $null
    if ($output -match "$rowPrefix[^\r\n]*\|\s*([\d.,]+)\s*(B|KB|MB|GB)\s*\|") {
        $allocated = [double]::Parse($Matches[1].Replace(',', ''), [Globalization.CultureInfo]::InvariantCulture)
        $allocatedBytes = Convert-SizeToBytes -Value $allocated -Unit $Matches[2]
    }

    return [PSCustomObject]@{
        Definition = $Definition
        MeanNanoseconds = $meanNanoseconds
        AllocatedBytes = $allocatedBytes
    }
}

Push-Location $repoRoot
try {
    if (-not (Test-Path $baselinePath)) {
        throw "找不到基準線檔案：$baselinePath"
    }

    $json = Get-Content -Path $baselinePath -Raw | ConvertFrom-Json
    foreach ($definition in $definitions) {
        $measurement = Invoke-ProtectedBenchmark -Definition $definition
        $entry = $json.benchmarks.($definition.Key)
        if ($null -eq $entry) {
            throw "基準線缺少項目：$($definition.Key)"
        }

        if ($UpdateBaseline) {
            $entry.meanNanoseconds = $measurement.MeanNanoseconds
            if ($null -ne $measurement.AllocatedBytes -and $null -ne $entry.allocatedBytes) {
                $entry.allocatedBytes = $measurement.AllocatedBytes
            }
            $entry.note = "Updated by Benchmark-Regression.ps1 on $(Get-Date -Format 'yyyy-MM-dd')"
            continue
        }

        $maxMean = [long]([long]$entry.meanNanoseconds * (1 + [double]$entry.toleranceRatio))
        Write-Host ""
        Write-Host "基準比對：$($definition.Key)"
        Write-Host "  基準 Mean：$([math]::Round([long]$entry.meanNanoseconds / 1000000, 3)) ms"
        Write-Host "  本次 Mean：$([math]::Round($measurement.MeanNanoseconds / 1000000, 3)) ms"
        Write-Host "  容許上限：$([math]::Round($maxMean / 1000000, 3)) ms (+$([math]::Round([double]$entry.toleranceRatio * 100))%)"
        if ($measurement.MeanNanoseconds -gt $maxMean) {
            throw "效能回歸：$($definition.Key) 的 Mean 超過基準上限。"
        }

        if ($null -ne $entry.allocatedBytes -and $null -ne $measurement.AllocatedBytes) {
            $maxAllocated = [long]([long]$entry.allocatedBytes * (1 + [double]$entry.allocationToleranceRatio))
            Write-Host "  基準配置：$([math]::Round([long]$entry.allocatedBytes / 1KB, 1)) KB"
            Write-Host "  本次配置：$([math]::Round($measurement.AllocatedBytes / 1KB, 1)) KB"
            if ($measurement.AllocatedBytes -gt $maxAllocated) {
                throw "配置量回歸：$($definition.Key) 超過基準上限。"
            }
        }

        Write-Host "通過：未超過回歸門檻。"
    }

    if ($UpdateBaseline) {
        $json | ConvertTo-Json -Depth 6 | Set-Content -Path $baselinePath -Encoding UTF8
        Write-Host "已更新所選效能基準線。"
    }
}
finally {
    Pop-Location
}
