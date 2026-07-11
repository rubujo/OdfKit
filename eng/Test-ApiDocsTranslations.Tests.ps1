#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path ([IO.Path]::GetTempPath()) "OdfKit-ApiDocsTranslationTests-$PID"

function Copy-TestRepository {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
    New-Item -ItemType Directory -Path "$testRoot/api-docs", "$testRoot/docs", "$testRoot/eng" | Out-Null
    Copy-Item "$root/api-docs/*" "$testRoot/api-docs" -Recurse
    Copy-Item "$root/docs/ip-compliance.md", "$root/docs/security-limits.md", "$root/docs/evidence-index.md" "$testRoot/docs"
    Copy-Item "$root/THIRD-PARTY-NOTICES.md" "$testRoot/THIRD-PARTY-NOTICES.md"
    Copy-Item "$root/eng/Test-ApiDocsTranslations.ps1" "$testRoot/eng/Test-ApiDocsTranslations.ps1"
}

function Get-TestResult {
    $output = & pwsh "$testRoot/eng/Test-ApiDocsTranslations.ps1" -RepositoryRoot $testRoot -Json
    if ($LASTEXITCODE) { throw "翻譯測試工具非預期失敗：$output" }
    $output | ConvertFrom-Json
}

function Assert-Status([string]$Expected) {
    $statuses = @(Get-TestResult).issues.status
    if ($Expected -notin $statuses) { throw "預期狀態 '$Expected'，實際為：$($statuses -join ', ')。" }
}

try {
    Copy-TestRepository
    if (-not (Get-TestResult).valid) { throw '正向翻譯 fixture 應通過。' }

    $sourcePaths = @(
        'api-docs/articles/license.md',
        'docs/ip-compliance.md',
        'docs/security-limits.md',
        'docs/evidence-index.md',
        'THIRD-PARTY-NOTICES.md'
    )
    foreach ($lineEnding in @("`r`n", "`n")) {
        Copy-TestRepository
        foreach ($relativePath in $sourcePaths) {
            $path = Join-Path $testRoot $relativePath
            $content = [IO.File]::ReadAllText($path) -replace "`r`n|`r|`n", $lineEnding
            [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($false))
        }
        if (-not (Get-TestResult).valid) { throw '權威來源雜湊不應受 LF／CRLF 影響。' }
    }

    Remove-Item "$testRoot/api-docs/en/articles/license.md"
    Assert-Status 'missing'

    Copy-TestRepository
    (Get-Content "$testRoot/api-docs/en/articles/license.md" -Raw).Replace('translation_source_sha256:', 'translation_source_sha256: stale-') |
        Set-Content "$testRoot/api-docs/en/articles/license.md" -NoNewline
    Assert-Status 'stale'

    Copy-TestRepository
    (Get-Content "$testRoot/api-docs/en/articles/license.md" -Raw).Replace('_lang: en', '_lang: de') |
        Set-Content "$testRoot/api-docs/en/articles/license.md" -NoNewline
    Assert-Status 'invalid-metadata'

    foreach ($case in @(
        @{ Path = 'api-docs/en/project-docs/security-limits.md'; Token = '1,048,576' },
        @{ Path = 'api-docs/en/project-docs/evidence-index.md'; Token = 'ODS-PACKAGE-001' },
        @{ Path = 'api-docs/en/project-docs/THIRD-PARTY-NOTICES.md'; Token = 'BouncyCastle.Cryptography' }
    )) {
        Copy-TestRepository
        $path = Join-Path $testRoot $case.Path
        (Get-Content $path -Raw).Replace($case.Token, 'removed-token') | Set-Content $path -NoNewline
        Assert-Status 'token-drift'
    }

    Copy-TestRepository
    (Get-Content "$testRoot/api-docs/en/toc.yml" -Raw).Replace('project-docs/security-limits.md', '../../docs/security-limits.md') |
        Set-Content "$testRoot/api-docs/en/toc.yml" -NoNewline
    Assert-Status 'wrong-link'
    Assert-Status 'cross-locale-link'

    Write-Host 'PASS：DocFX 翻譯契約正向及負向測試通過。'
} finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
