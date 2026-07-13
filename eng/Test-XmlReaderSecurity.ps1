#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sourceRoots = @(
    (Join-Path $root 'OdfKit')
)
$sourceRoots += Get-ChildItem -LiteralPath $root -Directory -Filter 'OdfKit.Extensions.*' |
    Select-Object -ExpandProperty FullName

$issues = [System.Collections.Generic.List[string]]::new()
$checkedInitializers = 0
foreach ($sourceRoot in $sourceRoots) {
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File -Recurse) {
        if ($file.FullName -match '[\\/]Generated[\\/]' -or $file.Name.EndsWith('.g.cs')) { continue }

        $source = Get-Content -LiteralPath $file.FullName -Raw
        $matches = [regex]::Matches(
            $source,
            '(?:new\s+XmlReaderSettings|XmlReaderSettings\s+\w+\s*=\s*new\s*\(\s*\))',
            [Text.RegularExpressions.RegexOptions]::CultureInvariant)
        foreach ($match in $matches) {
            $checkedInitializers++
            $length = [Math]::Min(1200, $source.Length - $match.Index)
            $window = $source.Substring($match.Index, $length)
            $lineNumber = 1 + ($source.Substring(0, $match.Index).Split("`n").Count - 1)
            $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName)

            if ($window -notmatch 'DtdProcessing\s*=\s*DtdProcessing\.Prohibit') {
                $issues.Add("${relativePath}:$lineNumber 未明確禁止 DTD 處理。")
            }
            if ($window -notmatch 'XmlResolver\s*=\s*null') {
                $issues.Add("${relativePath}:$lineNumber 未明確停用外部 XML resolver。")
            }
        }
    }
}

if ($checkedInitializers -eq 0) {
    throw '未找到任何手寫 XmlReaderSettings，安全性掃描可能已失效。'
}
if ($issues.Count -gt 0) {
    $issues | ForEach-Object { Write-Error $_ }
    throw "XML Reader 安全設定驗證失敗：$($issues.Count) 個問題。"
}

Write-Host "XML Reader 安全設定驗證成功：$checkedInitializers 個手寫設定。"
