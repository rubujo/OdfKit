# ODF 外部驗證來源稽核

稽核日期：2026-10-14。機器可讀文件與工具盤點見
[oasis-odf-tc-inventory.json](oasis-odf-tc-inventory.json)。本文件記錄採用範圍，
不把來源為官方、schema 通過、封裝可來回讀寫或引擎間一致，解讀成完整語意合規。

## 維護活動與釘選

| 來源 | 本次核對的維護證據 | 採用決定 |
| --- | --- | --- |
| [OPF odf-validator](https://github.com/openpreserve/odf-validator) | 最新 checkout 為 `06e6b6365cfaffe347b6672b1318b277bf62ec0a`，提交時間 2026-03-12T12:03:46Z；[0.20-beta-1 釋出](https://github.com/openpreserve/odf-validator/releases/tag/v0.20-beta-1) 時間 2026-03-12T12:04:42Z。 | 有當年度提交與釋出，採用固定 beta 版本作為獨立外部測試工具。 |
| [OASIS odf-tc](https://github.com/oasis-tcs/odf-tc) | 最新 checkout 為 `16a59d945875bd74834e82e166bebded316478da`，提交時間 2026-03-04T15:10:17+01:00；包含 ODF 1.5 工作資料。 | 官方 TC 仍有當年度更新，採用該 commit 中適用於已定案 ODF 1.3 的測試文件。 |

兩者官方 repository 頁面在本次查核未顯示封存標記。GitHub REST API 因未驗證請求額度
耗盡而不可用；提交時間以實際取得的 Git checkout 核對，釋出時間以 GitHub release 頁面
核對。距稽核日期約七個月沒有新提交不足以判定停止維護，尤其規範工具的更新頻率不同於
產品程式庫；但這也不是高頻維護或長期支援保證。更新版本前須重新核對封存狀態、近期
提交／釋出、未解問題及實際 canary，不得只因版本號較新就更新。

CI 使用固定來源與 SHA-256，不呼叫 latest、不在測試時抓取 master；維護判斷不會讓
既有可重現 gate 因網路或時間經過而自行失效。工具、fixture 與驗證結果都不進入產品
相依圖。下載的第三方 binary 不提交到本儲存庫，也不隨 NuGet 散布。

## OPF 獨立 baseline

[Test-OpfOdfBaseline.ps1](../eng/Test-OpfOdfBaseline.ps1) 對完整內建 corpus 執行
`opf-odf-validator`，另對採用的 OASIS corpus 執行第二份報告。它與 CLI 的
`--baseline odf-validator`（TDF）分開，沒有沿用 TDF 的 Database／Formula 排除清單。
使用 fixture profile 選擇 Extended 模式，不啟用 `--profile` 典藏政策：不允許加密、巨集
等典藏限制不能取代 ODF 合規規則。

初次全 corpus 實測：268 份中 165 份分類一致，103 份有差異。新增主控文字範本後 corpus 另增 10 份，維持同一組已知差異。已逐項核對差異的完整
錯誤特徵，記錄於 [opf-baseline-exceptions.json](opf-baseline-exceptions.json)，主要原因為：

- OPF 的 [OdfXmlValidator](https://github.com/openpreserve/odf-validator/blob/06e6b6365cfaffe347b6672b1318b277bf62ec0a/odf-core/src/main/java/org/openpreservation/odf/validation/OdfXmlValidator.java)
  對 single XML 寫死使用 ODF 1.3 schema，因此拒絕其他版本的合法 flat 文件。
- 通用 document schema 不定義 `office:formula`；OPF 對 Formula 根串流套用它，產生
  Formula／FormulaTemplate 與 FlatFormula 差異，不能解讀成 MathML 已完整驗證。
- Extended package 的 `META-INF/manifest.rdf`：ODF 1.4 Part 2 §2.2.2 明確豁免
  §2.2.1 E 的其他 META-INF 檔案限制；OPF Extended 模式仍回報這項限制。
  [官方封裝規範](https://docs.oasis-open.org/office/OpenDocument/v1.4/os/part2-packages/OpenDocument-v1.4-os-part2-packages.html)。

例外不是「通過」：報告分列 matched、documentedExceptions 與 failed。固定版本、fixture
ID、預期／實際分類及完整 ERROR／FATAL 訊息集合必須一致，只正規化 XML 行列位置；
額外錯誤、差異消失、版本改變或完整 corpus 不再使用某項例外，都會阻擋 gate。
逾時、程序啟動失敗、非 0/1 結束碼、FATAL、缺少 JSON 或結束碼與 JSON 不一致皆為
工具故障，不能冒充文件 invalid。OPF 尚為 beta，已知限制必須伴隨任何涵蓋宣稱。

```powershell
$opf = ./eng/Install-OpfOdfValidator.ps1 -DestinationRoot artifacts/external-tools/opf
./eng/Test-OpfOdfBaseline.ps1 -ValidatorJar $opf -RequireAllExceptions
```

## OASIS 文件盤點與採用

固定 commit 的 `src/test/resources` 共 58 份 ODF 文件／範本。逐項路徑、SHA-256 與決定
記錄在 inventory；採用的 37 份 ODF 1.3 文件（31 正向、6 負向）則另有可直接執行的
[corpus manifest](examples/oasis-odf-tc-corpus/manifest.json)。它們涵蓋四主格式、數值與日期
格式、插值／迴歸圖表、背景填色、文字段落間距及簡報影像。OdfKit、TDF 與 OPF 各自
驗證，並透過既有 corpus runner 檢查 kind、version、SHA-256 與 package round-trip。
負向案例包含不允許的頁首頁尾填色屬性、零起算值與不合法的 drawing-page style 子元素。
不宣稱公式重算、版面或圖表呈現已驗證。

| 決定 | 數量 | 原因 |
| --- | ---: | --- |
| 採用 ODF 1.3 正向文件 | 31 | 三個引擎可重現地接受，另驗證文件來回讀寫；包含主控文字範本。 |
| 採用 ODF 1.3 負向文件 | 6 | 三個引擎一致拒絕；官方來源不代表文件必然合規，這些提供真實負向控制。 |
| 排除 TDF 程序故障 | 1 | index link entry TableObject 的內嵌 MathML 使 TDF 擲出 NPE；工具故障不能當成文件 invalid。 |
| 排除 MovingAveragePeriod | 1 | TDF 拒絕 `Object 1/content.xml` 中的 `chart:symbol-image`；OdfKit／OPF 接受根串流不能證明內嵌圖表合法。 |
| 排除 `.odg` 內裝 flat XML | 1 | 檔名與實際容器類型不同，不符合目前 corpus 的型別偵測契約。 |
| 排除 ODF 1.5 草案 | 7 | 超出產品內建 ODF 1.0～1.4，不能用未定案規範修改現有 oracle。 |
| 排除 HTML export 輸入 | 8 | 預期為特定 XSLT 的 HTML，不能作為 SDK 合規或渲染 oracle。 |
| 排除 ODF 1.2 generator／規範來源範本 | 3 | 提供產生流程，不是帶有可執行分類預期的 fixture。 |

OASIS repository 根 LICENSE 為 TC IPR policy，不能假設整個 repository 採 Apache-2.0。
manifest 使用明確的 download-only 來源標記並連結該 LICENSE；只保留中繼資料，由測試
取得官方公開檔案，不重新散布實體文件、不覆蓋其原有授權或宣稱其為 CC0。

```powershell
./eng/Initialize-OasisOdfTcCorpus.ps1 -DestinationRoot artifacts/oasis-odf-tc
$tdf = ./eng/Install-OdfValidator.ps1 -DestinationRoot artifacts/external-tools/tdf
./eng/Test-OdfCorpus.ps1 -SkipBuild -SkipInternalValidation `
  -ExternalRoot artifacts/oasis-odf-tc `
  -ExternalManifest docs/examples/oasis-odf-tc-corpus/manifest.json -BaselineJar $tdf
./eng/Test-OpfOdfBaseline.ps1 -ValidatorJar $opf `
  -Manifest docs/examples/oasis-odf-tc-corpus/manifest.json -CorpusRoot artifacts/oasis-odf-tc `
  -ReportPath artifacts/opf-oasis-baseline.json
```

## OASIS 工具盤點

另盤點 `src/test` 下 53 份 Java／XSLT／shell 工具檔與根目錄兩份執行入口（共 55 份），逐項 hash 與決定亦保留在 inventory。
目前沒有適合直接作為一般 ODF 文件 gate 的工具，因此不新增 Maven／Saxon 相依：

- `DefaultValueTest.java` 的測試方法為空，不能提供通過證據。
- `getAttributeDefaults.xsl`、schema flattening、錨點／cross-reference 與 RNG HTML 工具
  驗證或產製的是規範文件；SDK schema metadata 不提供同等預設值產物，沒有適用的
  一致性比較目標。
- `odf2html` 與 `xslt-regression-test.sh` 的預期是 HTML export；核心 SDK 不提供相同
  renderer，不能直接把輸出差異當作 ODF 合規錯誤。
- formula test-suite generator 的目標是公式結果／重算。核心已有 Safe Large 計算引擎與
  代表性 OASIS oracle；這兩份 XSLT 目前沒有可直接接入的固定輸入／預期值與有界執行契約，
  因此尚未作為 gate 採用，不能解讀為公式重算不屬於 SDK。
- `Zip.java`／`Unzip.java` 為規範編輯輔助工具，不取代 SDK 有資源上限與路徑防護的 ZIP
  處理。沒有採用其原始碼或直接執行它們處理輸入。

`Test-OdfExternalSourceContracts.ps1` 會驗證 inventory、採用集合、來源 commit／hash、四主
格式與 OPF 例外的對應；專用 `odf-external-baseline.yml` 執行上述實測，不新增排程。

## 五份分類差異的裁決

這五份均有 OdfKit 問題，已修正並納入正向 corpus；沒有新增例外來掩蓋分類差異。

| OASIS 文件 | 根因與證據 | 修正 |
| --- | --- | --- |
| NamedRangeGlobal、PrintOnSeveralPagesW3H2、DCOUNT/DCOUNTA | `table:named-range` 的合法絕對位址含 `$`。ODF 1.3 schema 的 `cellAddress`／`cellRangeAddress` 使用 XML Schema pattern，`$` 是字元而非 .NET 結尾錨點。移除 foreign 內容後，官方 schema 的 Jing 驗證亦通過。 | 轉譯未跳脫且不在字元類別中的 `^`／`$`，並執行完整字串比對；非法位址與尾端垃圾仍須拒絕。 |
| dropdown input list | 官方 `text-drop-down` pattern 是零或多個 `text:label` 加上 `rng:text`；OdfKit 錯誤禁止有子元素時同時存在直接文字。Jing、TDF、OPF 接受。 | `rng:text` 可與 label 共存，仍拒絕非法 boolean、缺少必要名稱或未允許的子元素。 |
| empty master template | ODF 1.3 Part 3 §2.2.3 明列 `application/vnd.oasis.opendocument.text-master-template`，但 OdfKit 的格式與 profile 清單漏列。 | 增加 `TextMasterTemplate`、`.otm`、主控／範本分類與建立／載入；僅 OASIS 1.3／1.4 profile 放行，1.2 仍拒絕。 |

DCOUNT 文件另含合法的 `calcext:value-type` 擴充。TDF `-e` 接受，但預設 conformance
模式拒絕；這是比較模式未對齊，不是上游 bug。CLI 的 `validate` 與 `validate-corpus`
現在依 profile 的 `RequireForeignExtensionIsolation` 規則選擇 Extended 模式；既有外部
API 預設行為維持，另提供顯式 extendedConformance 多載。真實 JAR 的回歸測試同時驗證
一般模式仍拒絕、Extended 模式接受，以及兩個 CLI 入口正確選擇模式。

正規表示式裁決依據為 [W3C XML Schema Part 2 Appendix F](https://www.w3.org/TR/xmlschema-2/#regexs)；
主控文字範本依據為 [OASIS ODF 1.3 Part 3](https://docs.oasis-open.org/office/OpenDocument/v1.3/os/part3-schema/OpenDocument-v1.3-os-part3-schema.html)。
最小正負回歸測試在 `OdfKit.Tests/OasisOdf13RegressionTests.cs`；外部文件依固定 commit 與
hash 下載，不把第三方 binary 複製進單元測試。既有自訂 pattern 測試改用 XML Schema
語法，不再以 .NET 的 `^...$` 錨點語法冒充規範 pattern。

## 能否宣稱百分之百符合 OASIS ODF

目前不能。這是證據與涵蓋範圍的結論，不能把 `165/268`、已知差異數或 corpus 通過率
換算為規範合規百分比；外部驗證器亦有已重現的缺陷。

- OASIS 對文件、package、producer 與 consumer 分別定義合規條件，Strict／Extended
  與不同版本也有不同要求。必須限定目標與版本，不能以一份 SDK 的綜合測試宣稱全部合規。
- 官方 RELAX NG schema 能提供結構／資料型別的證據，不能單獨證明所有語意、跨串流
  關係、公式結果、數位簽章真實性或 consumer 行為。
- 核心驗證對裸 MathML 根節點有明確 schema pattern 豁免，不能以該 gate 的通過宣稱
  MathML 已完整驗證；formula 保留／來回讀寫亦不代表完整 OpenFormula 重算。
- 已建立 [ODF 1.0～1.4 條款矩陣](odf-conformance-matrix.md)，保留結構證據、人工裁決、
  未涵蓋與範圍界線。所有 normative obligation 的逐句裁決與完整跨產品互通證據仍未完成，
  不能把條款索引的完整登錄解讀為全部合規。

目前可支援的說法是：OdfKit 以 OASIS 官方 schema 與版本化 profile 為依據，對列明的
文件與操作提供可重現的結構、封裝及來回讀寫驗證，另以 Jing／TDF／OPF 交叉檢查；
各自的例外與未涵蓋部分另列，不宣稱完整 ODF 合規認證。
