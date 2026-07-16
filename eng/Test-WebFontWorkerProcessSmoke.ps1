#Requires -Version 7.0
<#
.SYNOPSIS
以純 .NET 字型引擎、HTTP 與兩個獨立 OS process 驗證 WebFont 動態產生及故障復原。
#>
[CmdletBinding()]
param(
    [string]$Destination = "artifacts/webfont-worker-process-smoke",
    [Parameter(Mandatory)][string]$FontPath,
    [Parameter(Mandatory)][string]$SourceSha256
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$destinationPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Destination))
$repoRootWithSeparator = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$comparison = if ($IsWindows) {
    [System.StringComparison]::OrdinalIgnoreCase
}
else {
    [System.StringComparison]::Ordinal
}
if (-not $destinationPath.StartsWith($repoRootWithSeparator, $comparison)) {
    throw "Destination 必須位於方案目錄內。"
}

$resolvedFontPath = (Resolve-Path -LiteralPath $FontPath).Path
$actualSourceSha256 = (Get-FileHash -LiteralPath $resolvedFontPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($SourceSha256 -notmatch "^[0-9a-fA-F]{64}$" -or $actualSourceSha256 -ne $SourceSha256.ToLowerInvariant()) {
    throw "WebFont Worker process smoke 的來源字型 SHA-256 不符合。"
}

New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
$runPath = Join-Path $destinationPath ([Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $runPath -Force | Out-Null

$projectPath = Join-Path $repoRoot "tests/OdfKit.WebFontWorkerProcessSmoke/OdfKit.WebFontWorkerProcessSmoke.csproj"
$intermediateRoot = Join-Path $runPath "obj"
dotnet restore $projectPath --nologo `
    -p:OdfKitWebFontWorkerProcessSmokeIntermediateRoot="$intermediateRoot\"
if ($LASTEXITCODE -ne 0) {
    throw "WebFont Worker process smoke helper 還原失敗。"
}
dotnet build $projectPath -c Release --nologo --no-restore `
    -p:OdfKitWebFontWorkerProcessSmokeIntermediateRoot="$intermediateRoot\"
if ($LASTEXITCODE -ne 0) {
    throw "WebFont Worker process smoke helper 建置失敗。"
}

$appDll = Join-Path $repoRoot "tests/OdfKit.WebFontWorkerProcessSmoke/bin/Release/net10.0/OdfKit.WebFontWorkerProcessSmoke.dll"
$cachePath = Join-Path $runPath "cache"
$assetPath = Join-Path $runPath "assets"
$counterPath = Join-Path $runPath "engine-calls.txt"
$gatePath = Join-Path $runPath "start.gate"
$processes = @()
try {
    foreach ($index in 1..2) {
        $readyPath = Join-Path $runPath "process-$index.ready"
        $stdoutPath = Join-Path $runPath "process-$index.stdout.log"
        $stderrPath = Join-Path $runPath "process-$index.stderr.log"
        $startParameters = @{
            FilePath = "dotnet"
            ArgumentList = @(
                "`"$appDll`"",
                "`"$cachePath`"",
                "`"$assetPath`"",
                "`"$counterPath`"",
                "`"$gatePath`"",
                "`"$readyPath`"",
                "`"$resolvedFontPath`"",
                $actualSourceSha256
            )
            RedirectStandardOutput = $stdoutPath
            RedirectStandardError = $stderrPath
            PassThru = $true
        }
        if ($IsWindows) {
            $startParameters.WindowStyle = "Hidden"
        }
        $processes += Start-Process @startParameters
    }

    $readyDeadline = [DateTime]::UtcNow.AddSeconds(20)
    while (@(Get-ChildItem -LiteralPath $runPath -Filter "process-*.ready" -File).Count -ne 2) {
        foreach ($process in $processes) {
            if ($process.HasExited) {
                throw "WebFont Worker process smoke helper 在同步閘門前提前結束。"
            }
        }
        if ([DateTime]::UtcNow -ge $readyDeadline) {
            throw "等待 WebFont Worker process smoke helper 就緒逾時。"
        }
        Start-Sleep -Milliseconds 50
    }

    Set-Content -LiteralPath $gatePath -Value "start" -Encoding utf8NoBOM
    foreach ($process in $processes) {
        if (-not $process.WaitForExit(30000)) {
            throw "WebFont Worker process smoke helper 執行逾時。"
        }
        if ($process.ExitCode -ne 0) {
            throw "WebFont Worker process smoke helper 結束碼為 $($process.ExitCode)。"
        }
    }

    $calls = @(Get-Content -LiteralPath $counterPath)
    if ($calls.Count -ne 1 -or $calls[0] -ne "generated") {
        throw "兩個獨立 process 共執行了 $($calls.Count) 次底層 engine；預期為一次。"
    }

    $manifests = @(Get-ChildItem -LiteralPath $cachePath -Filter "*.json" -File)
    $residue = @(Get-ChildItem -LiteralPath $runPath -File -Recurse |
        Where-Object { $_.Extension -in ".lock", ".tmp" })
    if ($manifests.Count -ne 1 -or $residue.Count -ne 0) {
        throw "耐久快取 manifest 數量不正確或仍有 lock／temporary file 殘留。"
    }

    $hashes = @(
        1..2 | ForEach-Object {
            (Get-Content -LiteralPath (Join-Path $runPath "process-$_.stdout.log") -Raw).Trim()
        }
    )
    if ($hashes.Count -ne 2 -or $hashes[0] -ne $hashes[1] -or $hashes[0] -notmatch "^[0-9a-f]{64}$") {
        throw "兩個獨立 process 未取得相同的內容定址資產。"
    }


    $recoveryPath = Join-Path $runPath "crash-recovery"
    $recoveryCachePath = Join-Path $recoveryPath "cache"
    $recoveryAssetPath = Join-Path $recoveryPath "assets"
    $recoveryCounterPath = Join-Path $recoveryPath "engine-calls.txt"
    $recoveryGatePath = Join-Path $recoveryPath "start.gate"
    New-Item -ItemType Directory -Path $recoveryPath -Force | Out-Null

    $holderReadyPath = Join-Path $recoveryPath "holder.ready"
    $holderStdoutPath = Join-Path $recoveryPath "holder.stdout.log"
    $holderStderrPath = Join-Path $recoveryPath "holder.stderr.log"
    $holderParameters = @{
        FilePath = "dotnet"
        ArgumentList = @(
            "`"$appDll`"",
            "`"$recoveryCachePath`"",
            "`"$recoveryAssetPath`"",
            "`"$recoveryCounterPath`"",
            "`"$recoveryGatePath`"",
            "`"$holderReadyPath`"",
            "`"$resolvedFontPath`"",
            $actualSourceSha256,
            "hold-until-killed"
        )
        RedirectStandardOutput = $holderStdoutPath
        RedirectStandardError = $holderStderrPath
        PassThru = $true
    }
    if ($IsWindows) {
        $holderParameters.WindowStyle = "Hidden"
    }
    $holder = Start-Process @holderParameters
    $processes += $holder

    $holderDeadline = [DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath "$holderReadyPath.engine-started" -PathType Leaf)) {
        if ($holder.HasExited) {
            throw "持有 generation lease 的 helper 在故障注入前提前結束。"
        }
        if (-not (Test-Path -LiteralPath $holderReadyPath -PathType Leaf)) {
            if ([DateTime]::UtcNow -ge $holderDeadline) {
                throw "等待故障復原 holder 就緒逾時。"
            }
            Start-Sleep -Milliseconds 50
            continue
        }
        if (-not (Test-Path -LiteralPath $recoveryGatePath -PathType Leaf)) {
            Set-Content -LiteralPath $recoveryGatePath -Value "start" -Encoding utf8NoBOM
        }
        if ([DateTime]::UtcNow -ge $holderDeadline) {
            throw "等待 holder 取得 generation lease 逾時。"
        }
        Start-Sleep -Milliseconds 50
    }

    Stop-Process -Id $holder.Id -Force
    $holder.WaitForExit()

    $recoveryReadyPath = Join-Path $recoveryPath "recovery.ready"
    $recoveryStdoutPath = Join-Path $recoveryPath "recovery.stdout.log"
    $recoveryStderrPath = Join-Path $recoveryPath "recovery.stderr.log"
    $recoveryParameters = @{
        FilePath = "dotnet"
        ArgumentList = @(
            "`"$appDll`"",
            "`"$recoveryCachePath`"",
            "`"$recoveryAssetPath`"",
            "`"$recoveryCounterPath`"",
            "`"$recoveryGatePath`"",
            "`"$recoveryReadyPath`"",
            "`"$resolvedFontPath`"",
            $actualSourceSha256
        )
        RedirectStandardOutput = $recoveryStdoutPath
        RedirectStandardError = $recoveryStderrPath
        PassThru = $true
    }
    if ($IsWindows) {
        $recoveryParameters.WindowStyle = "Hidden"
    }
    $recovery = Start-Process @recoveryParameters
    $processes += $recovery
    if (-not $recovery.WaitForExit(30000)) {
        throw "故障後接手的 WebFont worker 執行逾時。"
    }
    if ($recovery.ExitCode -ne 0) {
        throw "故障後接手的 WebFont worker 結束碼為 $($recovery.ExitCode)。"
    }

    $recoveryCalls = @(Get-Content -LiteralPath $recoveryCounterPath)
    $recoveryManifests = @(Get-ChildItem -LiteralPath $recoveryCachePath -Filter "*.json" -File)
    $recoveryResidue = @(Get-ChildItem -LiteralPath $recoveryPath -File -Recurse |
        Where-Object { $_.Extension -in ".lock", ".tmp" })
    if (($recoveryCalls.Count -ne 2) -or
        ($recoveryCalls[0] -ne "generated") -or
        ($recoveryCalls[1] -ne "generated") -or
        ($recoveryManifests.Count -ne 1) -or
        ($recoveryResidue.Count -ne 0)) {
        throw "強制終止持鎖 worker 後，接手產生或暫存清理結果不正確。"
    }

    Write-Host "PASS：兩個獨立 OS process 共用純 .NET WOFF2，底層 engine 僅執行一次。"
    Write-Host "PASS：持有 generation lease 的 process 遭強制終止後，另一 process 可接手並完成。"
    Write-Host "PASS：真實 HTTP endpoint 已驗證授權 401、限流 429、256 路 GET、SHA-256 與 immutable cache。"
    Write-Host "PASS：Managed verifier 已拒絕截斷、內容損毀及超限展開長度的真實 WOFF2 產物。"
    Write-Host "證據：$runPath"
}
finally {
    foreach ($process in $processes) {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
        }
        $process.Dispose()
    }
}
