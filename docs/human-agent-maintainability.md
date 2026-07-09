# 人機協作可維護性（非為拆而拆）

本文件定義 OdfKit 在 **人類維護** 與 **Agent 輔助維護** 之間的平衡準則。  
**禁止**把「行數變少／檔案變多」當成成功指標。

## 目標

| 角色 | 需要什麼 |
|------|----------|
| **人類** | 能快速找到領域邊界、審 PR、理解變更影響面 |
| **Agent** | 有清楚的檔名／partial 邊界、規範可執行、避免一次改到整包 god 檔 |

兩者共同目標是 **降低認知負擔與誤改風險**，不是滿足機械切檔門檻。

## 何時可以拆（允許）

同時滿足才拆：

1. **領域邊界清楚**（生命週期、I/O、加密、序列化、功能區），可用一句話命名。  
2. **人類之後會獨立改這塊**，或 Agent 任務可自然限制在該檔。  
3. 拆完後 **公開 API 不變**，或 API 變更有 PublicAPI 基線與文件。  
4. 不是為了通過 `List-LargeCsFiles`／行數門檻。

範例（已做、合理）：`OdfStreamingMailMerge.Segments`、`OdfPackageArchiveWriter.FlatXml`、`TableTableElement.Sparse`。

## 何時不要拆（禁止）

1. 只因「超過 N 行」。  
2. 把同一方法拆到多檔卻無邊界註解。  
3. 產生弱 partial（&lt; ~90 行的 Helpers／Candidates）。  
4. 為了讓 Agent「一次改比較少 token」而切碎語意。  
5. 重跑 `eng/historical-refactor/Split-*`。

## 對 Agent 的操作契約

1. 改功能前先讀 [`architecture-collaborators.md`](architecture-collaborators.md) 與現有 partial 檔名。  
2. **優先改既有邊界檔**，不要新建 partial。  
3. 若必須新建：在 PR／提交說明寫清 **領域理由**（給人類審），不是「檔案太大」。  
4. 診斷用 `Analyze-PartialSplits.ps1`；**MERGE/REVIEW 不應成為為拆而拆的 KPI**。  
5. 生成碼（`DOM/Generated`、schema provider）**不可手改**；改產生器。

## 與 RS0026／RS0027 的關係

可選參數多載收斂是 **API 形狀紀律**，與檔案拆分無關：

- 生成 DOM：產生器輸出無 optional prefix 多載。  
- 手寫：同名多載至多一個帶可選參數，且應為參數最多者；本專案偏好 **全改明確多載轉呼叫**。  
- 見 [`public-api-optional-parameters.md`](public-api-optional-parameters.md)。

## 相關

- [maintainability.md](maintainability.md)  
- [architecture-collaborators.md](architecture-collaborators.md)  
- [AGENTS.md](../AGENTS.md) §C2  
