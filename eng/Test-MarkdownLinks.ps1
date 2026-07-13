#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$scanRoots = @(
    (Join-Path $root 'README.md'),
    (Join-Path $root 'AGENTS.md'),
    (Join-Path $root 'docs'),
    (Join-Path $root 'eng/README.md'),
    (Join-Path $root 'eng/testdata')
)
$markdownFiles = foreach ($scanRoot in $scanRoots) {
    if (Test-Path -LiteralPath $scanRoot -PathType Leaf) {
        Get-Item -LiteralPath $scanRoot
    }
    elseif (Test-Path -LiteralPath $scanRoot -PathType Container) {
        Get-ChildItem -LiteralPath $scanRoot -Filter '*.md' -File -Recurse
    }
}

function ConvertTo-GitHubAnchor {
    param([string]$Heading)

    $text = $Heading.Trim().ToLowerInvariant()
    $text = [regex]::Replace($text, '<[^>]+>', '')
    $text = [regex]::Replace($text, '!?(?:\[([^\]]+)\])\([^)]+\)', '$1')
    $text = $text.Replace('`', '').Replace('*', '')
    $text = [regex]::Replace($text, '(?<!\w)_|_(?!\w)', '')
    $builder = [Text.StringBuilder]::new()
    foreach ($character in $text.ToCharArray()) {
        if ([char]::IsLetterOrDigit($character) -or $character -eq '-' -or $character -eq '_' -or $character -eq ' ') {
            [void]$builder.Append($(if ($character -eq ' ') { '-' } else { $character }))
        }
    }
    return $builder.ToString()
}

function Get-MarkdownAnchors {
    param([string]$Path)

    $anchors = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $duplicates = @{}
    $inFence = $false
    foreach ($line in [IO.File]::ReadLines($Path)) {
        if ($line -match '^\s*(```|~~~)') {
            $inFence = -not $inFence
            continue
        }
        if ($inFence) { continue }

        foreach ($htmlMatch in [regex]::Matches($line, '<a\s+[^>]*(?:id|name)=["''](?<anchor>[^"'']+)["''][^>]*>', 'IgnoreCase')) {
            [void]$anchors.Add($htmlMatch.Groups['anchor'].Value)
        }
        if ($line -notmatch '^\s{0,3}#{1,6}\s+(?<heading>.+?)\s*#*\s*$') { continue }

        $baseAnchor = ConvertTo-GitHubAnchor $Matches['heading']
        if ([string]::IsNullOrWhiteSpace($baseAnchor)) { continue }
        $count = if ($duplicates.ContainsKey($baseAnchor)) { [int]$duplicates[$baseAnchor] } else { 0 }
        $anchor = if ($count -eq 0) { $baseAnchor } else { "$baseAnchor-$count" }
        $duplicates[$baseAnchor] = $count + 1
        [void]$anchors.Add($anchor)
    }
    return ,$anchors
}

$issues = [System.Collections.Generic.List[string]]::new()
$anchorCache = @{}
foreach ($file in @($markdownFiles | Sort-Object FullName -Unique)) {
    $inFence = $false
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadLines($file.FullName)) {
        $lineNumber++
        if ($line -match '^\s*(```|~~~)') {
            $inFence = -not $inFence
            continue
        }
        if ($inFence) { continue }

        $matches = [System.Collections.Generic.List[System.Text.RegularExpressions.Match]]::new()
        foreach ($match in [regex]::Matches($line, '!?(?:\[[^\]]*\])\((?<target>[^)]+)\)')) {
            $matches.Add($match)
        }
        $definitionMatch = [regex]::Match($line, '^\s*\[[^\]]+\]:\s*(?<target><[^>]+>|\S+)')
        if ($definitionMatch.Success) {
            $matches.Add($definitionMatch)
        }

        foreach ($match in $matches) {
            $target = $match.Groups['target'].Value.Trim()
            if ($target.StartsWith('<') -and $target.EndsWith('>')) {
                $target = $target[1..($target.Length - 2)] -join ''
            }
            if ([string]::IsNullOrWhiteSpace($target) -or
                $target -match '^(?i:https?|mailto):') {
                continue
            }

            $targetParts = $target -split '#', 2
            $pathPart = ($targetParts[0] -split '\?', 2)[0]
            $fragment = if ($targetParts.Count -eq 2) { $targetParts[1] } else { '' }
            try {
                $pathPart = [Uri]::UnescapeDataString($pathPart)
                $fragment = [Uri]::UnescapeDataString($fragment)
            }
            catch {
                $issues.Add("$([IO.Path]::GetRelativePath($root, $file.FullName)):$lineNumber 連結含無效 URL 編碼：$target")
                continue
            }

            $candidate = if ([string]::IsNullOrWhiteSpace($pathPart)) {
                $file.FullName
            }
            else {
                [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $pathPart))
            }
            $relativeCandidate = [IO.Path]::GetRelativePath($root, $candidate)
            if ($relativeCandidate -eq '..' -or $relativeCandidate.StartsWith("..$([IO.Path]::DirectorySeparatorChar)")) {
                $issues.Add("$([IO.Path]::GetRelativePath($root, $file.FullName)):$lineNumber 連結超出 repository：$target")
                continue
            }
            if (-not (Test-Path -LiteralPath $candidate)) {
                $issues.Add("$([IO.Path]::GetRelativePath($root, $file.FullName)):$lineNumber 找不到連結目標：$target")
                continue
            }
            if (-not [string]::IsNullOrWhiteSpace($fragment) -and $candidate.EndsWith('.md', [StringComparison]::OrdinalIgnoreCase)) {
                if (-not $anchorCache.ContainsKey($candidate)) {
                    $anchorCache[$candidate] = Get-MarkdownAnchors $candidate
                }
                if (-not $anchorCache[$candidate].Contains($fragment)) {
                    $issues.Add("$([IO.Path]::GetRelativePath($root, $file.FullName)):$lineNumber 找不到 Markdown anchor：$target")
                }
            }
        }
    }
}

if ($issues.Count -gt 0) {
    $issues | ForEach-Object { Write-Error $_ }
    throw "Markdown 本機連結驗證失敗：$($issues.Count) 個問題。"
}

Write-Host "Markdown 本機連結驗證成功：$(@($markdownFiles).Count) 個檔案。"
