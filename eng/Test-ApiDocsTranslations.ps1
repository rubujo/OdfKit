#Requires -Version 7.0
<#
.SYNOPSIS
    驗證 DocFX 正式文件的多語系翻譯契約。
#>
[CmdletBinding()]
param(
    [switch]$Json,
    [switch]$FailOnIssues,
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
$root = if ($RepositoryRoot) { [IO.Path]::GetFullPath($RepositoryRoot) } else { Split-Path -Parent $PSScriptRoot }
$manifestPath = Join-Path $root 'api-docs/translations.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$issues = [System.Collections.Generic.List[object]]::new()

function Add-Issue([string]$Locale, [string]$Document, [string]$Status, [string]$Message) {
    $issues.Add([pscustomobject]@{ locale = $Locale; document = $Document; status = $Status; message = $Message })
}

foreach ($document in $manifest.documents) {
    $sourcePath = Join-Path $root $document.source
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        Add-Issue $manifest.canonicalLocale $document.id 'missing-source' "缺少權威來源 $($document.source)。"
        continue
    }
    $actualHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $document.sourceSha256) {
        Add-Issue $manifest.canonicalLocale $document.id 'source-changed' "權威來源雜湊已變更；manifest=$($document.sourceSha256)，actual=$actualHash。"
    }

    foreach ($locale in $manifest.locales | Where-Object { $_ -ne $manifest.canonicalLocale }) {
        $relativeDestination = $document.destination.Replace('{locale}', $locale)
        $destinationPath = Join-Path $root $relativeDestination
        if (-not (Test-Path -LiteralPath $destinationPath)) {
            Add-Issue $locale $document.id 'missing' "缺少譯文 $relativeDestination。"
            continue
        }
        $content = Get-Content -LiteralPath $destinationPath -Raw
        if ($content -notmatch "(?m)^_lang:\s*$([regex]::Escape($locale))\s*$") {
            Add-Issue $locale $document.id 'invalid-metadata' '_lang 不正確或缺漏。'
        }
        if ($content -notmatch "(?m)^translation_source:\s*$([regex]::Escape($document.source))\s*$") {
            Add-Issue $locale $document.id 'invalid-metadata' 'translation_source 不正確或缺漏。'
        }
        if ($content -notmatch "(?m)^translation_source_sha256:\s*$([regex]::Escape($document.sourceSha256))\s*$") {
            Add-Issue $locale $document.id 'stale' 'translation_source_sha256 已過期或缺漏。'
        }
        foreach ($token in $document.requiredTokens) {
            if (-not $content.Contains($token, [StringComparison]::Ordinal)) {
                Add-Issue $locale $document.id 'token-drift' "必要 token 遺失：$token。"
            }
        }
    }
}

foreach ($locale in $manifest.locales | Where-Object { $_ -ne $manifest.canonicalLocale }) {
    $tocPath = Join-Path $root "api-docs/$locale/toc.yml"
    $guidePath = Join-Path $root "api-docs/$locale/guide.md"
    foreach ($path in @($tocPath, $guidePath)) {
        if (-not (Test-Path -LiteralPath $path)) { continue }
        $content = Get-Content -LiteralPath $path -Raw
        foreach ($href in @('articles/license.md', 'project-docs/ip-compliance.md', 'project-docs/THIRD-PARTY-NOTICES.md', 'project-docs/security-limits.md', 'project-docs/evidence-index.md')) {
            if (-not $content.Contains($href, [StringComparison]::Ordinal)) {
                Add-Issue $locale 'navigation' 'wrong-link' "$([IO.Path]::GetRelativePath($root, $path)) 缺少同語系連結 $href。"
            }
        }
        if ($content -match '\.\./\.\./docs/|\.\./\.\./THIRD-PARTY-NOTICES|\.\./articles/license') {
            Add-Issue $locale 'navigation' 'cross-locale-link' "$([IO.Path]::GetRelativePath($root, $path)) 仍連向共用 zh-TW 文件。"
        }
    }
}

$result = [pscustomobject]@{
    valid = $issues.Count -eq 0
    canonicalLocale = $manifest.canonicalLocale
    localeCount = $manifest.locales.Count
    documentCount = $manifest.documents.Count
    translationCount = ($manifest.locales.Count - 1) * $manifest.documents.Count
    issues = @($issues)
}

if ($Json) {
    $result | ConvertTo-Json -Depth 5
} elseif ($issues.Count -eq 0) {
    Write-Host "PASS：$($result.translationCount) 份 DocFX 譯文契約均為 current。"
} else {
    $issues | Format-Table locale, document, status, message -AutoSize | Out-Host
    Write-Host "FAIL：發現 $($issues.Count) 個翻譯契約問題。"
}

if ($FailOnIssues -and $issues.Count -gt 0) {
    exit 1
}
