#Requires -Version 7.0
<#
.SYNOPSIS
移除 Playwright Firefox 私密瀏覽代理程式與目前使用者開始功能表捷徑。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BrowserRoot,

    [string]$StartMenuRoot = [Environment]::GetFolderPath('StartMenu'),

    [switch]$RemoveProxyAssets
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows -or -not (Test-Path -LiteralPath $StartMenuRoot)) {
    return 0
}

$resolvedBrowserRoot = [IO.Path]::GetFullPath($BrowserRoot).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$resolvedStartMenuRoot = [IO.Path]::GetFullPath($StartMenuRoot)
$shell = New-Object -ComObject WScript.Shell
$removed = 0

function Test-PlaywrightPrivateBrowsingPath {
    param([AllowEmptyString()][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    $candidate = $Path.Trim().Trim('"') -replace ',\d+$', ''
    return $candidate -match '(?i)[\\/]ms-playwright[\\/]firefox-\d+[\\/]firefox[\\/]private_browsing\.exe$'
}

try {
    Get-ChildItem -LiteralPath $resolvedStartMenuRoot -Filter '*.lnk' -File -Recurse -ErrorAction SilentlyContinue |
        ForEach-Object {
            try {
                $shortcut = $shell.CreateShortcut($_.FullName)
                $targetMatches = Test-PlaywrightPrivateBrowsingPath -Path $shortcut.TargetPath
                $iconMatches = Test-PlaywrightPrivateBrowsingPath -Path $shortcut.IconLocation
            }
            catch {
                Write-Verbose "略過無法解析的捷徑：$($_.FullName)"
                return
            }
            if (-not $targetMatches -and -not $iconMatches) {
                return
            }

            Remove-Item -LiteralPath $_.FullName -Force
            $removed++
        }
}
finally {
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
}

if ($RemoveProxyAssets) {
    Get-ChildItem -LiteralPath $resolvedBrowserRoot -Directory -Filter 'firefox-*' -ErrorAction SilentlyContinue |
        Where-Object Name -Match '^firefox-\d+$' |
        ForEach-Object {
            $firefoxRoot = Join-Path $_.FullName 'firefox'
            foreach ($assetName in @('private_browsing.exe', 'private_browsing.VisualElementsManifest.xml')) {
                $assetPath = [IO.Path]::GetFullPath((Join-Path $firefoxRoot $assetName))
                if (-not $assetPath.StartsWith($resolvedBrowserRoot, [StringComparison]::OrdinalIgnoreCase)) {
                    throw 'Playwright Firefox 私密瀏覽資產不在瀏覽器根目錄內。'
                }
                Remove-Item -LiteralPath $assetPath -Force -ErrorAction SilentlyContinue
            }
        }
}

return $removed
