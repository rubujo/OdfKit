#Requires -Version 7.0
<#
.SYNOPSIS
    建置 12 語系 GitHub Pages API reference 站台（DocFX 站內多語系結構）。
.DESCRIPTION
    站台結構與語系契約見 docs/api-docs-site.md。流程：
    組件建置 → 語系契約驗證 → docfx metadata → 未渲染頁面 href 修復 → docfx build → 站內連結健檢。
    輸出至 artifacts/api-site，供 .github/workflows/api-docs.yml 部署。
.PARAMETER NoRestore
    略過 dotnet tool restore（本機反覆執行時使用）。
.PARAMETER SkipProjectBuild
    略過八個組件的 dotnet build（組件輸出已存在且未變更時使用）。
#>
[CmdletBinding()]
param([switch]$NoRestore, [switch]$SkipProjectBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not $NoRestore) { dotnet tool restore; if ($LASTEXITCODE) { throw 'DocFX tool restore 失敗。' } }

    # 語系契約驗證：locales.json 是語系目錄的單一事實來源，每個語系必須有站內入口頁，
    # 且根層 index.md 必須連到每個語系（否則語系入口無法被發現）。
    $catalog = Get-Content api-docs/locales.json -Raw | ConvertFrom-Json
    $rootIndex = Get-Content api-docs/index.md -Raw
    $docfxJson = Get-Content api-docs/docfx.json -Raw
    foreach ($locale in $catalog.locales) {
        $localePath = "api-docs/$locale/index.md"
        $guidePath = "api-docs/$locale/guide.md"
        if (-not (Test-Path $localePath)) { throw "缺少語系入口頁 $localePath（locales.json 已宣告 $locale）。" }
        if (-not (Test-Path $guidePath)) { throw "缺少語系指南 $guidePath（不能只有入口頁）。" }
        $localeContent = Get-Content $localePath -Raw
        $guideContent = Get-Content $guidePath -Raw
        if ($localeContent -notmatch "(?m)^_lang:\s*$([regex]::Escape($locale))\s*$") { throw "$localePath 缺少正確的 _lang metadata。" }
        if ($localeContent -notmatch [regex]::Escape('(xref:OdfKit)')) { throw "$localePath 缺少 API reference 入口。" }
        if ($localeContent -notmatch [regex]::Escape('(guide.md)')) { throw "$localePath 缺少語系指南連結。" }
        if ($localeContent -notmatch 'CC0-1\.0' -or $localeContent -notmatch '\b(AI|KI|IA)\b') { throw "$localePath 缺少授權或 AI 產製聲明。" }
        if ($guideContent -notmatch "(?m)^_lang:\s*$([regex]::Escape($locale))\s*$") { throw "$guidePath 缺少正確的 _lang metadata。" }
        foreach ($required in @('PackageFidelity', 'SemanticApiDepth', 'InteropEvidence', 'CC0-1.0', 'xref:OdfKit')) {
            if ($guideContent -notmatch [regex]::Escape($required)) { throw "$guidePath 缺少必要內容：$required。" }
        }
        if ($locale -notin @('en', 'zh-TW') -and $localeContent -match 'Open the API reference|Site notes and compliance|Other languages|This project content is written|Original OdfKit content') {
            throw "$localePath 仍含英文 placeholder，尚未完成本地化。"
        }
        if ($rootIndex -notmatch [regex]::Escape("$locale/index.md")) { throw "api-docs/index.md 缺少語系連結：$locale。" }
        if ($docfxJson -notmatch [regex]::Escape("$locale/**.md")) { throw "api-docs/docfx.json 的 build.content 缺少語系文件集合：$locale/**.md。" }
    }
    Write-Host "PASS：$($catalog.locales.Count) 語系契約驗證通過。"

    if (-not $SkipProjectBuild) {
        $projects = @(
            'OdfKit/OdfKit.csproj',
            'OdfKit.Extensions.Collaboration/OdfKit.Extensions.Collaboration.csproj',
            'OdfKit.Extensions.Html/OdfKit.Extensions.Html.csproj',
            'OdfKit.Extensions.Imaging/OdfKit.Extensions.Imaging.csproj',
            'OdfKit.Extensions.Ooxml/OdfKit.Extensions.Ooxml.csproj',
            'OdfKit.Extensions.Pdf/OdfKit.Extensions.Pdf.csproj',
            'OdfKit.Extensions.Rdf/OdfKit.Extensions.Rdf.csproj',
            'OdfKit.Extensions.Rendering/OdfKit.Extensions.Rendering.csproj'
        )
        foreach ($project in $projects) {
            dotnet build $project -c Release -f net10.0 /p:ODFKIT_PUBLICAPI_BASELINE=1
            if ($LASTEXITCODE) { throw "API 文件組件建置失敗：$project" }
        }
    }

    Remove-Item -Recurse -Force api-docs/api -ErrorAction Ignore
    if (Test-Path api-docs/api) { throw '無法清除 api-docs/api（檔案可能被鎖定），請關閉占用程式後重試。' }
    dotnet docfx metadata api-docs/docfx.json
    if ($LASTEXITCODE) { throw 'DocFX metadata 產生失敗。' }

    # href 修復：metadata 對被 filterConfig 排除的型別（如 OdfKit.DOM.*）仍會在 references
    # 區塊輸出本地 href，DocFX build 會照抄成失效連結。移除指向未渲染頁面的 href，
    # 讓這些型別名稱渲染為純文字。頁面集合以 api/*.yml 檔名為準（<uid>.yml → <uid>.html）。
    $apiDir = 'api-docs/api'
    $pageUids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    Get-ChildItem $apiDir -Filter *.yml | ForEach-Object { [void]$pageUids.Add([IO.Path]::GetFileNameWithoutExtension($_.Name)) }
    $strippedHrefs = 0
    Get-ChildItem $apiDir -Filter *.yml | ForEach-Object {
        $lines = [IO.File]::ReadAllLines($_.FullName)
        $kept = foreach ($line in $lines) {
            if ($line -match '^(\s+)href:\s+([^#\s/:]+)\.html(#\S+)?\s*$' -and -not $pageUids.Contains($Matches[2])) {
                $strippedHrefs++
                continue
            }
            $line
        }
        if ($kept.Count -ne $lines.Count) { [IO.File]::WriteAllLines($_.FullName, $kept) }
    }
    Write-Host "已移除 $strippedHrefs 個指向未渲染頁面的本地 href。"

    Remove-Item -Recurse -Force artifacts/api-site -ErrorAction Ignore
    if (Test-Path artifacts/api-site) { throw '無法清除 artifacts/api-site（檔案被鎖定或無刪除權限），請關閉占用程式或以足夠權限清除後重試。' }
    dotnet docfx build api-docs/docfx.json --warningsAsErrors --maxParallelism 1
    if ($LASTEXITCODE) { throw 'DocFX build 失敗。' }

    # 站內連結健檢：掃描全站 HTML 的相對 href／src，任何指向不存在檔案的連結都視為失敗。
    $siteDir = Join-Path $root 'artifacts/api-site'
    $broken = [System.Collections.Generic.List[string]]::new()
    $checkedLinks = 0
    Get-ChildItem $siteDir -Recurse -Filter *.html | ForEach-Object {
        $page = $_
        $html = [IO.File]::ReadAllText($page.FullName)
        foreach ($m in [regex]::Matches($html, '(?:href|src)="([^"#]+?)(?:#[^"]*)?"')) {
            $url = $m.Groups[1].Value
            if ($url -eq '' -or $url -match '^(https?:|mailto:|javascript:|data:)') { continue }
            $unescaped = [Uri]::UnescapeDataString($url)
            $candidate = Join-Path $page.DirectoryName ($unescaped.Replace('/', [IO.Path]::DirectorySeparatorChar))
            $resolved = [IO.Path]::GetFullPath($candidate)
            $checkedLinks++
            if (-not (Test-Path $resolved) -and -not (Test-Path (Join-Path $resolved 'index.html'))) {
                $broken.Add("$($page.FullName.Substring($siteDir.Length + 1)) -> $url")
            }
        }
    }
    if ($broken.Count) {
        $broken | Select-Object -First 20 | ForEach-Object { Write-Host "  失效：$_" }
        throw "站內連結健檢失敗：$($broken.Count) 條連結指向不存在的檔案（共檢查 $checkedLinks 條）。"
    }
    Write-Host "PASS：站內連結健檢通過（$checkedLinks 條相對連結，0 失效）。"
}
finally { Pop-Location }
