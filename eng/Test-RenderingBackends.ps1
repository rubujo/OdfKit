#Requires -Version 7.0
<#
.SYNOPSIS
    執行 OdfKit.Extensions.Rendering 相關單元測試（Wave 3 REN-1）。
.DESCRIPTION
    執行 Mock 後端與本機程序後端測試，不需真實 LibreOffice 或 Unoserver 服務。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER Framework
    測試目標框架，預設 net8.0。
.PARAMETER NoBuild
    略過建置，直接執行測試。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repoRoot "OdfKit.Tests/OdfKit.Tests.csproj"
$filter = "FullyQualifiedName~LibreOfficeHttpRendererTests|FullyQualifiedName~LocalProcessBackendAsyncCancellationTests|FullyQualifiedName~PresentationAndRenderingTests"

Push-Location $repoRoot
try {
    if (-not $NoBuild) {
        dotnet build $testProject -c $Configuration --framework $Framework
    }

    $testArgs = @(
        "test",
        $testProject,
        "-c", $Configuration,
        "--framework", $Framework,
        "--filter", $filter,
        "--no-restore"
    )

    if ($NoBuild) {
        $testArgs += "--no-build"
    }

    dotnet @testArgs
}
finally {
    Pop-Location
}