#Requires -Version 7.0
<#
.SYNOPSIS
    執行 LibreOffice 26.x 實機互通性測試（Wave 3 X-2）。
.DESCRIPTION
    偵測本機 LibreOffice 26.x soffice，並執行 LibreOfficeInteropTests。
    若找不到 LibreOffice，預設以略過結束（exit 0）；加上 -RequireLibreOffice 則視為失敗。
.PARAMETER Configuration
    建置組態，預設 Release。
.PARAMETER Framework
    測試目標框架，預設 net8.0。
.PARAMETER SofficePath
    可選的 soffice 路徑；未指定時沿用 ODFKIT_SOFFICE_PATH / LIBREOFFICE_PATH 與常見安裝路徑。
.PARAMETER RequireLibreOffice
    若找不到 LibreOffice 26.x 則以 exit 1 結束。
.PARAMETER NoBuild
    略過建置，直接執行測試。
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0",
    [string]$SofficePath = "",
    [switch]$RequireLibreOffice,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repoRoot "OdfKit.Tests/OdfKit.Tests.csproj"

function Resolve-SofficeExecutable {
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

    $names = @("soffice.com", "soffice.exe", "soffice")
    foreach ($name in $names) {
        $direct = Join-Path $Candidate $name
        if (Test-Path -LiteralPath $direct) {
            return (Resolve-Path -LiteralPath $direct).Path
        }

        $nested = Join-Path $Candidate "program/$name"
        if (Test-Path -LiteralPath $nested) {
            return (Resolve-Path -LiteralPath $nested).Path
        }
    }

    return $null
}

function Get-SofficeVersionText {
    param([string]$Executable)

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Executable
    $psi.ArgumentList.Add("--version")
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false

    $process = [System.Diagnostics.Process]::Start($psi)
    if (-not $process.WaitForExit(15000)) {
        throw "soffice --version 逾時：$Executable"
    }

    return ($process.StandardOutput.ReadToEnd() + $process.StandardError.ReadToEnd())
}

function Find-LibreOffice26Soffice {
    $candidates = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($SofficePath)) {
        [void]$candidates.Add($SofficePath)
    }

    foreach ($envName in @("ODFKIT_SOFFICE_PATH", "LIBREOFFICE_PATH")) {
        $value = [Environment]::GetEnvironmentVariable($envName)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            [void]$candidates.Add($value)
        }
    }

    if ($IsWindows) {
        [void]$candidates.Add("C:\Program Files\LibreOffice\program\soffice.com")
        [void]$candidates.Add("C:\Program Files\LibreOffice\program\soffice.exe")
        [void]$candidates.Add("C:\Program Files (x86)\LibreOffice\program\soffice.com")
        [void]$candidates.Add("C:\Program Files (x86)\LibreOffice\program\soffice.exe")
    }

    $pathCommand = Get-Command soffice -ErrorAction SilentlyContinue
    if ($null -ne $pathCommand) {
        [void]$candidates.Add($pathCommand.Source)
    }

    foreach ($candidate in $candidates) {
        $executable = Resolve-SofficeExecutable -Candidate $candidate
        if ([string]::IsNullOrWhiteSpace($executable)) {
            continue
        }

        if ($executable -match "MockSoffice") {
            continue
        }

        $versionText = Get-SofficeVersionText -Executable $executable
        if ($versionText -match "LibreOffice 26\.") {
            return [PSCustomObject]@{
                Path = $executable
                Version = ($versionText.Trim() -split "`n" | Select-Object -First 1)
            }
        }
    }

    return $null
}

Push-Location $repoRoot
try {
    $soffice = Find-LibreOffice26Soffice
    if ($null -eq $soffice) {
        $message = "找不到 LibreOffice 26.x soffice；互通性測試將略過。可設定 ODFKIT_SOFFICE_PATH 或 -SofficePath。"
        if ($RequireLibreOffice) {
            throw $message
        }

        Write-Host $message
        exit 0
    }

    Write-Host "使用 LibreOffice：$($soffice.Path)"
    Write-Host "版本：$($soffice.Version)"

    $env:ODFKIT_SOFFICE_PATH = $soffice.Path

    if (-not $NoBuild) {
        dotnet build $testProject -c $Configuration --framework $Framework
    }

    $testArgs = @(
        "test",
        $testProject,
        "-c", $Configuration,
        "--framework", $Framework,
        "--filter", "FullyQualifiedName~LibreOfficeInteropTests",
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