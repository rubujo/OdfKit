#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..\OdfKit'

# 明確保留邊界的巨型 partial 型別（schema 驅動、功能區切割、加密管線等）
$keepTypes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
@(
    'OdfElement'
    'DefaultFormulaEvaluator'
    'TextDocument'
    'OdfPackage'
    'OdfSchemaPatternValidator'
    'OdfSigner'
    'OdfTableSheet'
    'OdfProfileRuleValidator'
    'OdfPackageValidator'
    'OdfDocument'
    'OdfNode'
    'OdfBouncyCastleOpenPgpProvider'
    'OdfEncryption'
    'SpreadsheetDocument'
    'PresentationDocument'
    'OdfDatabaseDocument'
    'OdfNumberFormatter'
    'OdfFormulaTranslator'
    'OdfStyleEngine'
    'OdsStreamWriter'
    'OdtStreamWriter'
    'OdfLocalizer'                 # 語系資源表拆分（Exceptions.<culture> 等），見 docs/maintainability.md
    'OdfSignatureVerifier'         # 簽章驗證管線（Dsig／Revocation／Timestamp）
    'OdfSchemaPatternAttributeMatcher'  # RELAX NG 屬性模式子驗證器
    'OdfSchemaPatternContentMatcher'    # RELAX NG 內容模式子驗證器
    'OdfElementSchemaRegistry'     # schema 枚舉 token 註冊表（多領域 partial）
    'OdfStreamingMailMerge'        # 串流郵件合併：本體／Segments／ExpressionCache
    'OdfPackageArchiveWriter'      # ZIP 寫入本體／Streams／FlatXml
    'TableTableElement'            # 表格 content model：Structure／Import／Sparse／CellViews
) | ForEach-Object { [void]$keepTypes.Add($_) }

# 已評估完成：雙／多檔拆分具實際邏輯邊界，永久保留（含原 REVIEW 升格）
$validatedDualKeepTypes = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
@(
    'OdfFormulaDocument'
    'OdfImageDocument'
    'OdfPageSetup'
    'OdfParagraph'
    'OdfSlide'
    'OdfCell'
    'OdfChartDocument'
    'OdfDrawPage'
    'OdfMailMergeEngine'
    # 以下為 2026-07 可維護性批次：人工審核後升格 KEEP（勿機械合併）
    'TemplateBinder'      # 繫結引擎本體 vs 欄位／範圍解析（TemplateBinder.Binding.cs）
    'OdsStreamReader'     # 串流讀取狀態機 vs 儲存格／列事件（OdsStreamReader.Cells.cs）
    'DrawingDocument'     # 繪圖文件根 vs Features／Shapes 功能區
    'OdfTable'            # 表格模型根 vs Structure（列欄結構操作）
) | ForEach-Object { [void]$validatedDualKeepTypes.Add($_) }

function Get-PartialTypeName {
    param([string[]]$Lines)
    foreach ($line in $Lines) {
        if ($line -match 'partial\s+(?:class|struct)\s+(\S+)') {
            return $Matches[1]
        }
    }
    return $null
}

function Get-RecommendedAction {
    param(
        [string]$TypeName,
        [int]$PartCount,
        [int]$TotalLines,
        [int]$MinPart,
        [int]$MaxPart,
        [string]$Pattern,
        [string[]]$FileNames
    )

    if ($PartCount -eq 1) { return 'N/A' }
    if ($keepTypes.Contains($TypeName)) { return 'KEEP' }
    if ($validatedDualKeepTypes.Contains($TypeName)) { return 'KEEP' }

    # 極小 helper partial（< 90 行）且檔名暗示可合併
    if ($MinPart -lt 90 -and $MaxPart -gt 150) {
        $smallFiles = $FileNames | Where-Object { $_ -match '\.(Helpers|Candidates|Primitive)\.cs$' }
        if ($smallFiles.Count -gt 0) {
            return 'MERGE-SMALL'
        }
    }

    if ($Pattern -eq 'MANY-SMALL' -and $TotalLines -lt 450) { return 'MERGE' }
    return 'REVIEW'
}

$files = Get-ChildItem -Path $root -Recurse -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\Generated\\' -and $_.Name -notmatch '\.g\.cs$' }

$partials = foreach ($file in $files) {
    $lines = Get-Content -LiteralPath $file.FullName -Encoding UTF8
    $typeName = Get-PartialTypeName -Lines $lines
    if ($typeName) {
        [PSCustomObject]@{
            TypeName = $typeName
            Lines    = $lines.Count
            Path     = $file.FullName.Substring($root.Length + 1)
            FileName = $file.Name
        }
    }
}

$groups = $partials | Group-Object TypeName | Sort-Object { ($_.Group | Measure-Object Lines -Sum).Sum } -Descending

$report = foreach ($g in $groups) {
    $items = $g.Group | Sort-Object Lines -Descending
    $total = ($items | Measure-Object Lines -Sum).Sum
    $maxPart = ($items | Measure-Object Lines -Maximum).Maximum
    $minPart = ($items | Measure-Object Lines -Minimum).Minimum
    $partCount = $items.Count
    $avgPart = [math]::Round($total / $partCount, 0)

    $suffix = if ($partCount -eq 1) { 'SINGLE' }
    elseif ($minPart -lt 80 -and $maxPart -gt 200) { 'MIXED-SIZES' }
    elseif ($avgPart -lt 120) { 'MANY-SMALL' }
    else { 'BALANCED' }

    $fileNames = @($items | ForEach-Object { $_.FileName })
    $action = Get-RecommendedAction -TypeName $g.Name -PartCount $partCount -TotalLines $total `
        -MinPart $minPart -MaxPart $maxPart -Pattern $suffix -FileNames $fileNames

    [PSCustomObject]@{
        TypeName   = $g.Name
        Parts      = $partCount
        TotalLines = $total
        MaxPart    = $maxPart
        MinPart    = $minPart
        AvgPart    = $avgPart
        Pattern    = $suffix
        Action     = $action
        Files      = ($fileNames -join ', ')
    }
}

Write-Host '=== Multi-file partial types ==='
$report | Where-Object { $_.Parts -gt 1 } | Sort-Object TotalLines -Descending | Format-Table -AutoSize -Wrap

$mergeCount = ($report | Where-Object { $_.Action -match '^MERGE' }).Count
$keepCount = ($report | Where-Object { $_.Action -eq 'KEEP' }).Count
$reviewCount = ($report | Where-Object { $_.Action -eq 'REVIEW' }).Count
$multiCount = ($report | Where-Object Parts -gt 1).Count

Write-Host "`nTotal partial types: $($groups.Count)"
Write-Host "Multi-file partial types: $multiCount"
Write-Host "Recommended MERGE: $mergeCount | KEEP: $keepCount | REVIEW: $reviewCount"

if ($mergeCount -eq 0 -and $reviewCount -eq 0) {
    Write-Host 'Status: COMPLETE — 所有 partial 拆分已評估完成，弱拆分已合併，其餘為合理邊界保留。'
}
elseif ($mergeCount -gt 0 -or $reviewCount -gt 0) {
    Write-Host 'Status: PENDING — 尚有 MERGE 或 REVIEW 項目待處理。'
}