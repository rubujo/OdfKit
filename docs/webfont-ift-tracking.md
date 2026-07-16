# WebFont IFT 標準追蹤與相容性閘門

> 基準日期：2026-07-16
>
> 狀態：追蹤中；OdfKit 尚未實作或宣稱支援 Incremental Font Transfer。

## 標準狀態

W3C 的 [Incremental Font Transfer](https://www.w3.org/TR/IFT/) 目前是 Candidate
Recommendation Draft。W3C
[出版歷史](https://www.w3.org/standards/history/IFT/)顯示 2025-07-31 曾發布 Candidate
Recommendation Snapshot，2025-11-18 發布後續 Candidate Recommendation Draft。這仍是可變動的候選
標準，不是已完成互通報告的 W3C Recommendation。

IFT 同時定義 table-keyed 與 glyph-keyed patch。保留 glyph ID 只協助 glyph-keyed patch 的穩定
glyph-to-ID 指派；它不會自動產生 IFT／IFTX table、patch map、compatibility ID、patch 檔案、
feature closure 或伺服器協定。因此文件不得把目前的 retain-gids 策略描述為「低成本即可支援
IFT」或「已與 IFT 相容」。

## 目前已實證的相容性基礎

- TrueType 子集保留來源 `maxp.numGlyphs`，未使用的 outline 以空 glyph 保留位置。
- format matrix 對每個成功的真實來源、TTF／WOFF／WOFF2 輸出，比較要求 scalar 的來源與輸出
  glyph ID，並確認 glyph count 未改變。
- manifest、CSS 與資產仍使用內容定址；這是日後 patch cache 可重用的基礎，但不是 IFT wire
  compatibility 的證據。

## 升級閘門

只有下列項目全部具可重現證據後，才能新增 experimental IFT 輸出：

1. 鎖定精確 IFT Candidate Recommendation 版本及變更紀錄。
2. 以 clean-room C# 實作 IFT／IFTX table、patch map、compatibility ID 與至少一種 patch format。
3. 驗證 glyph、layout feature、variation design space 與複合 glyph closure。
4. 以獨立 validator 及實際支援 IFT 的瀏覽器完成 base font、增量 patch、cache reuse 與錯誤注入。
5. 對 patch URL 的內容推論、跨 origin cache、拒絕服務與租戶隔離完成安全審查。
6. 比較 IFT、固定 Unicode slice 與精確動態補洞在真實 CNS corpus 上的傳輸量、CPU、峰值記憶體
   與 cache hit ratio；沒有實測優勢不得改成預設。

在這些閘門完成前，穩定 Unicode bucket 與受控動態補洞是可部署路徑，IFT 僅維持標準追蹤。
