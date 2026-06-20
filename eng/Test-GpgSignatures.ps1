#Requires -Version 7.0
<#
.SYNOPSIS
    驗證此 repo 所有提交皆為有效 GPG 簽署，且僅使用 repo 專屬金鑰。
#>
param(
    [string]$Root = (Join-Path $PSScriptRoot '..'),
    [string]$ExpectedKeyId = 'E51AD4BE0F11D52A'
)

$repoRoot = (Resolve-Path -LiteralPath $Root).Path
$localKey = git -C $repoRoot config --local --get user.signingkey 2>$null
if ([string]::IsNullOrWhiteSpace($localKey)) {
    Write-Error "repo 本機未設定 user.signingkey，無法強制使用專屬金鑰。"
    exit 1
}

if ($localKey -ne $ExpectedKeyId) {
    Write-Error "repo 本機 user.signingkey 為 $localKey，預期為 $ExpectedKeyId。"
    exit 1
}

$issues = [System.Collections.Generic.List[object]]::new()
$total = 0
$auditFile = Join-Path ([System.IO.Path]::GetTempPath()) "odfkit-gpg-audit-$([Guid]::NewGuid().ToString('N')).tmp"
try {
    git -C $repoRoot -c core.pager=cat log --format='%H###%G?###%GK###%s' > $auditFile
    Get-Content -LiteralPath $auditFile -Encoding UTF8 | ForEach-Object {
    if ([string]::IsNullOrWhiteSpace($_)) {
        return
    }

    $total++
    $parts = $_ -split '###', 4
    if ($parts.Count -lt 4) {
        $issues.Add([PSCustomObject]@{
                Hash = $parts[0].Substring(0, 7)
                Issue = '簽署紀錄格式無法解析'
                KeyId = ''
                Subject = $_
            })
        return
    }

    $hash = $parts[0]
    $status = $parts[1]
    $keyId = $parts[2]
    $subject = $parts[3]

    if ($status -ne 'G') {
        $issues.Add([PSCustomObject]@{
                Hash = $hash.Substring(0, 7)
                Issue = "簽署狀態為 $status（預期 G）"
                KeyId = $keyId
                Subject = $subject
            })
        return
    }

    if ($keyId -ne $ExpectedKeyId) {
        $issues.Add([PSCustomObject]@{
                Hash = $hash.Substring(0, 7)
                Issue = '使用了非 repo 專屬金鑰'
                KeyId = $keyId
                Subject = $subject
            })
    }
    }
}
finally {
    if (Test-Path -LiteralPath $auditFile) {
        Remove-Item -LiteralPath $auditFile -Force
    }
}

if ($issues.Count -gt 0) {
    Write-Error "偵測到 $($issues.Count) 筆提交不符合 GPG 簽署政策（僅允許 $ExpectedKeyId）。"
    $issues | Format-Table -AutoSize
    exit 1
}

Write-Host "OK：$total 筆提交皆為 Good signature，且金鑰皆為 $ExpectedKeyId。"
exit 0