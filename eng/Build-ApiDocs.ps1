#Requires -Version 7.0
<#
.SYNOPSIS
    建置 17 語系 GitHub Pages API reference 站台（DocFX 站內多語系結構）。
.DESCRIPTION
    站台結構與語系契約見 docs/api-docs-site.md。流程：
    組件建置 → 語系契約驗證 → docfx metadata → 未渲染頁面 href 修復 → docfx build → 站內連結健檢。
    輸出至 artifacts/api-site，供 .github/workflows/api-docs.yml 部署。
.PARAMETER NoRestore
    略過 dotnet tool restore（本機反覆執行時使用）。
.PARAMETER SkipProjectBuild
    略過 21 個公開套件組件的 dotnet build（組件輸出已存在且未變更時使用）。
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

    & pwsh eng/Test-ApiDocsTranslations.ps1 -FailOnIssues
    if ($LASTEXITCODE) { throw 'DocFX 正式文件翻譯契約驗證失敗。' }

    # 語系契約驗證：locales.json 是語系目錄的單一事實來源，每個語系必須有站內入口頁，
    # 且根層 index.md 必須連到每個語系（否則語系入口無法被發現）。
    $catalog = Get-Content api-docs/locales.json -Raw | ConvertFrom-Json
    $rootIndex = Get-Content api-docs/index.md -Raw
    $docfxJson = Get-Content api-docs/docfx.json -Raw
    $docfxConfig = $docfxJson | ConvertFrom-Json
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
        $requiredOfficialLinks = if ($locale -eq 'zh-TW') {
            @('../articles/license.md', '../../docs/ip-compliance.md', '../../THIRD-PARTY-NOTICES.md', '../../docs/security-limits.md', '../../docs/evidence-index.md')
        } else {
            @('articles/license.md', 'project-docs/ip-compliance.md', 'project-docs/THIRD-PARTY-NOTICES.md', 'project-docs/security-limits.md', 'project-docs/evidence-index.md')
        }
        if ($localeContent -notmatch "(?m)^_lang:\s*$([regex]::Escape($locale))\s*$") { throw "$localePath 缺少正確的 _lang metadata。" }
        if ($localeContent -notmatch [regex]::Escape('(xref:OdfKit)')) { throw "$localePath 缺少 API reference 入口。" }
        if ($localeContent -notmatch [regex]::Escape('[en + zh-TW]')) { throw "$localePath 的 API 入口缺少內容語系標示。" }
        if ($localeContent -notmatch [regex]::Escape('(guide.md)')) { throw "$localePath 缺少語系指南連結。" }
        if ($localeContent -notmatch 'CC0-1\.0' -or $localeContent -notmatch '\b(AI|KI|IA)\b') { throw "$localePath 缺少授權或 AI 產製聲明。" }
        if ($guideContent -notmatch "(?m)^_lang:\s*$([regex]::Escape($locale))\s*$") { throw "$guidePath 缺少正確的 _lang metadata。" }
        foreach ($required in @('PackageFidelity', 'SemanticApiDepth', 'InteropEvidence', 'CC0-1.0', 'xref:OdfKit')) {
            if ($guideContent -notmatch [regex]::Escape($required)) { throw "$guidePath 缺少必要內容：$required。" }
        }
        foreach ($requiredLink in $requiredOfficialLinks) {
            if ($guideContent -notmatch [regex]::Escape($requiredLink)) { throw "$guidePath 缺少正式聲明連結：$requiredLink。" }
            if ($tocContent -notmatch [regex]::Escape($requiredLink)) { throw "$tocPath 缺少正式聲明連結：$requiredLink。" }
        }
        if ($tocContent -match '(?m)^\s*href:\s*xref:') { throw "$tocPath 不得以 href: xref:* 指向 API；請使用 DocFX uid。" }
        if ($tocContent -notmatch '(?m)^\s*uid:\s*OdfKit\s*$') { throw "$tocPath 缺少以 uid 指定的共用 API reference 入口。" }
        if ($tocContent -notmatch [regex]::Escape('[en + zh-TW]')) { throw "$tocPath 的 API 入口缺少內容語系標示。" }
        if ($guideContent -notmatch [regex]::Escape('[en + zh-TW]')) { throw "$guidePath 的 API 入口缺少內容語系標示。" }
        if ($locale -notin @('en', 'zh-TW') -and $localeContent -match 'Open the API reference|Site notes and compliance|Other languages|This project content is written|Original OdfKit content') {
            throw "$localePath 仍含英文 placeholder，尚未完成本地化。"
        }
        if ($rootIndex -notmatch [regex]::Escape("$locale/index.md")) { throw "api-docs/index.md 缺少語系來源連結：$locale。" }
        if ($docfxJson -notmatch [regex]::Escape("$locale/**.{md,yml}")) { throw "api-docs/docfx.json 的 build.content 缺少語系文件集合：$locale/**.{md,yml}。" }
        $fileMetadataLocale = $docfxConfig.build.fileMetadata._lang.PSObject.Properties["$locale/**"].Value
        if ($fileMetadataLocale -ne $locale) { throw "api-docs/docfx.json 的 fileMetadata._lang 缺少或錯誤：$locale。" }
    }
    if ($rootIndex -match '(?m)^redirect_url\s*:') { throw '根首頁不得設定 redirect_url；必須保留語言選擇頁。' }
    $rootToc = Get-Content api-docs/toc.yml -Raw
    if ($rootToc -notmatch [regex]::Escape('API [en + zh-TW]')) { throw '根 navbar 的 API 入口缺少內容語系標示。' }
    if (@($docfxConfig.build.template) -notcontains 'modern') { throw 'DocFX 必須使用官方 modern 模板。' }
    if (@($docfxConfig.build.template) -notcontains 'template') { throw 'DocFX 必須套用 api-docs/template 自訂樣式。' }
    $projectDocsContent = @($docfxConfig.build.content | Where-Object { $_.src -eq '../docs' })
    if ($projectDocsContent.Count -ne 1 -or
        @($projectDocsContent[0].files) -notcontains '**/*.md' -or
        @($projectDocsContent[0].files) -notcontains 'toc.yml' -or
        $projectDocsContent[0].dest -ne 'project-docs') {
        throw 'DocFX 必須將 docs 下的 Markdown 與 toc.yml 完整發布到 project-docs。'
    }
    $projectDocsResources = @($docfxConfig.build.resource | Where-Object { $_.src -eq '../docs' })
    if ($projectDocsResources.Count -ne 1 -or
        @($projectDocsResources[0].files) -notcontains '**/*.json' -or
        $projectDocsResources[0].dest -ne 'project-docs') {
        throw 'DocFX 必須將 docs 下的 JSON 證據資源發布到 project-docs。'
    }
    if ($docfxConfig.build.globalMetadata._appLogoPath -ne 'images/odfkit-mark.svg') {
        throw 'modern 導覽列缺少 OdfKit 標誌。'
    }
    if ($docfxConfig.build.globalMetadata._appFooter -notmatch [regex]::Escape('/OdfKit/index.html')) {
        throw 'modern footer 缺少語言選擇頁入口。'
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
        '支持',
        '生成',
        '運行',
        '進程',
        '行程',
        '數據',
        '代碼生成',
        '代碼頁',
        '大數據',
        '調用',
        '回退',
        '單個',
        '執行程序',
        '加載',
        '本地名稱',
        '本地資料',
        '本地快取',
        '本地進程',
        '本地 LibreOffice',
        '本地 NuGet',
        '本地 nupkg',
        '本地時間',
        '本地 href',
        '基於本地'
    )
    # 術語表會列出其他語言的正式譯名；這些項目不是正體中文內容。
    $zhTwAllowedOccurrences = @{
        'docs\i18n-glossary.md|保存' = 1
    }
    $zhTwContentFiles = @(
        Get-ChildItem api-docs/zh-TW, api-docs/articles -Recurse -File -Include *.md
        Get-Item api-docs/index.md
        Get-ChildItem docs -Recurse -File -Include *.md
        Get-Item README.md, CHANGELOG.md, THIRD-PARTY-NOTICES.md, AGENTS.md,
            eng/README.md, eng/historical-refactor/README.md, samples/README.md,
            samples/WebFonts.AspNetCore/README.md, samples/WebFonts.WebForms/README.md,
            tools/README.md, OdfKit/PublicAPI/README.md, OdfKit/Compliance/i18n/README.md
        Get-ChildItem OdfKit, OdfKit.Extensions.*, OdfKit.WebFonts.* -Recurse -File -Filter *.cs |
            Where-Object {
                $_.FullName -notmatch '[\\/]DOM[\\/]Generated[\\/]' -and
                $_.Name -notmatch '^OdfLocalizer\.Exceptions\.[a-z]{2}(?:-[A-Z]{2})?\.cs$'
            }
    )
    $zhTwIssues = [System.Collections.Generic.List[string]]::new()
    foreach ($file in $zhTwContentFiles) {
        $content = [IO.File]::ReadAllText($file.FullName)
        foreach ($term in $zhTwForbiddenTerms) {
            $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName)
            $occurrenceCount = [regex]::Matches($content, [regex]::Escape($term)).Count
            $allowKey = "$relativePath|$term"
            $allowedCount = if ($zhTwAllowedOccurrences.ContainsKey($allowKey)) {
                $zhTwAllowedOccurrences[$allowKey]
            } else {
                0
            }
            if ($occurrenceCount -gt $allowedCount) {
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
            @{ Path = 'OdfKit/OdfKit.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.Extensions.Collaboration/OdfKit.Extensions.Collaboration.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.Extensions.Scripting/OdfKit.Extensions.Scripting.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.Extensions.Html/OdfKit.Extensions.Html.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.Extensions.Imaging/OdfKit.Extensions.Imaging.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.Extensions.Ooxml/OdfKit.Extensions.Ooxml.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.Extensions.Pdf/OdfKit.Extensions.Pdf.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.Extensions.Rdf/OdfKit.Extensions.Rdf.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.Extensions.Rendering/OdfKit.Extensions.Rendering.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.WebFonts.Abstractions/OdfKit.WebFonts.Abstractions.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.WebFonts.Encoding.Legacy/OdfKit.WebFonts.Encoding.Legacy.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.WebFonts.Data.SqlServer/OdfKit.WebFonts.Data.SqlServer.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.WebFonts.OpenType/OdfKit.WebFonts.OpenType.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.WebFonts.Build/OdfKit.WebFonts.Build.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.WebFonts.Worker/OdfKit.WebFonts.Worker.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.WebFonts.Sidecar/OdfKit.WebFonts.Sidecar.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.WebFonts.Profiles/OdfKit.WebFonts.Profiles.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.WebFonts.Windows/OdfKit.WebFonts.Windows.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.WebFonts.Hosting.AspNetCore/OdfKit.WebFonts.Hosting.AspNetCore.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.Extensions.Html.WebFonts/OdfKit.Extensions.Html.WebFonts.csproj'; Framework = 'net10.0' },
            @{ Path = 'OdfKit.WebFonts.Hosting.SystemWeb/OdfKit.WebFonts.Hosting.SystemWeb.csproj'; Framework = 'net48' }
        )
        foreach ($project in $projects) {
            dotnet build $project.Path `
                -c Release `
                -f $project.Framework `
                -p:NuGetAudit=false `
                -p:ODFKIT_PUBLICAPI_BASELINE=1
            if ($LASTEXITCODE) { throw "API 文件組件建置失敗：$($project.Path)" }
        }
    }

    # net48 將 System.ValueTuple 視為 framework facade，因此一般建置不會複製 DLL；
    # DocFX metadata 的獨立組件解析器仍需要同目錄參考。版本取自已審核的 WebFont
    # 相依政策，並只複製 NuGet 套件的 managed facade，不引入執行期或產品相依捷徑。
    $dependencyPolicy = Get-Content eng/webfont-dependency-policy.json -Raw | ConvertFrom-Json
    $valueTuplePackage = @($dependencyPolicy.packages | Where-Object { $_.id -eq 'System.ValueTuple' })
    if ($valueTuplePackage.Count -ne 1) { throw 'WebFont 相依政策必須恰好宣告一個 System.ValueTuple 版本。' }
    $globalPackagesOutput = (& dotnet nuget locals global-packages --list --force-english-output 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -or $globalPackagesOutput -notmatch '^global-packages:\s*(.+)$') {
        throw "無法解析 NuGet global-packages 路徑：$globalPackagesOutput。"
    }
    $valueTupleSource = Join-Path $Matches[1] "system.valuetuple/$($valueTuplePackage[0].version)/lib/net462/System.ValueTuple.dll"
    if (-not (Test-Path -LiteralPath $valueTupleSource)) { throw "缺少 DocFX net48 facade：$valueTupleSource。" }
    $systemWebOutput = 'OdfKit.WebFonts.Hosting.SystemWeb/bin/Release/net48'
    Copy-Item -LiteralPath $valueTupleSource -Destination $systemWebOutput -Force
    Write-Host "PASS：DocFX net48 System.ValueTuple $($valueTuplePackage[0].version) facade 已就緒。"

    Remove-Item -Recurse -Force api-docs/api -ErrorAction Ignore
    if (Test-Path api-docs/api) { throw '無法清除 api-docs/api（檔案可能被鎖定），請關閉占用程式後重試。' }
    dotnet docfx metadata api-docs/docfx.json --warningsAsErrors
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

    # 官方 modern 模板目前未提供 zh-TW UI 字串。全站導覽本來就採中英雙語，
    # 因此將共用操作字串補成相同形式，避免中文文件中心混入未說明的英文控制項。
    $uiReplacements = [ordered]@{
        'content="In this article"' = 'content="本頁內容 / In this article"'
        '>In this article<' = '>本頁內容 / In this article<'
        'placeholder="Search"' = 'placeholder="搜尋 / Search"'
        'aria-label="Search"' = 'aria-label="搜尋 / Search"'
        'placeholder="Filter by title"' = 'placeholder="依標題篩選 / Filter by title"'
        'content="Filter by title"' = 'content="依標題篩選 / Filter by title"'
        '>Table of Contents<' = '>目錄 / Table of Contents<'
        'aria-label="Show table of contents"' = 'aria-label="顯示目錄 / Show table of contents"'
        'aria-label="Close"' = 'aria-label="關閉 / Close"'
    }
    $localizedUiPages = 0
    Get-ChildItem $siteDir -Recurse -Filter *.html | ForEach-Object {
        $html = [IO.File]::ReadAllText($_.FullName)
        $updated = $html
        foreach ($entry in $uiReplacements.GetEnumerator()) {
            $updated = $updated.Replace($entry.Key, $entry.Value, [StringComparison]::Ordinal)
        }
        if ($updated -ne $html) {
            [IO.File]::WriteAllText($_.FullName, $updated)
            $localizedUiPages++
        }
    }
    if (-not $localizedUiPages) { throw 'DocFX 共用操作介面沒有任何頁面完成雙語化。' }
    Write-Host "已將 $localizedUiPages 個頁面的 DocFX 共用操作介面補為中英雙語。"

    # 同一儲存庫且已發布的文件必須留在靜態網站內，避免導覽跳回 GitHub 原始 Markdown。
    $publishedRepoPaths = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    Get-ChildItem docs -Recurse -File | ForEach-Object {
        $repoPath = [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
        $docsPath = [IO.Path]::GetRelativePath((Join-Path $root 'docs'), $_.FullName).Replace('\', '/')
        if ($_.Extension -eq '.md') {
            $publishedRepoPaths[$repoPath] = 'project-docs/' + [IO.Path]::ChangeExtension($docsPath, '.html')
        }
        elseif ($_.Extension -eq '.json') {
            $publishedRepoPaths[$repoPath] = 'project-docs/' + $docsPath
        }
        elseif ($_.Name -eq 'toc.yml') {
            $publishedRepoPaths[$repoPath] = 'project-docs/' + [IO.Path]::ChangeExtension($docsPath, '.html')
        }
    }
    $repositoryContentFiles = @(
        'README.md',
        'CHANGELOG.md',
        'THIRD-PARTY-NOTICES.md',
        'AGENTS.md',
        'eng/README.md',
        'eng/historical-refactor/README.md',
        'samples/README.md',
        'samples/OdfKit.HighLevelApi/README.md',
        'samples/WebFonts.AspNetCore/README.md',
        'samples/WebFonts.WebForms/README.md',
        'tools/README.md',
        'OdfKit/PublicAPI/README.md',
        'OdfKit/Compliance/i18n/README.md'
    )
    foreach ($repoPath in $repositoryContentFiles) {
        $publishedRepoPaths[$repoPath] = 'project-docs/' + [IO.Path]::ChangeExtension($repoPath, '.html')
    }
    $rewrittenRepositoryLinks = 0
    Get-ChildItem $siteDir -Recurse -Filter *.html | ForEach-Object {
        $page = $_
        $html = [IO.File]::ReadAllText($page.FullName)
        $rewritten = [regex]::Replace(
            $html,
            'https://github\.com/rubujo/OdfKit/blob/main/([^"?#]+)((?:[?#][^"\s]*)?)',
            {
                param($match)
                $repoPath = [Uri]::UnescapeDataString($match.Groups[1].Value)
                if (-not $publishedRepoPaths.ContainsKey($repoPath)) { return $match.Value }
                $target = Join-Path $siteDir $publishedRepoPaths[$repoPath]
                $relative = [IO.Path]::GetRelativePath($page.DirectoryName, $target).Replace('\', '/')
                $script:rewrittenRepositoryLinks++
                return $relative + $match.Groups[2].Value
            },
            [Text.RegularExpressions.RegexOptions]::IgnoreCase,
            [TimeSpan]::FromSeconds(5))
        if ($rewritten -ne $html) { [IO.File]::WriteAllText($page.FullName, $rewritten) }
    }
    Write-Host "已將 $rewrittenRepositoryLinks 條同一儲存庫文件連結改為站內靜態資源。"

    # 404 頁面後處理：GitHub Pages 會在任意深度的缺失路徑下回傳 404.html 內容，
    # 模板的相對資源與導覽連結在深層路徑會失效，必須注入 <base> 使其一律以站台根解析；
    # 404 頁也不得進入 sitemap，避免搜尋引擎索引錯誤頁。
    $notFoundPath = Join-Path $siteDir '404.html'
    if (-not (Test-Path $notFoundPath)) { throw 'API 網站缺少 404.html（api-docs/404.md 未建置）。' }
    $notFoundBaseUrl = $docfxConfig.build.sitemap.baseUrl
    $notFoundHtml = [IO.File]::ReadAllText($notFoundPath)
    if ($notFoundHtml -notmatch '<base\s') {
        $notFoundHtml = [regex]::Replace($notFoundHtml, '<head(\s[^>]*)?>', ('$0<base href="{0}">' -f $notFoundBaseUrl), 'IgnoreCase', [TimeSpan]::FromSeconds(5))
        [IO.File]::WriteAllText($notFoundPath, $notFoundHtml)
    }
    if ($notFoundHtml -notmatch [regex]::Escape("<base href=""$notFoundBaseUrl"">")) { throw '404.html 缺少站台根 <base> 注入。' }
    $sitemapPath = Join-Path $siteDir 'sitemap.xml'
    $sitemapXml = [IO.File]::ReadAllText($sitemapPath)
    $sitemapXml = [regex]::Replace($sitemapXml, '<url>(?:(?!</url>).)*?404\.html(?:(?!</url>).)*?</url>\s*', '', 'Singleline')
    [IO.File]::WriteAllText($sitemapPath, $sitemapXml)
    if ($sitemapXml -match '404\.html') { throw 'sitemap.xml 不得包含 404 頁面。' }
    Write-Host 'PASS：404.html 已注入 <base> 並自 sitemap 移除。'

    # 站內連結健檢：掃描全站 HTML 的相對 href／src，任何指向不存在檔案的連結都視為失敗。
    # Pages 不得直接連到 Markdown；已發布的同一儲存庫文件也不得繞回 GitHub blob 頁面。
    $broken = [System.Collections.Generic.List[string]]::new()
    $rawMarkdownLinks = [System.Collections.Generic.List[string]]::new()
    $repositoryMarkdownLinks = [System.Collections.Generic.List[string]]::new()
    $checkedLinks = 0
    Get-ChildItem $siteDir -Recurse -Filter *.html | ForEach-Object {
        $page = $_
        $html = [IO.File]::ReadAllText($page.FullName)
        foreach ($m in [regex]::Matches($html, '(?:href|src)="([^"#]+?)(?:#[^"]*)?"')) {
            $url = $m.Groups[1].Value
            if ($url -match '^https://github\.com/rubujo/OdfKit/blob/main/[^"?#]+\.md(?:$|[?#])') {
                $repositoryMarkdownLinks.Add("$($page.FullName.Substring($siteDir.Length + 1)) -> $url")
                continue
            }
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
    if ($repositoryMarkdownLinks.Count) {
        $repositoryMarkdownLinks | Select-Object -First 20 | ForEach-Object { Write-Host "  儲存庫 Markdown：$_" }
        throw "網站仍有 $($repositoryMarkdownLinks.Count) 條連結繞回同一儲存庫的 Markdown。"
    }
    if ($broken.Count) {
        $broken | Select-Object -First 20 | ForEach-Object { Write-Host "  失效：$_" }
        throw "站內連結健檢失敗：$($broken.Count) 條連結指向不存在的檔案（共檢查 $checkedLinks 條）。"
    }
    Write-Host "PASS：站內連結健檢通過（$checkedLinks 條相對連結，0 失效）。"

    $requiredOutputs = @(
        'articles/getting-started.html',
        'articles/package-selection.html',
        'project-docs/ip-compliance.html',
        'project-docs/security-limits.html',
        'project-docs/evidence-index.html',
        'project-docs/index.html',
        'project-docs/migration-high-level-api.html',
        'project-docs/provenance/semantic-api-clean-room.html',
        'project-docs/reference/semantic-facades.html',
        'project-docs/claims.json',
        'project-docs/webfont-managed-architecture.html',
        'project-docs/webfont-sidecar-deployment.html',
        'project-docs/webfont-evidence-matrix.html',
        'project-docs/webfont-ift-tracking.html',
        'project-docs/webfonts.html',
        'project-docs/provenance/webfont-managed-clean-room.html',
        'project-docs/THIRD-PARTY-NOTICES.html',
        'samples/WebFonts.AspNetCore/OdfKit.WebFonts.AspNetCore.Sample.csproj',
        'samples/WebFonts.AspNetCore/Program.cs',
        'samples/WebFonts.AspNetCore/appsettings.WebFont.example.json',
        'samples/WebFonts.AspNetCore/wwwroot/site.css',
        'samples/WebFonts.AspNetCore/wwwroot/webfont-autosubset.js',
        'samples/WebFonts.AspNetCore/wwwroot/webfont-sample.js',
        'samples/WebFonts.WebForms/Default.aspx',
        'samples/WebFonts.WebForms/Web.config',
        'samples/WebFonts.WebForms/WebFontGenerate.ashx',
        'samples/WebFonts.WebForms/webfont-autosubset.js',
        'samples/WebFonts.WebForms/webfont-sample.css',
        'samples/WebFonts.WebForms/webfont-sample.js',
        'samples/WebFonts.WebForms/webfonts.dynamic.example.json',
        'samples/WebFonts.WebForms/webfonts.dynamic.sidecar.example.json',
        'project-docs/eng/Manage-WebFontSidecarService.ps1',
        'sitemap.xml',
        'index.json',
        '404.html'
    )
    foreach ($requiredOutput in $requiredOutputs) {
        if (-not (Test-Path (Join-Path $siteDir $requiredOutput))) { throw "API 網站缺少必要輸出：$requiredOutput。" }
    }
    $allowedProjectDocs = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    Get-ChildItem docs -Recurse -File | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath((Join-Path $root 'docs'), $_.FullName).Replace('\', '/')
        if ($_.Extension -eq '.md' -or $_.Name -eq 'toc.yml') {
            [void]$allowedProjectDocs.Add([IO.Path]::ChangeExtension($relative, '.html'))
        }
        elseif ($_.Extension -eq '.json') {
            [void]$allowedProjectDocs.Add($relative)
        }
    }
    [void]$allowedProjectDocs.Add('THIRD-PARTY-NOTICES.html')
    [void]$allowedProjectDocs.Add('toc.json')
    foreach ($repoPath in $repositoryContentFiles) {
        [void]$allowedProjectDocs.Add([IO.Path]::ChangeExtension($repoPath, '.html'))
    }
    foreach ($repoPath in @('.editorconfig', '.github/workflows/api-docs.yml', 'eng/Build-ApiDocs.ps1', 'eng/Manage-WebFontSidecarService.ps1', 'eng/scripts/PdfVisualDiff.py', 'tests/fixtures/ooxml-visual-golden/manifest.json')) {
        [void]$allowedProjectDocs.Add($repoPath)
    }
    $projectDocsDirectory = Join-Path $siteDir 'project-docs'
    $unexpectedProjectDocs = @(
        Get-ChildItem $projectDocsDirectory -Recurse -File |
            Where-Object {
                $relative = [IO.Path]::GetRelativePath($projectDocsDirectory, $_.FullName).Replace('\', '/')
                -not $allowedProjectDocs.Contains($relative)
            }
    )
    if ($unexpectedProjectDocs.Count) {
        $unexpectedProjectDocs | ForEach-Object { Write-Host "  未核准資源：$($_.FullName.Substring($siteDir.Length + 1))" }
        throw 'project-docs 含有未對應至 docs 來源的輸出。'
    }
    foreach ($asset in @('public/main.css', 'images/odfkit-mark.svg')) {
        if (-not (Test-Path (Join-Path $siteDir $asset))) { throw "API 網站缺少自訂外觀資源：$asset。" }
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
        if ($html -notmatch [regex]::Escape('/OdfKit/index.html')) {
            throw "modern footer 驗證失敗：$($page.FullName.Substring($siteDir.Length + 1)) 缺少語言選擇頁。"
        }
    }
    foreach ($locale in $catalog.locales) {
        $localizedPages = @('index.html', 'guide.html')
        if ($locale -ne 'zh-TW') {
            $localizedPages += @(
                'articles/license.html',
                'project-docs/ip-compliance.html',
                'project-docs/THIRD-PARTY-NOTICES.html',
                'project-docs/security-limits.html',
                'project-docs/evidence-index.html'
            )
        }
        foreach ($pageName in $localizedPages) {
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
    foreach ($requiredUiText in @('本頁內容 / In this article', '搜尋 / Search', '依標題篩選 / Filter by title')) {
        if ($apiSample -notmatch [regex]::Escape($requiredUiText)) {
            throw "API 網站共用介面雙語化驗證失敗：缺少 $requiredUiText。"
        }
    }
    $homePage = [IO.File]::ReadAllText((Join-Path $siteDir 'index.html'))
    foreach ($requiredHomeToken in @('odfkit-home-hero', 'odfkit-language-grid', 'api/OdfKit.html', 'project-docs/index.html')) {
        if ($homePage -notmatch [regex]::Escape($requiredHomeToken)) {
            throw "API 網站首頁體驗驗證失敗：缺少 $requiredHomeToken。"
        }
    }
    Write-Host "PASS：modern 模板、17 語系 lang、footer、sitemap 與 $($htmlFiles.Count) 個 HTML 頁面驗證通過。"
}
finally { Pop-Location }
