#Requires -Version 7.0
<#
.SYNOPSIS
    檢查指令碼與測試程式的環境變數是否具備失敗安全的還原路徑。
.PARAMETER Root
    Repository 根目錄；預設為 eng 的上一層。
#>
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$issues = New-Object System.Collections.Generic.List[string]
$selfPath = (Resolve-Path -LiteralPath $PSCommandPath).Path

foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File -Include '*.ps1', '*.psm1') {
    if ($file.FullName -eq $selfPath -or $file.FullName -like "*\artifacts\*") {
        continue
    }

    $content = [System.IO.File]::ReadAllText($file.FullName)
    $assignments = [regex]::Matches($content, '\$env:([A-Za-z_][A-Za-z0-9_]*)\s*=')
    foreach ($variable in @($assignments | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)) {
        $escaped = [regex]::Escape($variable)
        $restorePattern = ('\$env:{0}\s*=\s*\$(previous|original)[A-Za-z0-9_]*|Remove-Item\s+(?:-LiteralPath\s+)?Env:{0}' -f $escaped)
        if ($content -notmatch $restorePattern -or $content -notmatch '\bfinally\s*\{') {
            $relative = [IO.Path]::GetRelativePath($Root, $file.FullName)
            [void]$issues.Add("$relative：環境變數 $variable 缺少 finally 還原路徑。")
        }
    }
}

$testsRoot = Join-Path $Root 'OdfKit.Tests'
foreach ($file in Get-ChildItem -LiteralPath $testsRoot -Recurse -File -Filter '*.cs') {
    $content = [System.IO.File]::ReadAllText($file.FullName)
    if ($content -match 'Environment\.SetEnvironmentVariable\([^\r\n]*EnvironmentVariableTarget\.(User|Machine)') {
        $relative = [IO.Path]::GetRelativePath($Root, $file.FullName)
        [void]$issues.Add("$relative：測試不得寫入 User 或 Machine scope 環境變數。")
    }

    $setCalls = [regex]::Matches(
        $content,
        'Environment\.SetEnvironmentVariable\(\s*([^,\r\n]+),')
    if ($setCalls.Count -gt 0 -and $content -notmatch '\[Collection\("SequentialRenderingTests"\)\]') {
        $relative = [IO.Path]::GetRelativePath($Root, $file.FullName)
        [void]$issues.Add("$relative：修改行程環境變數的測試必須加入不可平行化集合。")
    }

    foreach ($argument in @($setCalls | ForEach-Object { $_.Groups[1].Value.Trim() } | Sort-Object -Unique)) {
        $count = @($setCalls | Where-Object { $_.Groups[1].Value.Trim() -eq $argument }).Count
        if ($count -lt 2 -or $content -notmatch '\bfinally\s*\{') {
            $relative = [IO.Path]::GetRelativePath($Root, $file.FullName)
            [void]$issues.Add("$relative：SetEnvironmentVariable($argument, ...) 缺少成對 finally 還原。")
        }
    }
}

if ($issues.Count -gt 0) {
    foreach ($issue in $issues) {
        Write-Error $issue
    }
    exit 1
}

Write-Host 'PASS：指令碼與測試程式的環境變數皆具備 process-scope finally 還原路徑。'
