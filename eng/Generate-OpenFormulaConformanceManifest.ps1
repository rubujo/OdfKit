#Requires -Version 7.0
<#
.SYNOPSIS
    由 OdfFormulaSupport 的 Large Group 清單產生可稽核的 OpenFormula manifest。
.DESCRIPTION
    manifest 明確區分名稱派送、安全排除與逐函式語意 corpus，不會把 388/388
    名稱覆蓋誤標為未附條件的 OASIS Large 正式一致性。
#>
[CmdletBinding()]
param(
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$assemblyPath = Join-Path $repoRoot 'OdfKit/bin/Release/net10.0/OdfKit.dll'
$outputPath = Join-Path $repoRoot 'docs/openformula-conformance-manifest.json'
$normativeCorpusPath = Join-Path $repoRoot 'docs/openformula-normative-corpus.json'

if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "找不到 Release 組件：$assemblyPath"
}

Add-Type -Path $assemblyPath
$group = [OdfKit.Formula.OdfFormulaConformanceGroup]::Large
$requiredFunctions = [OdfKit.Formula.OdfFormulaSupport]::GetRequiredFunctions($group)
$normativeCorpus = Get-Content -LiteralPath $normativeCorpusPath -Raw | ConvertFrom-Json
$normativeFunctions = @($normativeCorpus.cases.function)
if ($normativeFunctions.Count -ne $requiredFunctions.Count -or
    (Compare-Object $requiredFunctions $normativeFunctions).Count -ne 0) {
    throw 'OpenFormula normative corpus 未一對一涵蓋 Large Group 函式。'
}
$semanticDimensions = @(
    'arity',
    'normal-types',
    'implicit-conversion',
    'blank-values',
    'error-propagation',
    'boundaries',
    'version-differences'
)

$entries = foreach ($functionName in $requiredFunctions) {
    $securityExcluded = $functionName -eq 'DDE'
    $semanticCases = foreach ($dimension in $semanticDimensions) {
        [ordered]@{
            id = "$functionName::$dimension"
            dimension = $dimension
            versions = @('1.2', '1.3', '1.4')
            oracle = if ($securityExcluded) { 'security-na-without-argument-evaluation' } else { 'safe-evaluation-contract' }
            evidenceTest = 'OpenFormulaFunctionSemanticContractTests.EveryLargeFunctionHasExecutableSafeSemanticContract'
        }
    }
    [ordered]@{
        name = $functionName
        versions = @('1.2', '1.3', '1.4')
        profileStatus = if ($securityExcluded) { 'security-excluded' } else { 'evaluated-dispatch' }
        semanticCorpusStatus = if ($securityExcluded) { 'security-tested' } else { 'safe-contract-covered' }
        normativeOracleStatus = if ($securityExcluded) { 'not-applicable-security-exclusion' } else { 'representative-oasis-oracle-covered' }
        semanticCases = @($semanticCases)
        evidenceTests = if ($securityExcluded) {
            @(
                'OpenFormulaConformanceCorpusTests.ScalarCorpusMatchesExpectedResult',
                'OpenFormulaNormativeCorpusTests.EverySafeLargeFunctionMatchesNormativeOracle',
                'OpenFormulaExtendedEvaluatorTests.DdeDoesNotEvaluateArguments'
            )
        } else {
            @(
                'OpenFormulaSupportTests.LargeGroupMandatoryFunctionsAreDispatchable',
                'OpenFormulaNormativeCorpusTests.EverySafeLargeFunctionMatchesNormativeOracle'
            )
        }
    }
}

$manifest = [ordered]@{
    schemaVersion = 3
    profile = 'OdfKit Safe Large'
    odfVersions = @('1.2', '1.3', '1.4')
    requiredFunctionCount = $requiredFunctions.Count
    safeSemanticContractCaseCount = $requiredFunctions.Count * $semanticDimensions.Count
    normativeFunctionCaseCount = $normativeCorpus.cases.Count
    safeLargeConformanceClaim = $true
    officialLargeConformanceClaim = $false
    requiredSemanticDimensions = $semanticDimensions
    securityExcludedFunctions = @('DDE')
    functions = @($entries)
}

$json = $manifest | ConvertTo-Json -Depth 8
$json = $json -replace "`r?`n", "`r`n"
$json += "`r`n"

if ($VerifyOnly) {
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        throw "缺少 OpenFormula conformance manifest：$outputPath"
    }

    $current = [System.IO.File]::ReadAllText($outputPath)
    if ($current -ne $json) {
        throw 'OpenFormula conformance manifest 已漂移，請重新執行產生器。'
    }

    Write-Host "PASS：OpenFormula conformance manifest 與 388 個 Large Group 函式清單一致。"
    exit 0
}

[System.IO.File]::WriteAllText(
    $outputPath,
    $json,
    [System.Text.UTF8Encoding]::new($false))
Write-Host "WROTE：$outputPath（$($requiredFunctions.Count) functions）"
