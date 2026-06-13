# Typed DOM Coverage

本文件記錄 OdfKit typed DOM 對標 ODFDOM 的目前完成線。typed DOM 的目標不是取代高階文件 facade，而是提供 schema-aware、prefix-independent、可保真的 ODF-first 操作層。

## 目前能力

- `OdfNodeFactory.CreateElement(...)` 會先呼叫 generated factory，找不到 typed wrapper 時才回退到通用 `OdfElement`。
- `OdfElement.cs` 保留常用手寫 wrapper，例如 text paragraph、heading、table cell、draw frame、office document 與 manifest file entry。
- `GeneratedDomWrappers.g.cs` 由 ODF 1.4 schema metadata 產生，涵蓋大量 schema element wrapper、factory case 與 attribute property，且已重產生為第一批 typed datatype property。
- `OdfTypedDomCoverage.Build()` 會產生 machine-readable schema-to-wrapper report，列出 schema element、wrapper type、fallback 狀態、schema child element relation、wrapper property 數、schema attribute value type 分布與 wrapper property CLR type 分布。
- CLI `typed-dom-coverage --format json` 可輸出同一份 report，供 CI 或 release artifact 保存。
- `eng/Test-OdfTypedDomCoverage.ps1` 會產生 `artifacts/typed-dom-coverage/odf-typed-dom-coverage.json`，GitHub Actions `Typed DOM coverage` workflow 會上傳同一份 artifact。
- `OdfElement` 已提供 typed child facade，可用泛型 `ChildElements<TElement>()`、`DescendantElements<TElement>()`、`AppendElement<TElement>()` 與 typed insert helper 操作 generated 與手寫 wrapper，並保留未知節點與原始 DOM child list。
- `GeneratedDomWrappers.g.cs` 已依 schema child element relation 產生 schema-specific child collection property，例如 `OfficeTextChildElements`、`TableTableRowChildElements` 與 `TextPChildElements`。
- `TypedDomParityTests` 已用 ODFDOM 風格 user story 覆蓋 text body、spreadsheet table 與 image frame / SVG alternative text 的 typed 建立、schema-specific child collection traversal 與 XML round-trip。
- `OdfElement` 已提供 schema-aware typed attribute helpers，涵蓋 `int`、`bool`、`decimal`、ODF 日期時間字串、XML Schema `time` / `duration`、ODF 長度 / 百分比、三段邊框線寬、0 到 100 百分比、角度、FO 分頁保持 / 換行選項 / 文字轉換 / 文字對齊、3D 投影 / 著色模式、SVG 填滿規則 / 線端樣式、表格邊框模型、文字清單標籤 / 清單定位 / 索引範圍 / 資料表來源 / 錨定 / 註解類別 / 頁面選取 / 參照格式 / 起始編號 / 註腳位置 / 標號序列 / 編號位置 / 預留位置 / 動畫 / 動畫方向 / 索引項目種類、線條樣式 / 類型 / 寬度 / 模式、繪圖填滿 / 圖片參照點 / 色彩模式 / 線條接合、通用字型家族 / 字型間距 / 字型浮雕 / 字型伸縮 / 字型樣式 / 變體 / 粗細、樣式斷行 / 背景重複 / 方向 / 書寫方向 / 文字旋轉縮放 / 文字組合 / 文字系統類型 / 文字強調 / 水平與垂直定位 / 垂直對齊 / 文繞圖 / 穿越排列 / 輪廓繞排、表單方向、表格方向 / 方位 / 成員顯示方向 / 版面配置模式、資料庫參照動作規則、XLink 類型 / 顯示 / 觸發行為、數字樣式長短 / 音譯樣式、表格排序 / 類型、簡報效果 / 速度 / 動作 / 轉場類型 / 轉場樣式 / 預設動畫類別、樣式名稱 / 樣式名稱參照清單、色彩、IRI 參照、儲存格位址 / 範圍位址、儲存格範圍位址清單、三維向量 / 三維點、二維整數座標清單、語言 / 國別 / 文字系統 token、命名空間 token、單一字元、文字編碼名稱、目標框架名稱、XML 名稱、`style:family`、`office:version` 與 MIME 類型。
- `OdfElement` 另提供 schema token helpers，涵蓋 `number:calendar`、`table:member-type`、`table:grouped-by`、`table:sort-mode`、`table:condition-source`、`table:function`、`db:is-nullable`、`db:data-source-setting-type`、`anim:color-interpolation`、`anim:color-interpolation-direction` 與 `draw:nohref`。
- `DomWrappersCSharpWriter` 會從 RELAX NG `data` / `value` 節點與常用 named pattern 推斷 datatype，讓 wrapper 可把 boolean、integer、decimal、date/dateTime、time、duration、length/percent、borderWidths、bounded percent、angle、foKeepTogether/foWrapOption/foTextTransform/foTextAlign、dr3dProjection/dr3dShadeMode、svgFillRule/svgStrokeLineCap、tableBorderModel、textLabelFollowedBy/textListLevelPositionMode/textIndexScope/textTableType/textAnchorType/textNoteClass/textSelectPage/textReferenceFormat/textStartNumberingAt/textFootnotesPosition/textCaptionSequenceFormat/textNumberPosition/textPlaceholderType/textAnimation/textAnimationDirection/textKind、drawFill/drawFillImageRefPoint/drawColorMode/drawStrokeLineJoin、lineStyle/lineType/lineWidth/lineMode、fontFamilyGeneric/fontPitch/fontRelief/fontStretch/fontStyle/fontVariant/fontWeight、styleLineBreak/styleRepeat/styleDirection/styleWritingMode/styleTextRotationScale/styleTextCombine/styleVerticalAlign/styleVerticalPos/styleVerticalRel/styleHorizontalPos/styleHorizontalRel/styleWrap/styleRunThrough/styleWrapContourMode/styleScriptType/styleTextEmphasize、formOrientation、tableDirection/tableOrientation/tableDisplayMemberMode/tableLayoutMode、databaseRule、xLinkType/xLinkShow/xLinkActuate、numberStyle/numberTransliterationStyle、tableOrder/tableType、presentationEffect/presentationSpeed/presentationAction/presentationTransitionType/presentationTransitionStyle/presentationPresetClass、style name/list、color、IRI reference、cell address/range address、cell range address list、vector3D、point3D、points、language/country/script tokens、namespacedToken、character、textEncoding、targetFrameName、XML name、style family、ODF version 與 media type attribute 輸出為可空強型別屬性；未知或衝突型別會保守維持 `string?`。
- generator 也會把上述 schema token attribute 輸出為對應 enum 屬性；目前 generated `string?` wrapper property 已收斂至 39691 個。
- 所有元素與屬性比對仍以 namespace URI + local name 為準，不依賴 XML prefix。
- generated wrapper 保留局部 CS1591 pragma；手寫 public / protected API 仍必須具備正體中文 XML docs。

## Coverage guard

目前 readiness test 固定下列最低門檻：

| Metric | Minimum |
|---|---:|
| Generated typed element classes | 550 |
| Generated factory cases | 590 |
| Generated attribute properties | 100000 |
| Schema child element relations in coverage report | 2000 |
| Generated child collection properties | 2000 |
| Generated integer attribute properties | 1000 |
| Generated boolean attribute properties | 10000 |
| Generated decimal attribute properties | 100 |
| Generated date/time attribute properties | 100 |
| Generated time attribute properties | 6 |
| Generated duration attribute properties | 1000 |
| Generated length / percent attribute properties | 10000 |
| Generated border widths attribute properties | 723 |
| Generated angle attribute properties | 1000 |
| Generated style name attribute properties | 1000 |
| Generated style name list attribute properties | 300 |
| Generated color attribute properties | 1000 |
| Generated IRI reference attribute properties | 400 |
| Generated bounded percent attribute properties | 1000 |
| Generated cell address attribute properties | 400 |
| Generated cell range address attribute properties | 400 |
| Generated cell range address list attribute properties | 800 |
| Generated vector3D attribute properties | 1000 |
| Generated point3D attribute properties | 90 |
| Generated points attribute properties | 90 |
| Generated language code attribute properties | 100 |
| Generated country code attribute properties | 100 |
| Generated script code attribute properties | 100 |
| Generated language tag attribute properties | 100 |
| Generated namespaced token attribute properties | 100 |
| Generated character attribute properties | 100 |
| Generated text encoding attribute properties | 438 |
| Generated target frame name attribute properties | 205 |
| Generated XLink type attribute properties | 172 |
| Generated XLink show attribute properties | 160 |
| Generated XLink actuate attribute properties | 167 |
| Generated number style attribute properties | 109 |
| Generated number calendar attribute properties | 106 |
| Generated table order attribute properties | 108 |
| Generated table type attribute properties | 102 |
| Generated presentation effect attribute properties | 131 |
| Generated presentation speed attribute properties | 231 |
| Generated presentation action attribute properties | 125 |
| Generated presentation transition type attribute properties | 99 |
| Generated presentation transition style attribute properties | 99 |
| Generated FO text transform attribute properties | 111 |
| Generated FO text align attribute properties | 106 |
| Generated style text rotation scale attribute properties | 111 |
| Generated style text combine attribute properties | 111 |
| Generated draw fill attribute properties | 109 |
| Generated draw fill image ref point attribute properties | 109 |
| Generated draw color mode attribute properties | 99 |
| Generated style vertical align attribute properties | 105 |
| Generated style vertical pos attribute properties | 106 |
| Generated style vertical rel attribute properties | 106 |
| Generated style horizontal pos attribute properties | 99 |
| Generated style horizontal rel attribute properties | 99 |
| Generated style wrap attribute properties | 99 |
| Generated style run-through attribute properties | 99 |
| Generated style wrap contour mode attribute properties | 99 |
| Generated style writing mode attribute properties | 104 |
| Generated table display member mode attribute properties | 103 |
| Generated table layout mode attribute properties | 103 |
| Generated table member type attribute properties | 102 |
| Generated table grouped-by attribute properties | 102 |
| Generated table sort mode attribute properties | 103 |
| Generated table condition source attribute properties | 103 |
| Generated table function attribute properties | 109 |
| Generated database rule attribute properties | 206 |
| Generated database is-nullable attribute properties | 103 |
| Generated database data source setting type attribute properties | 102 |
| Generated animation color interpolation attribute properties | 102 |
| Generated animation color interpolation direction attribute properties | 102 |
| Generated draw nohref attribute properties | 102 |
| Generated presentation preset class attribute properties | 103 |
| Generated number transliteration style attribute properties | 105 |
| Generated style script type attribute properties | 111 |
| Generated style text emphasize attribute properties | 111 |
| Generated draw stroke linejoin attribute properties | 99 |
| Generated SVG stroke linecap attribute properties | 99 |
| Generated FO keep-together attribute properties | 99 |
| Generated FO wrap option attribute properties | 100 |
| Generated 3D projection attribute properties | 100 |
| Generated 3D shade mode attribute properties | 100 |
| Generated SVG fill rule attribute properties | 109 |
| Generated table border model attribute properties | 99 |
| Generated text label-followed-by attribute properties | 107 |
| Generated text list level position mode attribute properties | 106 |
| Generated text index scope attribute properties | 104 |
| Generated text table type attribute properties | 103 |
| Generated text anchor type attribute properties | 102 |
| Generated text note class attribute properties | 101 |
| Generated text select page attribute properties | 100 |
| Generated text reference format attribute properties | 100 |
| Generated text start numbering at attribute properties | 100 |
| Generated text footnotes position attribute properties | 100 |
| Generated text caption sequence format attribute properties | 100 |
| Generated text number position attribute properties | 99 |
| Generated text placeholder type attribute properties | 99 |
| Generated text animation attribute properties | 99 |
| Generated text animation direction attribute properties | 99 |
| Generated text kind attribute properties | 99 |
| Generated line style attribute properties | 534 |
| Generated line type attribute properties | 433 |
| Generated line width attribute properties | 433 |
| Generated line mode attribute properties | 333 |
| Generated font style attribute properties | 433 |
| Generated font variant attribute properties | 211 |
| Generated font weight attribute properties | 433 |
| Generated font family generic attribute properties | 335 |
| Generated font pitch attribute properties | 335 |
| Generated font relief attribute properties | 111 |
| Generated font stretch attribute properties | 100 |
| Generated style line break attribute properties | 98 |
| Generated style repeat attribute properties | 111 |
| Generated style direction attribute properties | 99 |
| Generated form orientation attribute properties | 99 |
| Generated table direction attribute properties | 100 |
| Generated table orientation attribute properties | 104 |
| Generated XML name attribute properties | 1000 |
| Generated style family attribute properties | 50 |
| Generated ODF version attribute properties | 50 |
| Generated media type attribute properties | 100 |

這些數字不是產品宣稱，而是退化保護。若 schema generator 改版導致數字下降，必須在同一變更說明原因，並更新本文件。

## ODFDOM parity gaps

- 已有所有 typed wrapper 共用的泛型 child facade，且 generated wrapper 已產生依 schema child element relation 展開的專屬 child collection property；尚未產生帶有更高階語意排序 / choice group 的 content model facade。
- Attribute property 目前已有基底 typed helper，且 generated artifact 已包含 integer、boolean、decimal、date/dateTime、time、duration、length/percent、borderWidths、bounded percent、angle、foKeepTogether/foWrapOption/foTextTransform/foTextAlign、dr3dProjection/dr3dShadeMode、svgFillRule/svgStrokeLineCap、tableBorderModel、textLabelFollowedBy/textListLevelPositionMode/textIndexScope/textTableType/textAnchorType/textNoteClass/textSelectPage/textReferenceFormat/textStartNumberingAt/textFootnotesPosition/textCaptionSequenceFormat/textNumberPosition/textPlaceholderType/textAnimation/textAnimationDirection/textKind、drawFill/drawFillImageRefPoint/drawColorMode/drawStrokeLineJoin、lineStyle/lineType/lineWidth/lineMode、fontFamilyGeneric/fontPitch/fontRelief/fontStretch/fontStyle/fontVariant/fontWeight、styleLineBreak/styleRepeat/styleDirection/styleWritingMode/styleTextRotationScale/styleTextCombine/styleVerticalAlign/styleVerticalPos/styleVerticalRel/styleHorizontalPos/styleHorizontalRel/styleWrap/styleRunThrough/styleWrapContourMode/styleScriptType/styleTextEmphasize、formOrientation、tableDirection/tableOrientation/tableDisplayMemberMode/tableLayoutMode、databaseRule、xLinkType/xLinkShow/xLinkActuate、numberStyle/numberTransliterationStyle、tableOrder/tableType、presentationEffect/presentationSpeed/presentationAction/presentationTransitionType/presentationTransitionStyle/presentationPresetClass、style name/list、color、IRI reference、cell address/range address、cell range address list、vector3D、point3D、points、language/country/script tokens、namespacedToken、character、textEncoding、targetFrameName、XML name、style family、ODF version 與 media type 強型別 property；其他 ODF datatype 仍需補齊。
- Schema token attribute 目前已補 `number:calendar`、表格成員 / 群組 / 排序 / 條件來源 / 函式、資料庫 nullable / datasource setting、動畫色彩插值與 `draw:nohref`；開放字串或複合 grammar 的屬性仍保守維持 `string?`。
- schema-to-wrapper coverage report 已可由 API / CLI 產生，並包含 schema child relation coverage；release pipeline 會固定保存 artifact。
- 已有 repo 內 ODFDOM 風格 sample usage parity tests，涵蓋 text、spreadsheet 與 draw image frame traversal；尚未對外部 ODF Toolkit / ODFDOM 官方 sample corpus 做逐項 API parity。
- High-level facade 仍由 Text / Spreadsheet / Presentation / Drawing 等文件模型承接，不直接從 generated DOM 推導。

## 完成條件

Typed DOM parity 要升為 `complete`，至少需要：

- 產生 machine-readable coverage report，列出 schema element / attribute 對應 wrapper 與 schema child element relation。
- 所有 ODF 1.4 schema element 可由 factory 建立，並能 parse / serialize round-trip。
- typed child facade 可支援 ODFDOM 風格的建立、插入、列舉與 round-trip user story。
- generated wrapper 提供 schema-specific child collection property，並由 coverage guard 防止數量退化。
- 常用 datatype attribute 有強型別存取或明確保留為 string 的理由。
- 與 ODFDOM sample usage 對照的 user story tests，至少涵蓋文字、試算表與圖片 frame traversal；外部官方 sample corpus 仍需擴充。
