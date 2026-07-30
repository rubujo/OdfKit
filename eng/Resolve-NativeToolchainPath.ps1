#Requires -Version 7.0
<#
.SYNOPSIS
    解析 NativeAOT 原生連結所需的 Visual Studio Installer 目錄。
.DESCRIPTION
    ILCompiler 產生的連結命令以裸名稱呼叫 vswhere.exe 定位 MSVC linker，因此 Visual Studio
    Installer 目錄必須可由 PATH 解析。開發機常只安裝 Visual Studio 而未把該目錄加入 PATH，發布
    會在「Generating native code」之後以 MSB3073（返回碼 123）失敗，訊息只說
    「'vswhere.exe' 不是內部或外部命令」，不易判讀為環境問題。

    是否需要這個工具由「目標 RID」決定，不是由主機平台決定：只有 win-* 目標以 MSVC 連結，
    linux-* 與 osx-* 目標的 NativeAOT 以 clang／ld 連結，不存在 vswhere.exe。trim smoke 是
    win-x64、linux-x64 與 osx-arm64 三平台矩陣，非 Windows 目標必須直接放行。
    以 RID 而非 $IsWindows 判斷的另一個理由是可測性：RID 是普通字串參數，兩條分支都能在任一
    平台上驗證，$IsWindows 是唯讀常數而無法在測試中改寫。

    win-* 目標的解析順序（任一來源可用即可）：
    1. -VisualStudioInstallerDirectory 明確指定的目錄（可攜式或非標準安裝位置）。
    2. 目前 PATH——CI 的 Windows runner 本來就能解析，此時輸出空字串代表無須調整。
    3. 已知安裝位置（%ProgramFiles(x86)% 與 %ProgramFiles% 下的
       Microsoft Visual Studio\Installer）。

    本腳本只做解析，不修改任何環境變數：PATH 由呼叫端在自己的 try／finally 內前置並還原，
    符合 eng\Test-EnvironmentVariableIsolation.ps1 要求的 process-scope 還原路徑。
.PARAMETER RuntimeIdentifier
    發布目標 RID。只有 win-* 需要 MSVC 連結器，其餘一律直接放行。
.PARAMETER VisualStudioInstallerDirectory
    含 vswhere.exe 的 Visual Studio Installer 目錄；僅 win-* 目標適用。未指定時依序改用 PATH
    與已知安裝位置。
.OUTPUTS
    System.String。需要前置到 PATH 的目錄；非 win-* 目標或 PATH 已可解析時為空字串。
    win-* 目標三種來源皆無時擲出例外。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RuntimeIdentifier,
    [string]$VisualStudioInstallerDirectory
)

$ErrorActionPreference = 'Stop'

function Test-ContainsVsWhere {
    param([string]$Directory)

    return -not [string]::IsNullOrWhiteSpace($Directory) -and
        (Test-Path -LiteralPath (Join-Path $Directory 'vswhere.exe') -PathType Leaf)
}

# 0. 只有 win-* 目標以 MSVC 連結；linux-* 與 osx-* 目標的 NativeAOT 用 clang／ld，
#    不存在 vswhere.exe，必須直接放行，否則會擋死 trim smoke 的非 Windows 矩陣。
if (-not $RuntimeIdentifier.StartsWith('win', [System.StringComparison]::OrdinalIgnoreCase)) {
    if (-not [string]::IsNullOrWhiteSpace($VisualStudioInstallerDirectory)) {
        Write-Host "目標 $RuntimeIdentifier 不使用 vswhere.exe，已忽略 -VisualStudioInstallerDirectory。"
    }

    return ''
}

# 1. 明確指定優先；指定了卻不含 vswhere.exe 屬於呼叫端錯誤，不靜默改用其他來源。
if (-not [string]::IsNullOrWhiteSpace($VisualStudioInstallerDirectory)) {
    if (-not (Test-ContainsVsWhere -Directory $VisualStudioInstallerDirectory)) {
        throw "指定的 Visual Studio Installer 目錄不含 vswhere.exe：$VisualStudioInstallerDirectory"
    }

    return (Resolve-Path -LiteralPath $VisualStudioInstallerDirectory).Path
}

# 2. PATH 已可解析時無須調整。
if (Get-Command 'vswhere.exe' -ErrorAction Ignore) {
    return ''
}

# 3. 已知安裝位置。整段管線必須以 @() 包住：只有單一結果存活時，PowerShell 會把管線輸出
#    塌回純字串，$candidates[0] 就會取到字串的第一個字元而不是目錄。
$candidates = @(
    @(
        ${env:ProgramFiles(x86)}
        $env:ProgramFiles
    ) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { Join-Path $_ 'Microsoft Visual Studio\Installer' } |
        Where-Object { Test-ContainsVsWhere -Directory $_ }
)

if ($candidates.Count -eq 0) {
    throw @(
        '找不到 vswhere.exe。NativeAOT 原生連結需要 Visual Studio Installer 目錄可由 PATH 解析，'
        '請安裝 Visual Studio（含 C++ 建置工具）、改以 Developer PowerShell 執行，'
        '或以 -VisualStudioInstallerDirectory 指定該目錄。'
    ) -join ''
}

return $candidates[0]
