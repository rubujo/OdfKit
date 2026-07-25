#Requires -Version 7.0
<#
.SYNOPSIS
    安裝、更新、查詢或解除安裝 OdfKit WebFont Sidecar Windows Service。
.PARAMETER Action
    要執行的服務管理動作。
.PARAMETER ServiceName
    Windows Service 內部名稱。
.PARAMETER DisplayName
    顯示於服務管理員的名稱。
.PARAMETER HostExecutablePath
    同版本 NativeAOT Sidecar Host 的絕對路徑。
.PARAMETER PipeName
    Sidecar 具名 pipe 名稱。
.PARAMETER AssetRootPath
    Sidecar 與 System.Web 共用的內容定址資產根目錄。
.PARAMETER CacheRootPath
    Sidecar durable cache 目錄。
.PARAMETER TokenFilePath
    只含 Sidecar token 的 ACL 保護檔案。不存在時，Install 會產生 48-byte 隨機 token。
.PARAMETER FontSource
    一個或多個 id=絕對字型路徑。
.PARAMETER ServiceAccount
    服務帳號。預設使用 NT SERVICE\<ServiceName> 虛擬帳號。
.PARAMETER IisAppPoolName
    需要讀取 token 與資產的 IIS application pool 名稱。
.PARAMETER StartService
    安裝或更新完成後啟動服務。
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "Medium")]
param(
    [Parameter(Mandatory)]
    [ValidateSet("Install", "Update", "Status", "Uninstall")]
    [string]$Action,
    [ValidatePattern("^[A-Za-z0-9_.-]{1,80}$")]
    [string]$ServiceName = "OdfKitWebFontsSidecar",
    [string]$DisplayName = "OdfKit WebFonts Sidecar",
    [string]$HostExecutablePath,
    [ValidatePattern("^[^/\\]{1,128}$")]
    [string]$PipeName = "odfkit-webfonts-production",
    [string]$AssetRootPath,
    [string]$CacheRootPath,
    [string]$TokenFilePath,
    [string[]]$FontSource = @(),
    [string]$ServiceAccount,
    [string]$IisAppPoolName,
    [switch]$StartService
)

$ErrorActionPreference = "Stop"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Windows Service 管理必須在系統管理員 PowerShell 中執行。"
    }
}

function Invoke-ServiceControl {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & "$env:SystemRoot\System32\sc.exe" @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "sc.exe 執行失敗，結束碼 $LASTEXITCODE。"
    }
}

function Test-ServiceExists {
    & "$env:SystemRoot\System32\sc.exe" query $ServiceName *> $null
    return $LASTEXITCODE -eq 0
}

function Resolve-RequiredFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathFullyQualified($Path)) {
        throw "$Description 必須是絕對路徑。"
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "找不到$Description：$Path"
    }
    return (Resolve-Path -LiteralPath $Path).Path
}

function Resolve-DirectoryPath {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathFullyQualified($Path)) {
        throw "$Description 必須是絕對路徑。"
    }
    return [IO.Path]::GetFullPath($Path)
}

function Quote-ServiceArgument {
    param([Parameter(Mandatory)][string]$Value)

    if ($Value.Contains('"') -or $Value.Contains("`r") -or $Value.Contains("`n")) {
        throw "服務命令列參數包含不允許的字元。"
    }
    return '"' + $Value + '"'
}

function Grant-FileAccess {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Identity,
        [Parameter(Mandatory)][ValidateSet("R", "RX", "M")][string]$Permission,
        [switch]$Container
    )

    $grant = if ($Container) {
        "$Identity`:(OI)(CI)$Permission"
    }
    else {
        "$Identity`:$Permission"
    }
    & "$env:SystemRoot\System32\icacls.exe" $Path "/grant:r" $grant *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "無法設定 ACL：$Path"
    }
}

function Wait-ServiceState {
    param(
        [Parameter(Mandatory)][ValidateSet("Running", "Stopped")][string]$State,
        [int]$TimeoutSeconds = 30
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $service = Get-Service -Name $ServiceName -ErrorAction Stop
        if ($service.Status.ToString() -eq $State) {
            return
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "服務 $ServiceName 未在 $TimeoutSeconds 秒內進入 $State 狀態。"
}

function Stop-SidecarService {
    if ((Get-Service -Name $ServiceName -ErrorAction Stop).Status -ne "Stopped") {
        Invoke-ServiceControl @("stop", $ServiceName)
        Wait-ServiceState -State Stopped
    }
}

function Get-ServiceDefinition {
    $resolvedHost = Resolve-RequiredFile -Path $HostExecutablePath -Description " Sidecar Host"
    $resolvedAssetRoot = Resolve-DirectoryPath -Path $AssetRootPath -Description "資產根目錄"
    $resolvedCacheRoot = Resolve-DirectoryPath -Path $CacheRootPath -Description "快取根目錄"
    $resolvedTokenFile = [IO.Path]::GetFullPath($TokenFilePath)
    if (-not [IO.Path]::IsPathFullyQualified($TokenFilePath)) {
        throw "Token 檔案必須是絕對路徑。"
    }
    if ($FontSource.Count -eq 0) {
        throw "至少必須指定一個 FontSource。"
    }

    $resolvedFontSources = foreach ($entry in $FontSource) {
        $separator = $entry.IndexOf("=")
        if ($separator -le 0 -or $separator -eq $entry.Length - 1) {
            throw "FontSource 必須使用 id=絕對字型路徑格式。"
        }
        $id = $entry.Substring(0, $separator)
        if ($id -notmatch "^[A-Za-z0-9_.-]+$") {
            throw "FontSource id 包含不允許的字元：$id"
        }
        $path = Resolve-RequiredFile -Path $entry.Substring($separator + 1) -Description "字型"
        "$id=$path"
    }

    New-Item -ItemType Directory -Path $resolvedAssetRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $resolvedCacheRoot -Force | Out-Null
    $tokenDirectory = Split-Path -Parent $resolvedTokenFile
    New-Item -ItemType Directory -Path $tokenDirectory -Force | Out-Null
    if (-not (Test-Path -LiteralPath $resolvedTokenFile -PathType Leaf)) {
        $token = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
        Set-Content -LiteralPath $resolvedTokenFile -Value $token -Encoding utf8NoBOM -NoNewline
    }
    $tokenValue = (Get-Content -LiteralPath $resolvedTokenFile -Raw).Trim()
    $tokenByteCount = [Text.Encoding]::UTF8.GetByteCount($tokenValue)
    if ($tokenByteCount -lt 32 -or $tokenByteCount -gt 512) {
        throw "Sidecar token 必須介於 32 與 512 UTF-8 bytes。"
    }

    $identity = if ([string]::IsNullOrWhiteSpace($ServiceAccount)) {
        "NT SERVICE\$ServiceName"
    }
    else {
        $ServiceAccount
    }
    $passwordlessAccounts = @(
        "LocalSystem",
        "NT AUTHORITY\LocalService",
        "NT AUTHORITY\NetworkService"
    )
    if ($identity -notlike "NT SERVICE\*" `
        -and $identity -notin $passwordlessAccounts `
        -and -not $identity.EndsWith('$', [StringComparison]::Ordinal)) {
        throw "ServiceAccount 僅支援 Windows 內建帳號、服務虛擬帳號或 gMSA。"
    }

    $arguments = @(
        "--service-name", $ServiceName,
        "--pipe", $PipeName,
        "--asset-root", $resolvedAssetRoot,
        "--cache-root", $resolvedCacheRoot,
        "--token-file", $resolvedTokenFile,
        "--allow-cross-user"
    )
    foreach ($entry in $resolvedFontSources) {
        $arguments += @("--font-source", $entry)
    }
    $quotedArguments = $arguments | ForEach-Object { Quote-ServiceArgument $_ }
    $binaryPath = (Quote-ServiceArgument $resolvedHost) + " " + ($quotedArguments -join " ")

    return @{
        Account = $identity
        BinaryPath = $binaryPath
        HostDirectory = Split-Path -Parent $resolvedHost
        FontPaths = @($resolvedFontSources | ForEach-Object {
            $_.Substring($_.IndexOf("=") + 1)
        })
        AssetRoot = $resolvedAssetRoot
        CacheRoot = $resolvedCacheRoot
        TokenDirectory = $tokenDirectory
        TokenFile = $resolvedTokenFile
    }
}

function Set-ServicePermissions {
    param([Parameter(Mandatory)][hashtable]$Definition)

    Grant-FileAccess -Path $Definition.HostDirectory -Identity $Definition.Account -Permission RX -Container
    foreach ($path in $Definition.FontPaths) {
        Grant-FileAccess -Path $path -Identity $Definition.Account -Permission R
    }
    Grant-FileAccess -Path $Definition.AssetRoot -Identity $Definition.Account -Permission M -Container
    Grant-FileAccess -Path $Definition.CacheRoot -Identity $Definition.Account -Permission M -Container
    Grant-FileAccess -Path $Definition.TokenDirectory -Identity $Definition.Account -Permission RX -Container

    & "$env:SystemRoot\System32\icacls.exe" $Definition.TokenFile "/inheritance:r" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "無法移除 token 檔案的繼承 ACL。"
    }
    Grant-FileAccess -Path $Definition.TokenFile -Identity "BUILTIN\Administrators" -Permission R
    Grant-FileAccess -Path $Definition.TokenFile -Identity "NT AUTHORITY\SYSTEM" -Permission R
    Grant-FileAccess -Path $Definition.TokenFile -Identity $Definition.Account -Permission R

    if (-not [string]::IsNullOrWhiteSpace($IisAppPoolName)) {
        $appPoolIdentity = "IIS AppPool\$IisAppPoolName"
        Grant-FileAccess -Path $Definition.TokenDirectory -Identity $appPoolIdentity -Permission RX -Container
        Grant-FileAccess -Path $Definition.TokenFile -Identity $appPoolIdentity -Permission R
        Grant-FileAccess -Path $Definition.AssetRoot -Identity $appPoolIdentity -Permission RX -Container
    }
}

if (-not $WhatIfPreference) {
    Assert-Administrator
}
$exists = Test-ServiceExists

switch ($Action) {
    "Status" {
        if (-not $exists) {
            Write-Host "服務 $ServiceName 尚未安裝。"
            return
        }
        Invoke-ServiceControl @("qc", $ServiceName)
        Invoke-ServiceControl @("qfailure", $ServiceName)
        Invoke-ServiceControl @("queryex", $ServiceName)
        return
    }
    "Uninstall" {
        if (-not $exists) {
            Write-Host "服務 $ServiceName 尚未安裝。"
            return
        }
        if ($PSCmdlet.ShouldProcess($ServiceName, "停止並解除安裝 Windows Service")) {
            Stop-SidecarService
            Invoke-ServiceControl @("delete", $ServiceName)
            if ([Diagnostics.EventLog]::SourceExists($ServiceName)) {
                Remove-EventLog -Source $ServiceName
            }
        }
        return
    }
    "Install" {
        if ($exists) {
            throw "服務 $ServiceName 已存在；請使用 Action=Update。"
        }
    }
    "Update" {
        if (-not $exists) {
            throw "服務 $ServiceName 不存在；請使用 Action=Install。"
        }
    }
}

if ($WhatIfPreference) {
    Write-Host "WhatIf：將以服務帳號、ACL、secret 檔案及復原設定執行 $Action：$ServiceName"
    return
}

if (-not $PSCmdlet.ShouldProcess($ServiceName, "$Action Windows Service")) {
    return
}
$definition = Get-ServiceDefinition

$createdByInstall = $false
try {
    if ($Action -eq "Install") {
        Invoke-ServiceControl @(
            "create", $ServiceName,
            "binPath=", $definition.BinaryPath,
            "start=", "delayed-auto",
            "DisplayName=", $DisplayName)
        $createdByInstall = $true
        Invoke-ServiceControl @(
            "config", $ServiceName,
            "obj=", $definition.Account)
    }
    else {
        Stop-SidecarService
        Invoke-ServiceControl @(
            "config", $ServiceName,
            "binPath=", $definition.BinaryPath,
            "start=", "delayed-auto",
            "obj=", $definition.Account,
            "DisplayName=", $DisplayName)
    }

    Invoke-ServiceControl @(
        "description", $ServiceName,
        "OdfKit WebFonts request-time WOFF2 NativeAOT sidecar.")
    Invoke-ServiceControl @("sidtype", $ServiceName, "unrestricted")
    Set-ServicePermissions -Definition $definition
    if (-not [Diagnostics.EventLog]::SourceExists($ServiceName)) {
        New-EventLog -LogName Application -Source $ServiceName
    }
    Invoke-ServiceControl @(
        "failure", $ServiceName,
        "reset=", "900",
        "actions=", "restart/120000/restart/300000")
    Invoke-ServiceControl @("failureflag", $ServiceName, "1")

    if ($StartService) {
        Invoke-ServiceControl @("start", $ServiceName)
        Wait-ServiceState -State Running
    }

    Write-Host "Windows Service $ServiceName 已完成 $Action。"
}
catch {
    if ($createdByInstall) {
        & "$env:SystemRoot\System32\sc.exe" stop $ServiceName *> $null
        & "$env:SystemRoot\System32\sc.exe" delete $ServiceName *> $null
        if ([Diagnostics.EventLog]::SourceExists($ServiceName)) {
            Remove-EventLog -Source $ServiceName
        }
    }
    throw
}
