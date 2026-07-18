#Requires -Version 7.0
<#
.SYNOPSIS
以 SHA-256 鎖定的真實字型驗證 managed TTC、IVS、PUA 與明確格式拒絕。
#>
[CmdletBinding()]
param(
    [string]$Destination = "artifacts/webfont-format-matrix",
    [string]$CnsFontArchivePath,
    [string]$CnsKaiFontArchivePath
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$destinationPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $Destination))
$repoPrefix = [IO.Path]::GetFullPath($repoRoot).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if (-not $destinationPath.StartsWith($repoPrefix, $comparison)) {
    throw "Destination 必須位於方案目錄內。"
}

$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot "external-tools.json") -Raw | ConvertFrom-Json
$definitions = $manifest.webFontSmoke.internationalFonts
$sourceRoot = Join-Path $destinationPath "sources"
$outputRoot = Join-Path $destinationPath "evidence"
New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

function Invoke-LockedDownload {
    param(
        [Parameter(Mandatory)][uri]$Uri,
        [Parameter(Mandatory)][string]$DestinationPath,
        [Parameter(Mandatory)][string]$ExpectedSha256
    )

    if (Test-Path -LiteralPath $DestinationPath) {
        $existingHash = (Get-FileHash -LiteralPath $DestinationPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($existingHash -eq $ExpectedSha256) { return }
        throw "已存在的下載檔 SHA-256 不符合：$DestinationPath"
    }

    $temporaryPath = "$DestinationPath.download"
    try {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        $downloaded = $false
        for ($attempt = 1; $attempt -le 4; $attempt++) {
            try {
                Invoke-WebRequest -Uri $Uri -OutFile $temporaryPath `
                    -MaximumRetryCount 3 -RetryIntervalSec 2 -TimeoutSec 180
                $downloaded = $true
                break
            }
            catch {
                Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
                if ($attempt -eq 4) { throw }
                Write-Warning "下載失敗，將重試鎖定來源（第 $attempt 次）：$Uri"
                Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
            }
        }
        if (-not $downloaded) { throw "無法下載鎖定的 WebFont 測試來源：$Uri" }
        $actualHash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $ExpectedSha256) {
            throw "下載檔 SHA-256 不符合：$Uri"
        }
        Move-Item -LiteralPath $temporaryPath -Destination $DestinationPath
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

function Get-DirectFont {
    param([Parameter(Mandatory)]$Definition)

    $path = Join-Path $sourceRoot $Definition.fileName
    Invoke-LockedDownload -Uri $Definition.uri -DestinationPath $path -ExpectedSha256 $Definition.sha256
    return (Resolve-Path -LiteralPath $path).Path
}

function Get-ArchiveFont {
    param(
        [Parameter(Mandatory)]$Definition,
        [string]$ArchivePath
    )

    if ([string]::IsNullOrWhiteSpace($ArchivePath)) {
        $ArchivePath = Join-Path $sourceRoot $Definition.archiveFileName
    }
    else {
        $ArchivePath = [IO.Path]::GetFullPath((Join-Path $repoRoot $ArchivePath))
        if (-not $ArchivePath.StartsWith($repoPrefix, $comparison)) {
            throw "CnsFontArchivePath 必須位於方案目錄內。"
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $ArchivePath) -Force | Out-Null
    }
    Invoke-LockedDownload `
        -Uri $Definition.uri `
        -DestinationPath $ArchivePath `
        -ExpectedSha256 $Definition.archiveSha256
    $extractRoot = Join-Path $sourceRoot ([IO.Path]::GetFileNameWithoutExtension($Definition.archiveFileName))
    $font = Get-ChildItem -LiteralPath $extractRoot -Filter $Definition.fileName -File -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $font) {
        New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot -Force
        $font = Get-ChildItem -LiteralPath $extractRoot -Filter $Definition.fileName -File -Recurse |
            Select-Object -First 1
    }
    if ($null -eq $font) { throw "封存檔缺少字型：$($Definition.fileName)" }
    $actualHash = (Get-FileHash -LiteralPath $font.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $Definition.sha256) { throw "解壓字型 SHA-256 不符合：$($Definition.fileName)" }
    return $font.FullName
}

$extBPath = Get-ArchiveFont $definitions.cnsExtB -ArchivePath $CnsFontArchivePath
$plusPath = Get-ArchiveFont $definitions.cnsPlus -ArchivePath $CnsFontArchivePath
$kaiExtBPath = Get-ArchiveFont $definitions.cnsKaiExtB -ArchivePath $CnsKaiFontArchivePath
$kaiPlusPath = Get-ArchiveFont $definitions.cnsKaiPlus -ArchivePath $CnsKaiFontArchivePath
$ipamjPath = Get-ArchiveFont $definitions.ipamj
$collectionPath = Get-DirectFont $definitions.cjkCollection
$openTypePath = Get-DirectFont $definitions.cjkOpenType
$arabicPath = Get-DirectFont $definitions.arabic
$devanagariPath = Get-DirectFont $definitions.devanagari
$arabicStaticPath = Get-DirectFont $definitions.arabicStatic
$devanagariStaticPath = Get-DirectFont $definitions.devanagariStatic
$cff2Path = Get-DirectFont $definitions.cjkCff2
$colorEmojiPath = Get-DirectFont $definitions.colorEmoji
$colorEmojiColrV1Path = Get-DirectFont $definitions.colorEmojiColrV1

$projectPath = Join-Path $repoRoot "tests/OdfKit.WebFontFormatMatrix/OdfKit.WebFontFormatMatrix.csproj"
$intermediateRoot = Join-Path $destinationPath "obj"
dotnet restore $projectPath --nologo -p:NuGetAudit=false `
    -p:OdfKitWebFontFormatMatrixIntermediateRoot="$intermediateRoot\"
if ($LASTEXITCODE -ne 0) { throw "WebFont 格式矩陣還原失敗。" }
dotnet build $projectPath -c Release --nologo --no-restore `
    -p:OdfKitWebFontFormatMatrixIntermediateRoot="$intermediateRoot\"
if ($LASTEXITCODE -ne 0) { throw "WebFont 格式矩陣建置失敗。" }

$runnerPath = Join-Path $repoRoot `
    "tests/OdfKit.WebFontFormatMatrix/bin/Release/net10.0/OdfKit.WebFontFormatMatrix.dll"
$runnerArguments = @(
    $runnerPath,
    $outputRoot,
    $extBPath,
    $plusPath,
    $kaiExtBPath,
    $kaiPlusPath,
    $ipamjPath,
    $collectionPath,
    $openTypePath,
    $arabicStaticPath,
    $devanagariStaticPath,
    $arabicPath,
    $devanagariPath,
    $cff2Path,
    $colorEmojiPath,
    $colorEmojiColrV1Path)
$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = "dotnet"
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
foreach ($argument in $runnerArguments) { $startInfo.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::new()
$process.StartInfo = $startInfo
try {
    if (-not $process.Start()) { throw "WebFont 真實格式矩陣程序無法啟動。" }
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $stdoutTask.GetAwaiter().GetResult().Trim()
    $stderr = $stderrTask.GetAwaiter().GetResult().Trim()
    [IO.File]::WriteAllText((Join-Path $destinationPath "runner.stdout.log"), $stdout)
    [IO.File]::WriteAllText((Join-Path $destinationPath "runner.stderr.log"), $stderr)
    if (-not [string]::IsNullOrWhiteSpace($stdout)) { Write-Host $stdout }
    if (-not [string]::IsNullOrWhiteSpace($stderr)) { Write-Warning "格式矩陣 stderr：`n$stderr" }
    if ($process.ExitCode -ne 0) { throw "WebFont 真實格式矩陣失敗。" }
}
finally {
    $process.Dispose()
}

Write-Host "PASS：真實 TTF／TTC／OTC／WOFF／WOFF2／IVS／PUA／variable／CFF／CFF2、bitmap color 與 COLRv1 正向矩陣通過。"
Write-Host "證據：$(Join-Path $outputRoot 'format-matrix.json')"
