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
.PARAMETER OutputDirectory
    網站輸出目錄；預設為 Pages workflow 使用的 artifacts/api-site。
#>
[CmdletBinding()]
param(
    [switch]$NoRestore,
    [switch]$SkipProjectBuild,
    [string]$OutputDirectory = 'artifacts/api-site'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not $NoRestore) { dotnet tool restore; if ($LASTEXITCODE) { throw 'DocFX tool restore 失敗。' } }

    $toolManifest = Get-Content .config/dotnet-tools.json -Raw | ConvertFrom-Json
    $expectedDocfxVersion = $toolManifest.tools.docfx.version
    $actualDocfxVersion = (& dotnet docfx --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -or $actualDocfxVersion -notmatch "^$([regex]::Escape($expectedDocfxVersion))(\+|$)") {
        throw "DocFX 版本不符：預期 $expectedDocfxVersion，實際輸出為 '$actualDocfxVersion'。"
    }
    Write-Host "PASS：DocFX 固定版本 $expectedDocfxVersion。"

    # 語系契約驗證：locales.json 是語系目錄的單一事實來源，每個語系必須有站內入口頁，
    # 且根層 index.md 必須連到每個語系（否則語系入口無法被發現）。
    $catalog = Get-Content api-docs/locales.json -Raw | ConvertFrom-Json
    $rootIndex = Get-Content api-docs/index.md -Raw
    $docfxJson = Get-Content api-docs/docfx.json -Raw
    $docfxConfig = $docfxJson | ConvertFrom-Json
    $requiredSharedLinks = @(
        '../articles/license.md',
        '../../docs/ip-compliance.md',
        '../../THIRD-PARTY-NOTICES.md',
        '../../docs/security-limits.md',
        '../../docs/evidence-index.md'
    )
    foreach ($locale in $catalog.locales) {
        $localePath = "api-docs/$locale/index.md"
        $guidePath = "api-docs/$locale/guide.md"
        $tocPath = "api-docs/$locale/toc.yml"
        if (-not (Test-Path $localePath)) { throw "缺少語系入口頁 $localePath（locales.json 已宣告 $locale）。" }
        if (-not (Test-Path $guidePath)) { throw "缺少語系指南 $guidePath（不能只有入口頁）。" }
        if (-not (Test-Path $tocPath)) { throw "缺少語系導覽 $tocPath。" }
        $localeContent = Get-Content $localePath -Raw
        $guideContent = Get-Content $guidePath -Raw
        $tocContent = Get-Content $tocPath -Raw
        if ($localeContent -notmatch "(?m)^_lang:\s*$([regex]::Escape($locale))\s*$") { throw "$localePath 缺少正確的 _lang metadata。" }
        if ($localeContent -notmatch [regex]::Escape('(xref:OdfKit)')) { throw "$localePath 缺少 API reference 入口。" }
        if ($localeContent -notmatch [regex]::Escape('[en + zh-TW]')) { throw "$localePath 的 API 入口缺少內容語系標示。" }
        if ($localeContent -notmatch [regex]::Escape('(guide.md)')) { throw "$localePath 缺少語系指南連結。" }
        if ($localeContent -notmatch 'CC0-1\.0' -or $localeContent -notmatch '\b(AI|KI|IA)\b') { throw "$localePath 缺少授權或 AI 產製聲明。" }
        if ($guideContent -notmatch "(?m)^_lang:\s*$([regex]::Escape($locale))\s*$") { throw "$guidePath 缺少正確的 _lang metadata。" }
        foreach ($required in @('PackageFidelity', 'SemanticApiDepth', 'InteropEvidence', 'CC0-1.0', 'xref:OdfKit')) {
            if ($guideContent -notmatch [regex]::Escape($required)) { throw "$guidePath 缺少必要內容：$required。" }
        }
        foreach ($requiredLink in $requiredSharedLinks) {
            if ($guideContent -notmatch [regex]::Escape($requiredLink)) { throw "$guidePath 缺少正式聲明連結：$requiredLink。" }
            if ($tocContent -notmatch [regex]::Escape($requiredLink)) { throw "$tocPath 缺少正式聲明連結：$requiredLink。" }
        }
        if ($tocContent -match '(?m)^\s*href:\s*xref:') { throw "$tocPath 不得以 href: xref:* 指向 API；請使用 DocFX uid。" }
        if ($tocContent -notmatch '(?m)^\s*uid:\s*OdfKit\s*$') { throw "$tocPath 缺少以 uid 指定的共用 API reference 入口。" }
        if ($tocContent -notmatch [regex]::Escape('[en + zh-TW]')) { throw "$tocPath 的 API 入口缺少內容語系標示。" }
        if ($guideContent -notmatch [regex]::Escape('[en + zh-TW]')) { throw "$guidePath 的 API 入口缺少內容語系標示。" }
        if ($locale -ne 'zh-TW') {
            $guideZhTwLabels = [regex]::Matches($guideContent, [regex]::Escape('[zh-TW]')).Count
            $tocZhTwLabels = [regex]::Matches($tocContent, [regex]::Escape('[zh-TW]')).Count
            if ($guideZhTwLabels -lt $requiredSharedLinks.Count -or $tocZhTwLabels -lt $requiredSharedLinks.Count) {
                throw "$locale 的指南或 TOC 未明示共用正式頁面使用 zh-TW。"
            }
        }
        if ($locale -notin @('en', 'zh-TW') -and $localeContent -match 'Open the API reference|Site notes and compliance|Other languages|This project content is written|Original OdfKit content') {
            throw "$localePath 仍含英文 placeholder，尚未完成本地化。"
        }
        if ($rootIndex -notmatch [regex]::Escape("$locale/index.md")) { throw "api-docs/index.md 缺少語系連結：$locale。" }
        if ($docfxJson -notmatch [regex]::Escape("$locale/**.{md,yml}")) { throw "api-docs/docfx.json 的 build.content 缺少語系文件集合：$locale/**.{md,yml}。" }
        $fileMetadataLocale = $docfxConfig.build.fileMetadata._lang.PSObject.Properties["$locale/**"].Value
        if ($fileMetadataLocale -ne $locale) { throw "api-docs/docfx.json 的 fileMetadata._lang 缺少或錯誤：$locale。" }
    }
    if ($rootIndex -match '(?m)^redirect_url\s*:') { throw '根首頁不得設定 redirect_url；必須保留語言選擇頁。' }
    $rootToc = Get-Content api-docs/toc.yml -Raw
    if ($rootToc -notmatch [regex]::Escape('API [en + zh-TW]')) { throw '根 navbar 的 API 入口缺少內容語系標示。' }
    if (@($docfxConfig.build.template) -notcontains 'modern') { throw 'DocFX 必須使用官方 modern 模板。' }
    foreach ($mapping in @('ip-compliance.md', 'security-limits.md', 'evidence-index.md', 'THIRD-PARTY-NOTICES.md')) {
        if ($docfxJson -notmatch [regex]::Escape($mapping)) { throw "DocFX content mapping 缺少權威文件：$mapping。" }
    }
    foreach ($footerTarget in @('articles/license.html', 'project-docs/ip-compliance.html', 'project-docs/THIRD-PARTY-NOTICES.html', 'project-docs/security-limits.html', 'project-docs/evidence-index.html')) {
        if ($docfxConfig.build.globalMetadata._appFooter -notmatch [regex]::Escape("/OdfKit/$footerTarget")) { throw "modern footer 缺少入口：$footerTarget。" }
    }
    if ([regex]::Matches($docfxConfig.build.globalMetadata._appFooter, [regex]::Escape('[zh-TW]')).Count -lt 5) {
        throw 'modern footer 的正式頁面入口必須明示 zh-TW。'
    }
    Write-Host "PASS：$($catalog.locales.Count) 語系契約驗證通過。"

    # 正體中文（臺灣）用語閘門：概念頁與手寫 C# 註解會直接進入 API 網站，
    # 因此禁止已知的簡體字、陸用詞與曾發生過的 entry／slide 誤譯。
    $zhTwForbiddenTerms = @(
        '单元格',
        '封裝專案',
        'ZIP 專案',
        '影像專案',
        '幻燈片',
        '保存',
        '支持'
    )
    $zhTwContentFiles = @(
        Get-ChildItem api-docs/zh-TW, api-docs/articles -Recurse -File -Include *.md
        Get-Item api-docs/index.md
        Get-ChildItem OdfKit, OdfKit.Extensions.* -Recurse -File -Filter *.cs |
            Where-Object { $_.FullName -notmatch '[\\/]DOM[\\/]Generated[\\/]' }
    )
    $zhTwIssues = [System.Collections.Generic.List[string]]::new()
    foreach ($file in $zhTwContentFiles) {
        $content = [IO.File]::ReadAllText($file.FullName)
        foreach ($term in $zhTwForbiddenTerms) {
            if ($content.Contains($term, [StringComparison]::Ordinal)) {
                $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName)
                $zhTwIssues.Add("$relativePath：$term")
            }
        }
    }
    if ($zhTwIssues.Count) {
        $zhTwIssues | ForEach-Object { Write-Host "  不符合臺灣用語：$_" }
        throw "正體中文（臺灣）用語檢查失敗：$($zhTwIssues.Count) 個檔案／詞彙組合。"
    }
    Write-Host 'PASS：正體中文（臺灣）網站與手寫程式碼註解用語檢查通過。'

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

    $siteDir = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
    $rootPrefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $siteDir.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "OutputDirectory 必須位於專案工作區內：$siteDir。"
    }
    Remove-Item -Recurse -Force $siteDir -ErrorAction Ignore
    if (Test-Path $siteDir) { throw "無法清除 $siteDir（檔案被鎖定或無刪除權限），請關閉占用程式或改用 -OutputDirectory。" }
    dotnet docfx build api-docs/docfx.json --warningsAsErrors --maxParallelism 1 --output $siteDir
    if ($LASTEXITCODE) { throw 'DocFX build 失敗。' }

    # 站內連結健檢：掃描全站 HTML 的相對 href／src，任何指向不存在檔案的連結都視為失敗。
    # Pages 不得以內部連結直接公開 Markdown；次級 repo 文件必須連到 GitHub 渲染頁。
    $broken = [System.Collections.Generic.List[string]]::new()
    $rawMarkdownLinks = [System.Collections.Generic.List[string]]::new()
    $checkedLinks = 0
    Get-ChildItem $siteDir -Recurse -Filter *.html | ForEach-Object {
        $page = $_
        $html = [IO.File]::ReadAllText($page.FullName)
        foreach ($m in [regex]::Matches($html, '(?:href|src)="([^"#]+?)(?:#[^"]*)?"')) {
            $url = $m.Groups[1].Value
            if ($url -eq '' -or $url -match '^(https?:|mailto:|javascript:|data:)') { continue }
            if ($url -match '(?i)\.md(?:$|[?#])') {
                $rawMarkdownLinks.Add("$($page.FullName.Substring($siteDir.Length + 1)) -> $url")
                continue
            }
            $unescaped = [Uri]::UnescapeDataString($url)
            if ($unescaped.StartsWith('/OdfKit/', [StringComparison]::Ordinal)) {
                $candidate = Join-Path $siteDir $unescaped.Substring('/OdfKit/'.Length)
            }
            else {
                $candidate = Join-Path $page.DirectoryName ($unescaped.Replace('/', [IO.Path]::DirectorySeparatorChar))
            }
            $resolved = [IO.Path]::GetFullPath($candidate)
            $checkedLinks++
            if (-not (Test-Path $resolved) -and -not (Test-Path (Join-Path $resolved 'index.html'))) {
                $broken.Add("$($page.FullName.Substring($siteDir.Length + 1)) -> $url")
            }
        }
    }
    if ($rawMarkdownLinks.Count) {
        $rawMarkdownLinks | Select-Object -First 20 | ForEach-Object { Write-Host "  原始 Markdown：$_" }
        throw "網站仍有 $($rawMarkdownLinks.Count) 條內部連結直接指向 Markdown。"
    }
    if ($broken.Count) {
        $broken | Select-Object -First 20 | ForEach-Object { Write-Host "  失效：$_" }
        throw "站內連結健檢失敗：$($broken.Count) 條連結指向不存在的檔案（共檢查 $checkedLinks 條）。"
    }
    Write-Host "PASS：站內連結健檢通過（$checkedLinks 條相對連結，0 失效）。"

    $requiredOutputs = @(
        'project-docs/ip-compliance.html',
        'project-docs/security-limits.html',
        'project-docs/evidence-index.html',
        'project-docs/THIRD-PARTY-NOTICES.html',
        'sitemap.xml',
        'index.json'
    )
    foreach ($requiredOutput in $requiredOutputs) {
        if (-not (Test-Path (Join-Path $siteDir $requiredOutput))) { throw "API 網站缺少必要輸出：$requiredOutput。" }
    }
    $allowedProjectDocs = @(
        'THIRD-PARTY-NOTICES.html',
        'evidence-index.html',
        'ip-compliance.html',
        'security-limits.html'
    )
    $projectDocsDirectory = Join-Path $siteDir 'project-docs'
    $unexpectedProjectDocs = @(
        Get-ChildItem $projectDocsDirectory -Recurse -File |
            Where-Object { $_.Name -notin $allowedProjectDocs }
    )
    if ($unexpectedProjectDocs.Count) {
        $unexpectedProjectDocs | ForEach-Object { Write-Host "  未核准資源：$($_.FullName.Substring($siteDir.Length + 1))" }
        throw 'project-docs 只能發布四個權威 HTML 頁面。'
    }
    $unresolvedXrefs = [System.Collections.Generic.List[string]]::new()
    Get-ChildItem $siteDir -Recurse -File |
        Where-Object { $_.Extension -in @('.html', '.json', '.js', '.yml') } |
        ForEach-Object {
            $content = [IO.File]::ReadAllText($_.FullName)
            if ($content -match 'href=["'']xref:|(?m)^\s*href:\s*xref:|xref:OdfKit(?:["'']|\s|$)') {
                $unresolvedXrefs.Add($_.FullName.Substring($siteDir.Length + 1))
            }
        }
    if ($unresolvedXrefs.Count) {
        $unresolvedXrefs | Select-Object -First 20 | ForEach-Object { Write-Host "  未解析 xref：$_" }
        throw "modern 模板輸出仍含未解析 xref：$($unresolvedXrefs.Count) 個檔案。"
    }
    $htmlFiles = @(Get-ChildItem $siteDir -Recurse -Filter *.html)
    if ($htmlFiles.Count -lt 596) { throw "API 網站頁數異常：$($htmlFiles.Count) < 596。" }
    $contentHtmlFiles = @($htmlFiles | Where-Object { $_.Name -ne 'toc.html' })
    foreach ($page in $contentHtmlFiles) {
        $html = [IO.File]::ReadAllText($page.FullName)
        foreach ($footerTarget in @('/OdfKit/articles/license.html', '/OdfKit/project-docs/ip-compliance.html', '/OdfKit/project-docs/THIRD-PARTY-NOTICES.html', '/OdfKit/project-docs/security-limits.html', '/OdfKit/project-docs/evidence-index.html')) {
            if ($html -notmatch [regex]::Escape($footerTarget)) {
                throw "modern footer 驗證失敗：$($page.FullName.Substring($siteDir.Length + 1)) 缺少 $footerTarget。"
            }
        }
    }
    foreach ($locale in $catalog.locales) {
        foreach ($pageName in @('index.html', 'guide.html')) {
            $pagePath = Join-Path $siteDir "$locale/$pageName"
            $html = [IO.File]::ReadAllText($pagePath)
            $langPattern = '<html[^>]+lang=["'']{0}["'']' -f [regex]::Escape($locale)
            if ($html -notmatch $langPattern) {
                throw "$locale/$pageName 的 HTML lang 不正確。"
            }
        }
    }
    $apiSamplePath = Join-Path $siteDir 'api/OdfKit.Spreadsheet.OdsStreamReader.html'
    $apiSample = [IO.File]::ReadAllText($apiSamplePath)
    if ($apiSample -notmatch 'Provides the OdsStreamReader API\.' -or $apiSample -notmatch '以低記憶體流式方式逐列讀取 ODS 試算表') {
        throw 'API member 雙語內容驗證失敗：OdsStreamReader 頁面缺少英文或正體中文摘要。'
    }
    Write-Host "PASS：modern 模板、12 語系 lang、footer、sitemap 與 $($htmlFiles.Count) 個 HTML 頁面驗證通過。"
}
finally { Pop-Location }
