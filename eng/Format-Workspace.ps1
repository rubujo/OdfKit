#Requires -Version 7.0
<#
.SYNOPSIS
    格式化 OdfKit 工作區，排除會觸發 Visual Studio 合併標記的 OdfKit.Tests 專案。

.DESCRIPTION
    對整個 solution 執行 dotnet format 時，雙目標 OdfKit.Tests(net8.0/net10.0) 可能注入
    Git 合併衝突標記（Visual Studio TODO 取消合併）並導致 CS8300。本腳本僅格式化主程式庫與擴充套件專案。
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$projects = @(
    'OdfKit/OdfKit.csproj'
    'OdfKit.Extensions.Html/OdfKit.Extensions.Html.csproj'
    'OdfKit.Extensions.Imaging/OdfKit.Extensions.Imaging.csproj'
    'OdfKit.Extensions.Ooxml/OdfKit.Extensions.Ooxml.csproj'
    'OdfKit.Extensions.Pdf/OdfKit.Extensions.Pdf.csproj'
    'OdfKit.Extensions.Rendering/OdfKit.Extensions.Rendering.csproj'
    'OdfKit.Extensions.Rdf/OdfKit.Extensions.Rdf.csproj',
    'OdfKit.Extensions.Collaboration/OdfKit.Extensions.Collaboration.csproj'
    'tools/OdfKit.Cli/OdfKit.Cli.csproj'
    'tools/OdfSchemaGenerator/OdfSchemaGenerator.csproj'
)

foreach ($rel in $projects) {
    $path = Join-Path $root $rel
    if (-not (Test-Path $path)) {
        Write-Warning "略過不存在的專案：$rel"
        continue
    }
    Write-Host "Formatting $rel ..."
    dotnet format $path --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet format 失敗：$rel" }
}

Write-Host 'Done. 注意：請勿對整個 solution 執行 dotnet format，以避免 OdfKit.Tests 合併標記。'