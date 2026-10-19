# ODF 1.0～1.4 條款合規追蹤矩陣

建立日期：2026-10-14。這是可重現的**條款級基線與缺口盤點**，不是完整合規認證。
全部版本的已出版編號條款均登錄，但尚未將每段規範文字拆成獨立義務並完成逐句人工裁決。
逐句來源定位、核准勘誤及精確句子裁決另見 [義務裁決追蹤](odf-obligation-adjudication.md)。
未連結證據的條款保留為 evidence-gap，不能算成已通過，也不能直接推論功能沒有實作。

## 索引與來源

| ODF 版本 | 編號條款數 | 逐條矩陣 |
| --- | ---: | --- |
| 1.0 | 993 | [odf-1.0.csv](odf-conformance-matrix/odf-1.0.csv) |
| 1.1 | 1,000 | [odf-1.1.csv](odf-conformance-matrix/odf-1.1.csv) |
| 1.2 | 3,379 | [odf-1.2.csv](odf-conformance-matrix/odf-1.2.csv) |
| 1.3 | 3,428 | [odf-1.3.csv](odf-conformance-matrix/odf-1.3.csv) |
| 1.4 | 3,456 | [odf-1.4.csv](odf-conformance-matrix/odf-1.4.csv) |
| 合計 | 12,256 | [來源與 schema 鎖定](odf-conformance-matrix/sources.json) |

來源為 OASIS 定案出版物，共 14 份：1.0／1.1 官方 PDF、1.2 主文件及三個 part、
1.3／1.4 各四個 part。不是草案或 latest URL。每份保留 URL、SHA-256、條款數與擷取方式，
另外鎖定 repo 內全部 13 份官方 document／manifest／dsig RNG 的 SHA-256。
schema hash 僅將 CRLF 正規化為 LF，以容許 Git checkout 行尾轉換；BOM、其他空白與內容
仍參與 hash。出版物原始下載的 SHA-256 維持逐位元組計算。
下載的原始出版物保留在 ignored `artifacts/odf-conformance-sources`，不提交第三方全文。

1.0／1.1 使用 PDF 的出版目錄；1.2～1.4 使用 HTML 編號標題與本檔目錄，包含 HTML 中
以一般段落排版的 appendix。PDF 的 `#page=` 是出版頁碼。條款編號以版本和 part 分隔，
不假設不同版本的同一編號代表相同要求。

CSV 每列包括穩定 `id`、版本、part、編號、標題、官方連結、scope、status、schema 名稱
及人工 audit 關聯。HTML 編號標題的直屬文字另保留 normalized prose hash 與 modal-word
計數；這是變更偵測／導航資訊，不是完整 normative obligation 數量。PDF／非標題 appendix
沒有這項資料時保留空值，不製造相等的證據。

## 證據狀態

| 狀態 | 條款列數 | 可以佐證的結論 |
| --- | ---: | --- |
| structure-only | 7,320 | 標題中的 QName 可對應官方 RNG；有結構驗證入口，尚無整條語意合規證據。 |
| partial-formula-profile | 1,669 | 已有 Safe Large 計算引擎、語意 corpus 與代表性 OASIS oracle；不能聲稱官方 Small／Medium／Large 全部合規。 |
| evidence-gap | 3,207 | 尚缺條款級證據或人工裁決。包含定義、處理語意與關係要求，不全是已確認程式缺陷。 |
| informative | 60 | 出版物明示 non normative 的章節及其子節；不計為合規義務。 |

父節、子節、非規範性節與義務數量不同；上述列數**不可換算成合規百分比**。
schema 名稱自動關聯也不證明 matcher 與 Jing 完全等價，更不涵蓋 defaults、參照、語意或
應用程式行為。沒有把未審閱的文字自動判定為範圍外或已支援。

[odf-conformance-audits.json](odf-conformance-audits.json) 記錄 30 個優先需求、183 個不同條款
關聯、實作路徑、可定位的測試 symbol、優先級及下一步。audit 的 fixed 只表示描述的子要求
已修正；同一條款仍可同時有其他未涵蓋要求，不能提升整列為 fully conforming。

## 本次修正

| 需求 ID | 原問題 | 修正與回歸證據 |
| --- | --- | --- |
| VAL-001 | 比對原生 schema 前把版本改成該 schema 的合法值。 | 原始版本不再改寫；1.0／1.1 保留官方 string 語法，由版本規則拒絕未知值，1.2+ 另有 grammar 值域驗證。 |
| VAL-002 | Formula／Database 直接跳過整個 document root。 | office:formula 相容形狀仍檢查 envelope 並回報警告；更正 Database 不在官方 grammar 的誤判，移除其豁免，必需 datasource 與非法子節點交由官方 pattern 驗證。 |
| VAL-003 | 1.0／1.1 manifest 輸出未定義的 version 屬性。 | 根 manifest 與根 file-entry 只在支援版本輸出 version；五版均有正向測試。 |
| VAL-004 | 舊 manifest 沒有 version 時用 package 預設 1.4 schema 誤判。 | 缺少宣告時使用偵測文件版本；有宣告時維持 package/document 版本獨立的既有契約。 |
| VAL-005 | `xml:id` 只檢查葉節點，不檢查跨節點重複。 | `ODF3104` 在每個 XML 串流內檢查唯一性；不同嵌入 XML 文件可使用同一 ID。 |
| VAL-006 | 嵌入文件用外層 schema，並把自己的版本改掉。 | pattern 與 namespace scan 使用嵌入文件的宣告版本；已知五版可接受，未知版明確回報。 |
| GAP-006 | 無 manifest version 的舊 package 載入後版本回復至 1.4。 | 解密後從核心 XML 根恢復版本；同步／非同步載入與再儲存保留 1.0／1.1。 |
| PRODUCER-001 | ChartClass 接受未加前綴的名稱卻原樣輸出。 | setter 正規化為 `chart:` token；getter 與 round-trip 傳回 canonical token。已有前綴維持。 |
| PRODUCER-002 | 影像新增／替換後，把 image 放在 title／desc 之後。 | 影像內容插入 metadata 之前；重複替換保留 accessibility metadata，非法逆序仍被 schema 拒絕。 |
| PRODUCER-003 | 資料表描述使用 query 專屬的 db:command。 | 命令保留在有文件定義的 SDK 擴充命名空間，須使用 Extended；讀取舊值並在更新時移除舊屬性。 |
| VAL-007 | Strict 也先刪除 foreign content，可能讓非法內容通過。 | Strict 直接比對原始 grammar；Extended 才使用 foreign-content 處理。metadata 登錄也不能取代 grammar。 |

新增最小正負案例見 [OdfConformanceGapTests.cs](../OdfKit.Tests/OdfConformanceGapTests.cs)。
前一輪的 named range、drop-down、master template 修正另外以 STRUCT-004～006 連結既有
[OasisOdf13RegressionTests.cs](../OdfKit.Tests/OasisOdf13RegressionTests.cs)，不當作本次新增工作。

## 仍屬 SDK 的驗證缺口

| 優先級／ID | 未涵蓋範圍 | 裁決 |
| --- | --- | --- |
| P1／EXEMPT-001 | 裸 MathML 的完整 grammar。 | 明確的 validator-gap；現在有 `ODF3103` 警告，但沒有修成完整 MathML 驗證。 |
| P1／EXEMPT-002 | SDK office:formula 相容形狀與 Database 參照語意。 | Database 已使用官方 grammar，不再豁免；office:formula 非官方 body 分支，仍有明示相容限制，不能算成官方 Formula 合規。 |
| P1／GAP-001 | IDREF、style/name、跨串流參照的目標解析與 scope。 | 重複 `xml:id` 已修正；沒有因此宣稱全部參照可解析。 |
| P1／GAP-003 | QName／namespacedToken 的 namespace 與相等比較。 | data 節點已接入目前元素的 prefix binding 與 whitespace collapse；literal QName 的展開名稱相等比較及完整 facet 證據仍未完成。 |
| P1／GAP-002 | XML Schema whitespace/value-space/facet／regex 與 RELAX NG 引擎的完整等價性。 | evidence-gap；已修正屬性與子內容選擇不同 choice 分支的缺陷，但仍需按官方實際 pattern 的正負 oracle 持續盤點，不能聲稱整個 matcher 已認證。 |
| P1／GAP-004 | RDF metadata 列舉、relative IRI 與 pkg ontology 關係。 | 有 RDF API，但缺 profile 層的完整條款證據；不能歸為沒有 RDF 功能。 |
| P1／GAP-005 | package consumer 的完整 IRI 解析步驟。 | ZIP 路徑安全不等於 IRI consumer 合規；先盤點各 resolver，不以一律拒絕 `..` 取代規範。 |
| P1／CRYPTO-001～002 | 簽章真實性、信任、撤銷及 encryption 的版本／profile 組合。 | 有專用 API、schema 與外部 corpus；profile grammar 通過不代表密碼學與所有組合合規。 |
| P1／FORMULA-001 | 完整 OpenFormula evaluator 義務。 | 核心已有 388 函式計算引擎及代表性 oracle，不能把公式重算整體列為 SDK 範圍外。完整官方 evaluator 合規仍未宣稱。 |

`ODF3103` 是 Warning；`OdfValidationReport.IsValid` 表示沒有 blocking issues，不代表全部
檢查已執行。需要完整覆蓋的採用者應檢查 warnings 與本矩陣，而不是只看 IsValid。

## 範圍外與刻意安全排除

- GUI authoring tool 的提示、鍵盤操作及實際輔助技術互動，屬互動應用程式；文件 XML 的
  accessibility 結構、屬性與保留仍屬 SDK。部分相關指南本身是 non normative。
- 不受信任的 script/macro host 與實際 SQL server 執行，屬外部執行環境；語法、封裝、
  保留及 policy 診斷仍屬 SDK，沒有豁免其 XML。
- Safe OpenFormula profile 刻意排除 DDE 自動執行。這是安全排除／profile 偏差，不能提升成
  官方 Large evaluator 合規；不是宣稱 SDK 完全不支援公式。
- 像素一致性不屬於核心結構驗證器的判定，但專案已有 Rendering／PDF／轉換 extension；
  不把它們的能力或缺口籠統排除於整個 SDK。其品質與互通證據另由 renderer 測試處理。

## 維護與閘門

本次本機驗證（2026-10-14）：新增 31 個 C# 回歸案例；.NET 8／10 各 546 項相關測試
通過，兩個測試 TFM 的 analyzer build 與函式庫 API verify 均為零警告、零錯誤。
內建 corpus 278／278 通過（273 個有效文件來回讀寫無失敗）；OASIS corpus 37／37
通過，TDF baseline 沒有差異或例外。另有 5 個矩陣擷取回歸測試、重建一致性、XML Reader
安全、文件連結、CI 資源政策與資源限制壓力測試通過。這些是本次修正的驗證證據，
不提升本矩陣的完整合規宣稱。

日常／CI 使用不需網路的契約檢查：

```powershell
./eng/Test-OdfConformanceMatrix.ps1
```

它檢查條款完整數量、唯一 ID、來源、status、schema SHA-256、audit 關聯，以及實作檔與
測試 symbol 存在；這些是可追蹤性檢查，不能代替實際執行回歸測試。
外部 baseline workflow 已呼叫此檢查，不新增排程、下載、產品相依或高成本 job。

重建前依 sources.json 的固定 URL 取得 14 份原始 publication，放入該文件指定的檔名，
並驗證下載 SHA-256。使用 Python 3 與 PDF 讀取相依 pypdf：

```powershell
python eng/Build-OdfConformanceMatrix.py --source-dir artifacts/odf-conformance-sources
python eng/Build-OdfConformanceMatrix.py --source-dir artifacts/odf-conformance-sources --verify
python eng/testdata/Test-OdfConformanceMatrix.py
```

重建時 review 出版物 hash、條款增刪及 audit 對應；不能因 schema/generated metadata
有名字就把 status 升級為完全符合。一般測試不重新下載官方全文。
