#Requires -Version 7.0
<#
.SYNOPSIS
以 SHA-256 鎖定的 W3C 官方 corpus 驗證純 Managed WOFF2 transformed tables 解碼。
#>
[CmdletBinding()]
param(
    [string]$Destination = "artifacts/webfont-woff2-corpus",
    [switch]$NoRestore
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

$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot "external-tools.json") -Raw |
    ConvertFrom-Json
$corpus = $manifest.webFontWoff2Corpus
$sourceRoot = Join-Path $destinationPath "sources"
$evidenceRoot = Join-Path $destinationPath "evidence"
New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null

function Get-LockedCorpusFile {
    param([Parameter(Mandatory)]$Definition)

    $path = Join-Path $sourceRoot $Definition.fileName
    if (Test-Path -LiteralPath $path) {
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($hash -eq $Definition.sha256) { return $path }
        throw "已存在的 W3C corpus SHA-256 不符合：$path"
    }

    $temporaryPath = "$path.download"
    try {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        try {
            Invoke-WebRequest -Uri $Definition.uri -OutFile $temporaryPath `
                -MaximumRetryCount 3 -RetryIntervalSec 2 -TimeoutSec 180
        }
        catch {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
            $gh = Get-Command gh -CommandType Application -ErrorAction SilentlyContinue |
                Select-Object -First 1
            if ($null -ne $gh -and -not [string]::IsNullOrWhiteSpace($Definition.apiPath)) {
                $response = & $gh.Source api $Definition.apiPath | ConvertFrom-Json
                if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($response.content)) { throw }
                [IO.File]::WriteAllBytes(
                    $temporaryPath,
                    [Convert]::FromBase64String(($response.content -replace '\s', '')))
            }
            else {
                $curl = Get-Command curl -CommandType Application -ErrorAction SilentlyContinue |
                    Select-Object -First 1
                if ($null -eq $curl) { throw }
                & $curl.Source --fail --location --retry 3 --retry-all-errors `
                    --output $temporaryPath $Definition.uri
                if ($LASTEXITCODE -ne 0) { throw }
            }
        }
        $hash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($hash -ne $Definition.sha256) {
            throw "下載的 W3C corpus SHA-256 不符合：$($Definition.uri)"
        }
        Move-Item -LiteralPath $temporaryPath -Destination $path
        return $path
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

$woff2Path = Get-LockedCorpusFile $corpus.woff2
$referencePath = Get-LockedCorpusFile $corpus.reference
$projectPath = Join-Path $repoRoot "tests/OdfKit.WebFontFormatMatrix/OdfKit.WebFontFormatMatrix.csproj"
$buildArguments = @("build", $projectPath, "-c", "Release", "--nologo", "-p:NuGetAudit=false")
if ($NoRestore) { $buildArguments += "--no-restore" }
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw "W3C WOFF2 corpus runner 建置失敗。" }

$runnerPath = Join-Path $repoRoot `
    "tests/OdfKit.WebFontFormatMatrix/bin/Release/net10.0/OdfKit.WebFontFormatMatrix.dll"
$evidencePath = Join-Path $evidenceRoot "woff2-transforms.json"
& dotnet $runnerPath woff2-corpus $woff2Path $referencePath $evidencePath
if ($LASTEXITCODE -ne 0) { throw "W3C WOFF2 transformed tables corpus 驗證失敗。" }

$evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
$evidence | Add-Member -NotePropertyName corpusRevision -NotePropertyValue $corpus.revision
$evidence | Add-Member -NotePropertyName corpusLicense -NotePropertyValue $corpus.license
$evidence | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $evidencePath -Encoding utf8NoBOM
foreach ($definition in $corpus.production) {
    $productionPath = Get-LockedCorpusFile $definition
    $productionEvidence = Join-Path $evidenceRoot "$($definition.id).json"
    & dotnet $runnerPath woff2-production $productionPath $definition.text $productionEvidence
    if ($LASTEXITCODE -ne 0) {
        throw "Production WOFF2 corpus 驗證失敗：$($definition.id)"
    }
    $result = Get-Content -LiteralPath $productionEvidence -Raw | ConvertFrom-Json
    $result | Add-Member -NotePropertyName version -NotePropertyValue $definition.version
    $result | Add-Member -NotePropertyName license -NotePropertyValue $definition.license
    $result | ConvertTo-Json -Depth 10 |
        Set-Content -LiteralPath $productionEvidence -Encoding utf8NoBOM
}
Write-Host "W3C WOFF2 transformed tables corpus 驗證通過：$evidencePath"
