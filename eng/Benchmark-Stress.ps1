#Requires -Version 7.0
<#
.SYNOPSIS
    執行資源限制壓力測試（docs/security-limits.md 的可執行對照）。
.DESCRIPTION
    每個情境都在獨立子處理程序執行，因此堆疊溢位、記憶體耗盡這類「整個處理程序崩潰」的回歸
    也能被偵測，這是 BenchmarkDotNet 做不到的。內容包含：
    - 逐列建立工作表的縮放比（資料量放大 4 倍，耗時比值超過門檻即判定為二次方成本）。
    - 接近與超過運算子上限的連鎖公式，在 256 KB 與 128 KB 堆疊執行緒上不得崩潰。
    - 超長前置運算子鏈必須被乾淨拒絕。
    - 以引數為迴圈上限的公式必須在時間預算內結束。
    縮放比不受機器速度影響，因此不需要基準線 JSON；絕對耗時只會列印，不作為判定依據。
.PARAMETER Configuration
    建置組態，預設 Release。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$benchmarkProject = Join-Path $repoRoot "OdfKit.Benchmarks/OdfKit.Benchmarks.csproj"

dotnet build $benchmarkProject -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) {
    throw "OdfKit.Benchmarks 建置失敗。"
}

dotnet run --project $benchmarkProject -c $Configuration --no-build -- --stress
exit $LASTEXITCODE
