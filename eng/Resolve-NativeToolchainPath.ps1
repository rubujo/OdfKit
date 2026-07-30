#Requires -Version 7.0
<#
.SYNOPSIS
    解析 NativeAOT 原生連結所需的 Visual Studio Installer 目錄。
.DESCRIPTION
    ILCompiler 產生的連結命令以裸名稱呼叫 vswhere.exe 定位 MSVC linker，因此 Visual Studio
    Installer 目錄必須可由 PATH 解析。開發機常只安裝 Visual Studio 而未把該目錄加入 PATH，發布
    會在「Generating native code」之後以 MSB3073（返回碼 123）失敗，訊息只說
    「'vswhere.exe' 不是內部或外部命令」，不易判讀為環境問題。

    解析順序（任一來源可用即可）：
    1. -VisualStudioInstallerDirectory 明確指定的目錄（可攜式或非標準安裝位置）。
    2. 目前 PATH——CI 的 Windows runner 本來就能解析，此時輸出空字串代表無須調整。
    3. 已知安裝位置（%ProgramFiles(x86)% 與 %ProgramFiles% 下的
       Microsoft Visual Studio\Installer）。

    本腳本只做解析，不修改任何環境變數：PATH 由呼叫端在自己的 try／finally 內前置並還原，
    符合 eng\Test-EnvironmentVariableIsolation.ps1 要求的 process-scope 還原路徑。
.PARAMETER VisualStudioInstallerDirectory
    含 vswhere.exe 的 Visual Studio Installer 目錄。未指定時依序改用 PATH 與已知安裝位置。
.OUTPUTS
    System.String。需要前置到 PATH 的目錄；PATH 已可解析時為空字串。三種來源皆無時擲出例外。
#>
[CmdletBinding()]
param(
    [string]$VisualStudioInstallerDirectory
)

$ErrorActionPreference = 'Stop'

function Test-ContainsVsWhere {
    param([string]$Directory)

    return -not [string]::IsNullOrWhiteSpace($Directory) -and
        (Test-Path -LiteralPath (Join-Path $Directory 'vswhere.exe') -PathType Leaf)
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
