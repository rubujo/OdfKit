#Requires -Version 7.0
[CmdletBinding()]
param([switch]$NoRestore, [switch]$ReuseSiteTemplate)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not $NoRestore -and -not $ReuseSiteTemplate) { dotnet tool restore; if ($LASTEXITCODE) { throw 'DocFX tool restore 失敗。' } }
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
    if (-not $ReuseSiteTemplate) {
        foreach ($project in $projects) {
            dotnet build $project -c Release -f net10.0 --no-restore /p:ODFKIT_PUBLICAPI_BASELINE=1
            if ($LASTEXITCODE) { throw "API 文件組件建置失敗：$project" }
        }
        Remove-Item -Recurse -Force api-docs/api, artifacts/api-site-template -ErrorAction SilentlyContinue
        dotnet docfx metadata api-docs/docfx.json
        if ($LASTEXITCODE) { throw 'DocFX metadata 產生失敗。' }
        dotnet docfx build api-docs/docfx.json --warningsAsErrors --maxParallelism 1
        if ($LASTEXITCODE) { throw 'DocFX build 失敗。' }
    }
    Remove-Item -Recurse -Force artifacts/api-site -ErrorAction SilentlyContinue
    $catalog = Get-Content api-docs/locales.json -Raw | ConvertFrom-Json
    New-Item -ItemType Directory artifacts/api-site -Force | Out-Null
    Copy-Item artifacts/api-site-template artifacts/api-site/reference -Recurse
    foreach ($locale in $catalog.locales) {
        $destination = Join-Path artifacts/api-site $locale
        New-Item -ItemType Directory $destination | Out-Null
        $displayName = $catalog.displayNames.$locale
        $fallback = if ($locale -eq 'zh-TW') { 'API 文件沿用英文與正體中文 XML 摘要。' }
            else { 'API member content currently falls back to English and Traditional Chinese (Taiwan).' }
        $landing = @"
<!doctype html><html lang="$locale"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>OdfKit API Reference - $displayName</title><body><main><h1>OdfKit API Reference</h1>
<p>Language: $displayName</p><p>$fallback</p>
<p><strong>AI disclosure:</strong> This project content is written, organized, or produced with AI tools.</p>
<p>Original OdfKit content is CC0-1.0. Third-party content retains its own license. This is not an official OASIS, TDF, LibreOffice, or Apache project. No SLA or commercial indemnity is provided.</p>
<p><a href="../reference/articles/index.html">Open searchable API reference</a></p></main></body></html>
"@
        [IO.File]::WriteAllText((Join-Path $destination 'index.html'), $landing, [Text.UTF8Encoding]::new($false))
    }
    $redirect = '<!doctype html><html lang="zh-TW"><meta charset="utf-8"><meta http-equiv="refresh" content="0;url=zh-TW/"><title>OdfKit API Reference</title></html>'
    [IO.File]::WriteAllText((Join-Path $root 'artifacts/api-site/index.html'), $redirect, [Text.UTF8Encoding]::new($false))
}
finally { Pop-Location }
