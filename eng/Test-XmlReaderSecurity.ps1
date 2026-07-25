#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# 掃描範圍：正式程式碼（OdfKit、OdfKit.Extensions.*、OdfKit.WebFonts.*）
# 以及輔助程式碼（tools/、tests/、samples/、OdfKit.Tests）。
# 輔助程式碼一併掃描的原因：tools/OdfSchemaGenerator 等目錄內含會被實際執行的
# XML 解析邏輯，同樣需要防退化；OdfKit.Tests 內含刻意建構的不安全 XmlDocument
# 測試素材，透過下方的 $xmlDocumentWaivers 明確豁免，而非整目錄跳過。
$sourceRoots = @(
    (Join-Path $root 'OdfKit')
    (Join-Path $root 'OdfKit.Tests')
    (Join-Path $root 'tools')
    (Join-Path $root 'tests')
    (Join-Path $root 'samples')
)
$sourceRoots += Get-ChildItem -LiteralPath $root -Directory -Filter 'OdfKit.Extensions.*' |
    Select-Object -ExpandProperty FullName
$sourceRoots += Get-ChildItem -LiteralPath $root -Directory -Filter 'OdfKit.WebFonts.*' |
    Select-Object -ExpandProperty FullName
$sourceRoots = $sourceRoots | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -Unique

# --- new XmlDocument() 豁免清單 -------------------------------------------
# 每一筆豁免都以「相對路徑 + 行號」鎖定，並附上理由，供稽核追溯。
# 路徑一律使用正斜線，與下方正規化後的 $relativePath 一致（跨 Windows／Linux）。
# 這些都是 OdfKit.Tests/AdvancedSecurityTests.cs 內刻意建構、供負向測試使用的
# XmlDocument（讀取測試流程自行產生的簽章 XML，非解析外部不受信任輸入）。
# 豁免清單採「行號 + 內容雙重比對」：掃描時若某筆豁免對應的行已不再包含
# `new XmlDocument`（代表程式碼已搬移或修改），該筆豁免視為過期，腳本會直接
# 失敗並要求更新此清單──藉此避免豁免清單長期漂移、掩護新的不安全用法。
# 新增任何未列於此清單、且未在同運算式內設定 XmlResolver = null 的
# `new XmlDocument`，都會被下方的檢查攔截。
$xmlDocumentWaivers = @(
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 60; Reason = '讀取測試自行簽署之 documentsignatures.xml，驗證版本屬性，非解析不受信任輸入。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 158; Reason = 'XML-DSig 簽章驗證負向測試：讀取測試自行產生之簽章 XML。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 250; Reason = 'XML-DSig 簽章驗證負向測試：讀取測試自行產生之簽章 XML。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 361; Reason = 'XML-DSig 簽章驗證負向測試：讀取測試自行產生之簽章 XML。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 1340; Reason = '時間戳竄改負向測試：讀取測試自行產生之簽章 XML 以擷取欄位後竄改。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 1394; Reason = '時間戳竄改負向測試：讀取測試自行產生之簽章 XML 以擷取欄位後竄改。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 1526; Reason = '時間戳竄改負向測試：讀取測試自行產生之簽章 XML 以擷取欄位後竄改。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 1627; Reason = '雙套件時間戳比對測試：讀取測試自行產生之簽章 XML。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 1641; Reason = '雙套件時間戳比對測試：讀取測試自行產生之簽章 XML。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 1664; Reason = '雙套件時間戳比對測試：讀取測試自行產生之簽章 XML。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 1676; Reason = '雙套件時間戳比對測試：讀取測試自行產生之簽章 XML（第二份套件）。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 1685; Reason = '以乾淨 XmlDocument 匯入既有節點以計算 C14N 雜湊，內容來自本測試流程。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 1778; Reason = '時間戳交叉驗證負向測試：讀取測試自行產生之簽章 XML。' }
    @{ Path = 'OdfKit.Tests/AdvancedSecurityTests.cs'; Line = 1788; Reason = '以乾淨 XmlDocument 匯入既有節點以計算 C14N 雜湊，內容來自本測試流程。' }
)
$xmlDocumentWaiversMatched = [System.Collections.Generic.HashSet[string]]::new()

$issues = [System.Collections.Generic.List[string]]::new()
$checkedReaderSettings = 0
$checkedXmlDocuments = 0

foreach ($sourceRoot in $sourceRoots) {
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File -Recurse) {
        if ($file.FullName -match '[\\/]Generated[\\/]' -or $file.Name.EndsWith('.g.cs')) { continue }

        $source = Get-Content -LiteralPath $file.FullName -Raw
        # 一律正規化為正斜線：[IO.Path]::GetRelativePath 在 Windows 回傳反斜線、在 Linux
        # 回傳正斜線，而 CI 的 maintainability job 跑在 ubuntu-latest。若不正規化，下方
        # 以路徑字串比對的豁免清單會在 Linux 上完全比不中，導致每一筆豁免同時被判為
        # 「未豁免」與「已過期」，本機通過、CI 卻整批失敗。
        $relativePath = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')

        # --- 檢查一：手寫 XmlReaderSettings 必須明確禁止 DTD 與外部 resolver ---
        $readerMatches = [regex]::Matches(
            $source,
            '(?:new\s+XmlReaderSettings|XmlReaderSettings\s+\w+\s*=\s*new\s*\(\s*\))',
            [Text.RegularExpressions.RegexOptions]::CultureInvariant)
        foreach ($match in $readerMatches) {
            $checkedReaderSettings++
            $length = [Math]::Min(1200, $source.Length - $match.Index)
            $window = $source.Substring($match.Index, $length)
            $lineNumber = 1 + ($source.Substring(0, $match.Index).Split("`n").Count - 1)

            if ($window -notmatch 'DtdProcessing\s*=\s*DtdProcessing\.Prohibit') {
                $issues.Add("${relativePath}:$lineNumber 未明確禁止 DTD 處理。")
            }
            if ($window -notmatch 'XmlResolver\s*=\s*null') {
                $issues.Add("${relativePath}:$lineNumber 未明確停用外部 XML resolver。")
            }
        }

        # --- 檢查二：new XmlDocument() 必須停用外部 resolver ---------------
        # .NET Framework 上 XmlDocument.XmlResolver 預設為 XmlUrlResolver（非
        # null），故任何手寫 new XmlDocument 都必須明確停用。比對策略分兩層：
        #   1) 物件初始設定式內同時寫 XmlResolver = null（如
        #      `new XmlDocument { XmlResolver = null }`）；
        #   2) 先以 `var x = new XmlDocument();` 宣告變數，之後在同一份原始碼
        #      中另有 `x.XmlResolver = null;`。此寫法容易誤判——只要「之後任
        #      何位置」出現對同名變數的指定就會視為安全，並不驗證指定發生在
        #      第一次使用（例如 Load）之前。已知限制：若變數名稱重複使用
        #      （例如迴圈中重新宣告的同名區域變數），比對範圍是整份檔案而非
        #      單一作用域，可能造成誤判為安全。目前程式碼庫未出現此樣式，
        #      但未來若新增「宣告後才設定」的 XmlDocument，請自行確認語意正確。
        $docMatches = [regex]::Matches(
            $source,
            'new\s+XmlDocument\s*\(\s*\)|new\s+XmlDocument\s*\{',
            [Text.RegularExpressions.RegexOptions]::CultureInvariant)
        foreach ($match in $docMatches) {
            $checkedXmlDocuments++
            $lineNumber = 1 + ($source.Substring(0, $match.Index).Split("`n").Count - 1)

            # 同一敘述內（至下一個分號為止）是否已設定 XmlResolver = null。
            $semicolonIndex = $source.IndexOf(';', $match.Index)
            $statementEnd = if ($semicolonIndex -ge 0) { $semicolonIndex } else { [Math]::Min($source.Length - 1, $match.Index + 400) }
            $statementWindow = $source.Substring($match.Index, $statementEnd - $match.Index + 1)
            $isSafe = $statementWindow -match 'XmlResolver\s*=\s*null'

            if (-not $isSafe) {
                # 嘗試找出 `var name = new XmlDocument` 的變數名稱，往後搜尋
                # 是否有 `name.XmlResolver = null`。
                $lineStart = $source.LastIndexOf("`n", $match.Index)
                $lineStart = if ($lineStart -lt 0) { 0 } else { $lineStart + 1 }
                $prefixOnLine = $source.Substring($lineStart, $match.Index - $lineStart)
                $varMatch = [regex]::Match($prefixOnLine, '(?:var|XmlDocument)\s+(?<name>\w+)\s*=\s*$')
                if ($varMatch.Success) {
                    $varName = [regex]::Escape($varMatch.Groups['name'].Value)
                    $forwardLength = [Math]::Min(2000, $source.Length - $match.Index)
                    $forwardWindow = $source.Substring($match.Index, $forwardLength)
                    if ($forwardWindow -match "$varName\s*\.\s*XmlResolver\s*=\s*null") {
                        $isSafe = $true
                    }
                }
            }

            if (-not $isSafe) {
                $waiver = $xmlDocumentWaivers | Where-Object {
                    $_.Path -eq $relativePath -and $_.Line -eq $lineNumber
                } | Select-Object -First 1

                if ($null -ne $waiver) {
                    $xmlDocumentWaiversMatched.Add("$($waiver.Path):$($waiver.Line)") | Out-Null
                }
                else {
                    $issues.Add("${relativePath}:$lineNumber new XmlDocument 未明確停用外部 XML resolver（XmlResolver = null），且未列於豁免清單。")
                }
            }
        }
    }
}

# --- 豁免清單過期偵測：清單中未被實際比對到的項目視為失效設定 -------------
foreach ($waiver in $xmlDocumentWaivers) {
    $key = "$($waiver.Path):$($waiver.Line)"
    if (-not $xmlDocumentWaiversMatched.Contains($key)) {
        $issues.Add("豁免清單項目已過期或位置不符：$key（該行已不含未受保護的 new XmlDocument，請更新或移除此豁免）。")
    }
}

# --- 保險絲：任一類檢查若比對數為 0，代表正規表示式或掃描範圍已失效 -------
if ($checkedReaderSettings -eq 0) {
    throw '未找到任何手寫 XmlReaderSettings，安全性掃描可能已失效。'
}
if ($checkedXmlDocuments -eq 0) {
    throw '未找到任何手寫 new XmlDocument，安全性掃描可能已失效。'
}

if ($issues.Count -gt 0) {
    # 用 Write-Host 而非 Write-Error 列出問題：本腳本開頭設定 $ErrorActionPreference = 'Stop'，
    # 在該模式下 Write-Error 會在「第一筆」就終止腳本，導致其餘問題與下方的總數摘要永遠不會
    # 顯示；排查時只看得到一個問題，會誤判影響範圍。改以 Write-Host 全數列出後再 throw。
    $issues | ForEach-Object { Write-Host "  * $_" }
    throw "XML Reader 安全設定驗證失敗：$($issues.Count) 個問題。"
}

Write-Host "XML Reader 安全設定驗證成功：$checkedReaderSettings 個手寫 XmlReaderSettings、$checkedXmlDocuments 個手寫 XmlDocument（其中 $($xmlDocumentWaivers.Count) 個依豁免清單放行）。"
