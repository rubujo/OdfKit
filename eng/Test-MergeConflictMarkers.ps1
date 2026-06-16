#Requires -Version 7.0
<#
.SYNOPSIS
    掃描原始碼中的合併衝突標記（CS8300 成因）。
#>
param(
    [string]$Root = (Join-Path $PSScriptRoot '..')
)

$markerPatterns = @('^<<<<<<<', '^=======', '^>>>>>>>')
$issues = [System.Collections.Generic.List[object]]::new()

Get-ChildItem -Path $Root -Recurse -Filter '*.cs' -File |
    Where-Object {
        $_.FullName -notmatch '[\\/]bin[\\/]' -and
        $_.FullName -notmatch '[\\/]obj[\\/]' -and
        $_.FullName -notmatch '[\\/]Generated[\\/]'
    } |
    ForEach-Object {
        $lines = Get-Content -LiteralPath $_.FullName -Encoding UTF8
        for ($i = 0; $i -lt $lines.Count; $i++) {
            foreach ($pattern in $markerPatterns) {
                if ($lines[$i] -match $pattern) {
                    $relative = $_.FullName.Substring($Root.Length + 1)
                    $issues.Add([PSCustomObject]@{
                            File = $relative
                            Line = $i + 1
                            Text = $lines[$i].Trim()
                        })
                    break
                }
            }
        }
    }

if ($issues.Count -gt 0) {
    Write-Error "偵測到 $($issues.Count) 處合併衝突標記（可能為 IDE multi-target 合併失敗殘留）。"
    $issues | Format-Table -AutoSize
    exit 1
}

Write-Host 'OK：未發現合併衝突標記。'
exit 0