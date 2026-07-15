#Requires -Version 7.0
<#
.SYNOPSIS
移除指向 Playwright Firefox 私密瀏覽代理程式的目前使用者開始功能表捷徑。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BrowserRoot,

    [string]$StartMenuRoot = [Environment]::GetFolderPath('StartMenu')
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

try {
    Get-ChildItem -LiteralPath $resolvedStartMenuRoot -Filter '*.lnk' -File -Recurse -ErrorAction SilentlyContinue |
        ForEach-Object {
            try {
                $shortcut = $shell.CreateShortcut($_.FullName)
                if ([string]::IsNullOrWhiteSpace($shortcut.TargetPath)) {
                    return
                }
                $targetPath = [IO.Path]::GetFullPath($shortcut.TargetPath)
            }
            catch {
                Write-Verbose "略過無法解析的捷徑：$($_.FullName)"
                return
            }
            if (-not $targetPath.StartsWith($resolvedBrowserRoot, [StringComparison]::OrdinalIgnoreCase)) {
                return
            }

            $relativeTarget = $targetPath.Substring($resolvedBrowserRoot.Length)
            $segments = $relativeTarget -split '[\\/]'
            $isPlaywrightPrivateBrowsingProxy = $segments.Count -ge 3 `
                -and $segments[0] -match '^firefox-\d+$' `
                -and $segments[1] -eq 'firefox' `
                -and $segments[-1] -eq 'private_browsing.exe'
            if (-not $isPlaywrightPrivateBrowsingProxy) {
                return
            }

            Remove-Item -LiteralPath $_.FullName -Force
            $removed++
        }
}
finally {
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
}

return $removed
