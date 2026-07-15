#Requires -Version 7.0
<#
.SYNOPSIS
驗證 Playwright Firefox 私密瀏覽捷徑清理範圍不會影響一般 Firefox。
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "odfkit-playwright-shortcut-$([Guid]::NewGuid().ToString('N'))"))
$systemTemporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $temporaryRoot.StartsWith($systemTemporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw '暫存測試目錄不在系統暫存目錄內。'
}

$browserRoot = Join-Path $temporaryRoot 'ms-playwright'
$playwrightFirefoxRoot = Join-Path $browserRoot 'firefox-1532/firefox'
$regularFirefoxRoot = Join-Path $temporaryRoot 'Mozilla Firefox'
$startMenuRoot = Join-Path $temporaryRoot 'Start Menu/Programs'
$shell = $null

try {
    New-Item -ItemType Directory -Path $playwrightFirefoxRoot, $regularFirefoxRoot, $startMenuRoot -Force | Out-Null
    $playwrightPrivateProxy = Join-Path $playwrightFirefoxRoot 'private_browsing.exe'
    $playwrightFirefox = Join-Path $playwrightFirefoxRoot 'firefox.exe'
    $regularPrivateProxy = Join-Path $regularFirefoxRoot 'private_browsing.exe'
    New-Item -ItemType File -Path $playwrightPrivateProxy, $playwrightFirefox, $regularPrivateProxy -Force | Out-Null

    $shell = New-Object -ComObject WScript.Shell
    $playwrightPrivateShortcut = Join-Path $startMenuRoot 'Nightly Private Browsing.lnk'
    $playwrightFirefoxShortcut = Join-Path $startMenuRoot 'Playwright Firefox.lnk'
    $regularPrivateShortcut = Join-Path $startMenuRoot 'Firefox Private Browsing.lnk'
    foreach ($definition in @(
            @($playwrightPrivateShortcut, $playwrightPrivateProxy),
            @($playwrightFirefoxShortcut, $playwrightFirefox),
            @($regularPrivateShortcut, $regularPrivateProxy))) {
        $shortcut = $shell.CreateShortcut($definition[0])
        $shortcut.TargetPath = $definition[1]
        $shortcut.Save()
    }
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    $shell = $null

    $removed = & (Join-Path $PSScriptRoot 'Remove-PlaywrightFirefoxPrivateBrowsingShortcut.ps1') `
        -BrowserRoot $browserRoot `
        -StartMenuRoot $startMenuRoot
    if ($removed -ne 1 -or (Test-Path -LiteralPath $playwrightPrivateShortcut)) {
        throw '未精確移除 Playwright Firefox 私密瀏覽捷徑。'
    }
    if (-not (Test-Path -LiteralPath $playwrightFirefoxShortcut) `
        -or -not (Test-Path -LiteralPath $regularPrivateShortcut) `
        -or -not (Test-Path -LiteralPath $playwrightPrivateProxy)) {
        throw '清理範圍影響了瀏覽器執行檔或非目標捷徑。'
    }

    Write-Host 'PASS：Playwright Firefox 私密瀏覽捷徑清理範圍正確。'
}
finally {
    if ($null -ne $shell) {
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    }
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
