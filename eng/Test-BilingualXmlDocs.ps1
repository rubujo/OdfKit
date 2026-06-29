param(
    [switch]$FailOnIssues
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourceRoots = @(
    'OdfKit',
    'OdfKit.Extensions.Collaboration',
    'OdfKit.Extensions.Html',
    'OdfKit.Extensions.Imaging',
    'OdfKit.Extensions.Ooxml',
    'OdfKit.Extensions.Pdf',
    'OdfKit.Extensions.Rdf',
    'OdfKit.Extensions.Rendering'
)

$declarationPattern = '^\s*(public|protected|protected\s+internal|private\s+protected)\s+(?!const\b)(?!readonly\b)(?!event\b).+'
$memberNamePattern = '\b([A-Za-z_][A-Za-z0-9_]*)\s*(?:\(|\{|=>|:)'
$typeDeclarationPattern = '^\s*(?:(public|protected|protected\s+internal|private\s+protected|internal|private)\s+)?(?:sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+|ref\s+)*\b(class|struct|record|interface|enum)\b'

function Test-GeneratedPath {
    param([string]$Path)

    $normalized = $Path.Replace('\', '/')
    return $normalized -match '/Generated/' -or
        $normalized -match '\.g\.cs$' -or
        $normalized -match '/bin/' -or
        $normalized -match '/obj/'
}

function Get-XmlDocBlock {
    param(
        [string[]]$Lines,
        [int]$DeclarationIndex
    )

    $index = $DeclarationIndex - 1
    while ($index -ge 0 -and
        ($Lines[$index] -match '^\s*$' -or
         $Lines[$index] -match '^\s*\[[^\]]+\]\s*$'))
    {
        $index--
    }

    $block = New-Object System.Collections.Generic.List[string]
    while ($index -ge 0 -and $Lines[$index] -match '^\s*///')
    {
        $block.Insert(0, $Lines[$index])
        $index--
    }

    return $block.ToArray()
}

function Get-SummaryLines {
    param([string[]]$DocBlock)

    $inside = $false
    $summary = New-Object System.Collections.Generic.List[string]
    foreach ($line in $DocBlock)
    {
        $text = ($line -replace '^\s*///\s?', '').Trim()
        if ($text -match '<summary>')
        {
            $inside = $true
            $text = ($text -replace '.*<summary>\s*', '').Trim()
            if ($text.Length -gt 0)
            {
                $summary.Add($text)
            }
            continue
        }

        if ($text -match '</summary>')
        {
            $text = ($text -replace '\s*</summary>.*', '').Trim()
            if ($text.Length -gt 0)
            {
                $summary.Add($text)
            }
            $inside = $false
            continue
        }

        if ($inside -and $text.Length -gt 0)
        {
            $summary.Add($text)
        }
    }

    return $summary.ToArray()
}

function Get-MemberName {
    param([string]$Line)

    $clean = $Line -replace '<[^>]+>', ''
    $typeMatch = [regex]::Match($clean, '\b(class|struct|record|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)')
    if ($typeMatch.Success)
    {
        return $typeMatch.Groups[2].Value
    }

    $match = [regex]::Match($clean, $memberNamePattern)
    if ($match.Success)
    {
        return $match.Groups[1].Value
    }

    return '<unknown>'
}

function Test-BilingualSummary {
    param([string[]]$SummaryLines)

    if ($SummaryLines.Count -lt 2)
    {
        return 'summary-not-bilingual'
    }

    $english = $SummaryLines[0]
    $chinese = $SummaryLines[1]
    if ($english -notmatch '[A-Za-z]' -or $english -match '\p{IsCJKUnifiedIdeographs}')
    {
        return 'summary-english-line-invalid'
    }

    if ($chinese -notmatch '\p{IsCJKUnifiedIdeographs}')
    {
        return 'summary-chinese-line-missing'
    }

    return $null
}

function Get-BraceDelta {
    param([string]$Line)

    $withoutLineComment = ($Line -split '//', 2)[0]
    $open = ([regex]::Matches($withoutLineComment, '\{')).Count
    $close = ([regex]::Matches($withoutLineComment, '\}')).Count
    return $open - $close
}

function Test-TypeDeclarationEnclosesLine {
    param(
        [string[]]$Lines,
        [int]$TypeDeclarationIndex,
        [int]$TargetIndex
    )

    $openingIndex = -1
    for ($index = $TypeDeclarationIndex; $index -le $TargetIndex; $index++)
    {
        if ($Lines[$index] -match '\{')
        {
            $openingIndex = $index
            break
        }
    }

    if ($openingIndex -lt 0)
    {
        return $false
    }

    $depth = 0
    for ($index = $openingIndex; $index -lt $TargetIndex; $index++)
    {
        $depth += Get-BraceDelta $Lines[$index]
        if ($depth -le 0)
        {
            return $false
        }
    }

    return $depth -gt 0
}

function Test-InNonPublicType {
    param(
        [string[]]$Lines,
        [int]$DeclarationIndex
    )

    for ($index = $DeclarationIndex - 1; $index -ge 0; $index--)
    {
        $match = [regex]::Match($Lines[$index], $typeDeclarationPattern)
        if (-not $match.Success)
        {
            continue
        }

        if (-not (Test-TypeDeclarationEnclosesLine -Lines $Lines -TypeDeclarationIndex $index -TargetIndex $DeclarationIndex))
        {
            continue
        }

        $accessibility = $match.Groups[1].Value
        if ($accessibility -ne 'public' -and
            $accessibility -ne 'protected' -and
            $accessibility -ne 'protected internal' -and
            $accessibility -ne 'private protected')
        {
            return $true
        }
    }

    return $false
}

$issues = New-Object System.Collections.Generic.List[object]

foreach ($sourceRoot in $sourceRoots)
{
    $absoluteRoot = Join-Path $root $sourceRoot
    if (-not (Test-Path -LiteralPath $absoluteRoot))
    {
        continue
    }

    Get-ChildItem -LiteralPath $absoluteRoot -Filter '*.cs' -Recurse |
        Where-Object { -not (Test-GeneratedPath $_.FullName) } |
        ForEach-Object {
            $file = $_.FullName
            $lines = Get-Content -LiteralPath $file
            for ($i = 0; $i -lt $lines.Count; $i++)
            {
                $line = $lines[$i]
                if ($line -notmatch $declarationPattern)
                {
                    continue
                }

                if (Test-InNonPublicType -Lines $lines -DeclarationIndex $i)
                {
                    continue
                }

                $docBlock = Get-XmlDocBlock -Lines $lines -DeclarationIndex $i
                $relativePath = [System.IO.Path]::GetRelativePath($root, $file)
                $member = Get-MemberName $line
                if ($docBlock.Count -eq 0)
                {
                    $issues.Add([pscustomobject]@{
                        File = $relativePath
                        Line = $i + 1
                        Member = $member
                        Issue = 'missing-xml-doc'
                    })
                    continue
                }

                $summaryLines = Get-SummaryLines -DocBlock $docBlock
                if ($summaryLines.Count -eq 0)
                {
                    $issues.Add([pscustomobject]@{
                        File = $relativePath
                        Line = $i + 1
                        Member = $member
                        Issue = 'missing-summary'
                    })
                    continue
                }

                $summaryIssue = Test-BilingualSummary -SummaryLines $summaryLines
                if ($summaryIssue)
                {
                    $issues.Add([pscustomobject]@{
                        File = $relativePath
                        Line = $i + 1
                        Member = $member
                        Issue = $summaryIssue
                    })
                }
            }
        }
}

if ($issues.Count -eq 0)
{
    Write-Host 'No bilingual XML documentation issues found.'
    exit 0
}

$issues |
    Sort-Object File, Line |
    ForEach-Object {
        Write-Output ("{0}:{1}: {2} [{3}]" -f $_.File, $_.Line, $_.Member, $_.Issue)
    }

Write-Host ("TOTAL={0}; FILES={1}" -f $issues.Count, (($issues | Select-Object -ExpandProperty File -Unique).Count))

if ($FailOnIssues)
{
    exit 1
}

exit 0
