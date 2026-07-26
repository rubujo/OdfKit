#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$issues = [Collections.Generic.List[string]]::new()
$pattern = [regex]::new(
    'catch\s*(?:\([^)]*\))?\s*\{\s*\}',
    [Text.RegularExpressions.RegexOptions]::CultureInvariant)

foreach ($file in Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Filter "*.cs") {
    if ($file.FullName -match '[\\/](?:bin|obj|Generated)[\\/]' -or $file.Name.EndsWith(".g.cs")) {
        continue
    }
    $source = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in $pattern.Matches($source)) {
        $lineStart = $source.LastIndexOf("`n", $match.Index)
        $lineStart = if ($lineStart -lt 0) { 0 } else { $lineStart + 1 }
        $prefix = $source.Substring($lineStart, $match.Index - $lineStart).TrimStart()
        if ($prefix.StartsWith("//", [StringComparison]::Ordinal)) {
            continue
        }
        $line = 1 + ($source.Substring(0, $match.Index).Split("`n").Count - 1)
        $relative = [IO.Path]::GetRelativePath($repoRoot, $file.FullName).Replace('\', '/')
        $issues.Add("${relative}:$line 空白 catch 會靜默吞掉例外；請縮小例外型別並處理、回報或傳播。")
    }
}

if ($issues.Count -gt 0) {
    $issues | ForEach-Object { Write-Host "  * $_" }
    throw "空白 catch 閘門失敗：$($issues.Count) 個問題。"
}

Write-Host "OK：手寫 C# 未含空白 catch。"
