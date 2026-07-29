#Requires -Version 7.0
<#
.SYNOPSIS
    執行 Apache OpenOffice 4.1.x 實機互通性測試。
.DESCRIPTION
    偵測真實 Apache OpenOffice soffice，並執行獨立的 ApacheOpenOfficeInteropTests。
    若找不到 binary，預設以略過結束；加上 -RequireOpenOffice 則視為失敗。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER Framework
    測試目標框架，預設 net10.0。
.PARAMETER SofficePath
    可選的 soffice 路徑；未指定時沿用 ODFKIT_OPENOFFICE_PATH 與常見安裝路徑。
.PARAMETER RequireOpenOffice
    若找不到 Apache OpenOffice 4.1.x 則視為失敗。
.PARAMETER NoBuild
    略過建置，直接執行測試。
.PARAMETER DetectOnly
    僅執行路徑與版本探測。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0",
    [string]$SofficePath = "",
    [switch]$RequireOpenOffice,
    [switch]$NoBuild,
    [switch]$DetectOnly
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repoRoot "OdfKit.Tests/OdfKit.Tests.csproj"

function Resolve-OpenOfficeSoffice {
    param([string]$Candidate)

    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        return $null
    }

    if (Test-Path -LiteralPath $Candidate -PathType Leaf) {
        return (Resolve-Path -LiteralPath $Candidate).Path
    }

    if (-not (Test-Path -LiteralPath $Candidate -PathType Container)) {
        return $null
    }

    foreach ($relativePath in @("soffice.exe", "soffice", "program/soffice.exe", "program/soffice")) {
        $executable = Join-Path $Candidate $relativePath
        if (Test-Path -LiteralPath $executable -PathType Leaf) {
            return (Resolve-Path -LiteralPath $executable).Path
        }
    }

    return $null
}

function Get-OpenOfficeVersionText {
    param([string]$Executable)

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.ArgumentList.Add("--version")
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        if (-not $process.WaitForExit(15000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Apache OpenOffice --version 逾時：$Executable"
        }

        return ($process.StandardOutput.ReadToEnd() + $process.StandardError.ReadToEnd()).Trim()
    }
    finally {
        $process.Dispose()
    }
}

function Find-ApacheOpenOfficeSoffice {
    $candidates = [System.Collections.Generic.List[string]]::new()
    foreach ($candidate in @(
        $SofficePath,
        [Environment]::GetEnvironmentVariable("ODFKIT_OPENOFFICE_PATH"),
        "C:\Program Files\OpenOffice 4\program\soffice.exe",
        "C:\Program Files (x86)\OpenOffice 4\program\soffice.exe"
    )) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            [void]$candidates.Add($candidate)
        }
    }

    foreach ($candidate in $candidates) {
        $executable = Resolve-OpenOfficeSoffice -Candidate $candidate
        if ([string]::IsNullOrWhiteSpace($executable)) {
            continue
        }

        if ($IsWindows) {
            $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
            if ($versionInfo.FileDescription -notmatch "OpenOffice" -or
                $versionInfo.CompanyName -notmatch "Apache Software Foundation") {
                continue
            }
        }

        $versionText = Get-OpenOfficeVersionText -Executable $executable
        if ($versionText -match "Apache OpenOffice 4\.1\.") {
            return [PSCustomObject]@{
                Path = $executable
                Version = ($versionText -split "`n" | Select-Object -First 1)
            }
        }
    }

    return $null
}

Push-Location $repoRoot
try {
    $soffice = Find-ApacheOpenOfficeSoffice
    if ($null -eq $soffice) {
        $message = "找不到 Apache OpenOffice 4.1.x soffice；可設定 ODFKIT_OPENOFFICE_PATH 或 -SofficePath。"
        if ($RequireOpenOffice) {
            throw $message
        }

        Write-Host $message
        return
    }

    Write-Host "使用 Apache OpenOffice：$($soffice.Path)"
    Write-Host "版本：$($soffice.Version)"
    if ($DetectOnly) {
        return
    }

    $previousPath = $env:ODFKIT_OPENOFFICE_PATH
    $previousRequired = $env:ODFKIT_REQUIRE_OPENOFFICE
    $env:ODFKIT_OPENOFFICE_PATH = $soffice.Path
    $env:ODFKIT_REQUIRE_OPENOFFICE = if ($RequireOpenOffice) { "true" } else { $null }
    try {
        if (-not $NoBuild) {
            dotnet build $testProject -c $Configuration --framework $Framework
            if ($LASTEXITCODE -ne 0) {
                throw "Apache OpenOffice 互通測試建置失敗，結束碼 $LASTEXITCODE。"
            }
        }

        $arguments = @(
            "test",
            $testProject,
            "-c", $Configuration,
            "--framework", $Framework,
            "--filter", "FullyQualifiedName~ApacheOpenOfficeInteropTests",
            "--no-restore"
        )
        if ($NoBuild) {
            $arguments += "--no-build"
        }

        dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Apache OpenOffice 互通測試失敗，結束碼 $LASTEXITCODE。"
        }
    }
    finally {
        $env:ODFKIT_OPENOFFICE_PATH = $previousPath
        $env:ODFKIT_REQUIRE_OPENOFFICE = $previousRequired
    }
}
finally {
    Pop-Location
}
