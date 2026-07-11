# 能力宣稱與證據索引

> 內容語系：正體中文（臺灣）（`zh-TW`）。

本索引將能力拆成三個互不推導的維度。機器可讀來源為
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json)，CI 會檢查
claim ID、證據路徑與限制說明。

| Claim | 格式 | 維度 | 層級 | 限制摘要 |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | 封裝來回讀寫不代表公式重算或完整試算表語意。 |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | 讀取已儲存值與公式，不重算公式。 |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | 不提供排版或渲染引擎。 |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | ODP 為 DOM／封裝負載，不宣稱串流投影片 API。 |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | 不實作 SmartArt 佈局或像素級渲染引擎。 |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | 特定 LibreOffice 版本實測不代表所有套件像素一致。 |

`PackageFidelity` 只回答封裝能否安全處理；`SemanticApiDepth` 回答 API 能理解及修改多少文件
語意；`InteropEvidence` 回答哪些外部軟體與版本曾被實測。任何單一維度的最高層級都不能替代
另外兩個維度。

四種主格式的語意族群、CRUD 操作、規格章節、實作、測試、互通證據與限制，
以 [`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json) 為單一事實來源，並由
`eng/Test-SemanticCoverage.ps1` 在 CI 阻擋不完整宣稱。Clean-room 來源邊界見
[`provenance/semantic-api-clean-room.md`](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/semantic-api-clean-room.md)。
