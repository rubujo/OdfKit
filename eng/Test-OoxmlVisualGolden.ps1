#Requires -Version 7.0
<#
.SYNOPSIS
    執行 OOXML 轉換視覺 golden 驗收（Wave 3 Q-3）。
.DESCRIPTION
    檢查 Windows、LibreOffice 26.x、Microsoft Office COM 與 Python PDF 比對依賴，
    並執行 OfficeInteropConversionTests。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER Framework
    測試目標框架，預設 net8.0。
.PARAMETER RequireEnvironment
    若環境不完整則以 exit 1 結束；預設略過（exit 0）。
.PARAMETER NoBuild
    略過建置，直接執行測試。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0",
    [switch]$RequireEnvironment,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repoRoot "OdfKit.Tests/OdfKit.Tests.csproj"
$manifestPath = Join-Path $repoRoot "tests/fixtures/ooxml-visual-golden/manifest.json"
$diffScriptPath = Join-Path $repoRoot "eng/scripts/PdfVisualDiff.py"

function Test-ProgIdAvailable {
    param([string]$ProgId)

    if (-not $IsWindows) {
        return $false
    }

    try {
        $type = [Type]::GetTypeFromProgID($ProgId)
        return $null -ne $type
    }
    catch {
        return $false
    }
}

function Test-PythonPdfDiffReady {
    param([string]$PythonPath)

    if ([string]::IsNullOrWhiteSpace($PythonPath) -or -not (Test-Path -LiteralPath $PythonPath)) {
        return $false
    }

    $check = @"
import importlib.util
mods = ('numpy', 'PIL', 'pypdfium2')
missing = [m for m in mods if importlib.util.find_spec(m) is None]
raise SystemExit(1 if missing else 0)
"@

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $PythonPath
    $psi.ArgumentList.Add("-c")
    $psi.ArgumentList.Add($check)
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardOutput = $true
    $psi.UseShellExecute = $false

    $process = [System.Diagnostics.Process]::Start($psi)
    if (-not $process.WaitForExit(30000)) {
        return $false
    }

    return $process.ExitCode -eq 0
}

function Get-EnvironmentIssues {
    $issues = New-Object System.Collections.Generic.List[string]

    if (-not $IsWindows) {
        [void]$issues.Add("非 Windows 平台（Office COM 不可用）")
    }

    $sofficePath = [Environment]::GetEnvironmentVariable("ODFKIT_SOFFICE_PATH")
    if ([string]::IsNullOrWhiteSpace($sofficePath)) {
        [void]$issues.Add("未設定 ODFKIT_SOFFICE_PATH")
    }

    if (-not (Test-ProgIdAvailable -ProgId "Word.Application")) {
        [void]$issues.Add("找不到 Word.Application COM")
    }

    if (-not (Test-ProgIdAvailable -ProgId "Excel.Application")) {
        [void]$issues.Add("找不到 Excel.Application COM")
    }

    $pythonPath = [Environment]::GetEnvironmentVariable("ODFKIT_PDF_RENDERER_PYTHON")
    if (-not (Test-PythonPdfDiffReady -PythonPath $pythonPath)) {
        [void]$issues.Add("ODFKIT_PDF_RENDERER_PYTHON 未設定或缺少 numpy/Pillow/pypdfium2")
    }

    if (-not (Test-Path -LiteralPath $manifestPath)) {
        [void]$issues.Add("找不到 manifest：$manifestPath")
    }

    if (-not (Test-Path -LiteralPath $diffScriptPath)) {
        [void]$issues.Add("找不到 PdfVisualDiff.py：$diffScriptPath")
    }

    return $issues
}

Push-Location $repoRoot
try {
    $issues = Get-EnvironmentIssues
    if ($issues.Count -gt 0) {
        $message = "OOXML 視覺 golden 環境不完整，測試將略過：`n- " + ($issues -join "`n- ")
        if ($RequireEnvironment) {
            throw $message
        }

        Write-Host $message
        exit 0
    }

    Write-Host "OOXML 視覺 golden 環境就緒；執行 OfficeInteropConversionTests。"
    Write-Host "Manifest：$manifestPath"

    if (-not $NoBuild) {
        dotnet build $testProject -c $Configuration --framework $Framework
    }

    $testArgs = @(
        "test",
        $testProject,
        "-c", $Configuration,
        "--framework", $Framework,
        "--filter", "FullyQualifiedName~OfficeInteropConversionTests",
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