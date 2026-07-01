# 競品對照

本文件整理 OdfKit 與其他可程式化存取 ODF（OpenDocument Format）文件的
函式庫／SDK 的功能、規格深度與授權對照，作為評估與選型參考。比較基準日
為 2026-07；各競品的實際能力可能隨版本更新變動，採用前請以官方文件覆核。

## 對標對象

| 名稱 | 平台／語言 | 授權 | 定位 |
|---|---|---|---|
| **OdfKit** | .NET（`net10.0`＋`netstandard2.0`） | CC0-1.0（公有領域） | 本專案 |
| ODF Toolkit（ODFDOM／Simple API／ODF Validator，`tdf/odftoolkit`） | Java | Apache-2.0 | Java 生態的官方對標基準，OASIS ODF TC 相關社群維護 |
| Apache POI（`poi-odf` 模組） | Java | Apache-2.0 | 以 Office 二進位／OOXML 起家，ODF 支援僅 ODS，非核心定位 |
| LibreOffice SDK（UNO API） | 多語言 bridge，需完整安裝 LibreOffice | MPL-2.0 / LGPL | 完整應用程式而非輕量函式庫，功能最完整但需外部進程 |
| Aspose.Words / Aspose.Cells / Aspose.Slides | .NET／Java 等 | 商業授權 | Office 文件處理商用套件，ODF 為附帶輸出格式之一 |
| GemBox.Document / GemBox.Spreadsheet | .NET | 商業授權 | 輕量商用元件，ODT／ODS 讀寫，無次要格式 |
| Spire.Office for .NET | .NET | 商業授權 | Office 文件商用套件組合，ODF 多為轉出目標而非完整讀寫模型 |
| Syncfusion Document SDK | .NET | 商業授權（社群版有條件免費） | Word→ODT 轉出成熟，Excel 不支援讀取 ODS |

## 功能覆蓋對照

| 項目 | OdfKit | ODF Toolkit | Apache POI | LibreOffice SDK | Aspose | GemBox | Spire.Office | Syncfusion |
|---|---|---|---|---|---|---|---|---|
| ODT/ODS/ODP/ODG 四主格式 | ✅ 全部 complete | ✅（依呼叫端自行組裝深度） | ⚠️ 僅 ODS | ✅（完整應用程式邏輯） | ⚠️ 各產品獨立、深度不一 | ⚠️ 僅 ODT＋ODS | ⚠️ 多為轉出，非完整讀寫模型 | ⚠️ 僅 Word→ODT 轉出 |
| Template／Master／Flat XML 變體 | ✅ 全部 complete | 部分支援 | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| 次要格式（Chart／Formula／Image／Database） | ✅ ODC／ODF／ODI／ODB 全部 complete | 部分（依模組） | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| 真實 ODF 1.1-1.4 RelaxNG schema 驗證 | ✅ CI 內建執行 | ✅（官方 ODF Validator 源自此生態） | ❌ | 間接（應用程式行為） | ❌ | ❌ | ❌ | ❌ |
| OpenFormula 公式評估引擎 | ✅ 內建（含 1.4 新函式 `EASTERSUNDAY`） | 部分 | ❌ | ✅（Calc 核心） | 有限 | 有限 | 有限 | 有限 |
| 數位簽章／OpenPGP 加密（ODF 1.3+） | ✅ | 部分 | ❌ | ✅ | 有限 | ❌ | 有限 | ❌ |
| 無外部進程依賴 | ✅（核心零依賴；Rendering 為選用擴充） | ✅ | ✅ | ❌（必須執行 LibreOffice） | ✅ | ✅ | ✅ | ✅ |
| i18n／在地化訊息 | ✅ 12 語言 | 部分 | N/A | ✅ | 部分 | 部分 | 部分 | 部分 |

## 授權與成本對照

| 名稱 | 授權 | 商業使用限制 | 年費／授權費 |
|---|---|---|---|
| OdfKit | CC0-1.0 | 無（公有領域，不需歸屬聲明） | 免費 |
| ODF Toolkit | Apache-2.0 | 需保留授權與 NOTICE | 免費 |
| Apache POI | Apache-2.0 | 需保留授權與 NOTICE | 免費 |
| LibreOffice SDK | MPL-2.0 / LGPL | 需留意 LGPL 動態連結條款；LibreOffice 本身需另行部署 | 免費（但有運維成本） |
| Aspose 系列 | 商業授權 | 依產品／開發者／伺服器數量計價 | 通常數千美元起／年 |
| GemBox 系列 | 商業授權 | 依授權方案計價 | 中等（一次性授權為主） |
| Spire.Office | 商業授權 | 依授權方案計價 | 中等 |
| Syncfusion | 商業授權（社群版） | 社群版有營收門檻限制 | 中至高 |

## 關鍵結論

1. **.NET 生態中的深度領先**：在 .NET 平台上，OdfKit 是目前唯一同時涵蓋
   四大主格式、全部次要格式（Chart／Formula／Image／Database）、
   Template／Master／Flat XML 變體，並具備真實 ODF 1.1-1.4 schema
   驗證的函式庫。商業套件（Aspose／GemBox／Spire.Office／Syncfusion）
   普遍將 ODF 視為 Office 格式轉出的附帶目標，缺乏次要格式、schema 驗證、
   Flat XML 與主控文件支援。
2. **真正的對標對象是 Java 的 ODF Toolkit／ODFDOM**，而非 .NET 商業套件。
   OdfKit 在覆蓋廣度上已不遜色，甚至次要格式與變體支援更系統化；
   ODF Toolkit 的優勢在於近二十年的生產環境實戰驗證與更龐大的社群基礎，
   這是 OdfKit 仍待累積的信譽差距，而非功能差距。
3. **授權是差異化優勢**：CC0-1.0 較 Apache-2.0 更寬鬆（不需歸屬聲明），
   且完全免費；相對商業套件的年費授權模式，對需要長期商用、避免授權
   稽核風險的場景有明顯吸引力。
4. **透明度是信任基礎**：OdfKit 文件（如 [ODF 1.4 逐章稽核紀錄](odf14-gap-audit.md)、
   [ODF 格式支援矩陣](odf-format-support.md)）刻意記錄已知限制與負向
   互通結果，而非僅宣稱全面支援；這是新興函式庫建立企業信任的正確路徑。

## 延伸閱讀

- [ODF 格式支援矩陣](odf-format-support.md)
- [ODF Toolkit 對標線](odf-toolkit-parity.md)
- [ODF 1.4 逐章稽核紀錄](odf14-gap-audit.md)
