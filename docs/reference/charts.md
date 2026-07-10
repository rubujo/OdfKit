# 圖表工作流

`OdfChartDocument` 是獨立圖表與嵌入式圖表共用的編輯模型。ODS 內可用
`InsertChartFromRange` 建立圖表，或以 `GetEmbeddedChartDocument` 取得既有嵌入圖表後繼續編輯。

支援的任務導向入口包含常用 preset、序列、軸、資料標籤、marker、number format、bubble、stock、
3D、wall 與 floor。`OdfEmbeddedChartOptions` 可在插入時設定標題、preset、marker、軸格式與
`OdfChart3DOptions`；後續變更仍透過同一個 `OdfChartDocument` 進行。

圖表 API 編輯 ODF 圖表結構與樣式，不執行 Office 級排版或試算表重算。資料樣式名稱會寫入
chart style，實際顯示取決於文件中的 number style、字型與開啟端。Bubble、stock、3D 與
wall／floor 在非 LibreOffice profile 下可能呈現不同，應搭配實務相容性檢查器。

範例見 [Cookbook：建立進階實務圖表](../cookbook.md#建立進階實務圖表)。
