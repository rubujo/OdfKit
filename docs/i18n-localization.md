# OdfKit i18n 與在地化

本文件說明 OdfKit 目前的 i18n 與在地化機制，包含訊息來源、語系選擇、
回退規則與已支援語言。

## 1. 機制概觀

OdfKit 透過 `OdfKit.Compliance.OdfLocalizer` 統一管理錯誤訊息、警告訊息與
部分合規建議文字。程式庫內拋出的可在地化訊息，會以訊息鍵值搭配語系字典
解析，而不是在程式碼中直接寫死文字。

## 2. 語系選擇與回退

`OdfLocalizer` 解析訊息時的順序如下：

1. 呼叫時明確傳入的 `CultureInfo`
2. `OdfLocalizer.DefaultCulture`
3. `CultureInfo.CurrentUICulture`
4. 英文預設字典 `en`
5. 若仍找不到，回傳原始訊息鍵值

## 3. 已支援語言

目前已註冊的語言如下：

| 語系代碼 | 說明 |
|----------|------|
| `en` | 英文 |
| `zh-TW` | 正體中文（臺灣） |
| `de` | 德文 |
| `fr` | 法文 |
| `nl` | 荷蘭文 |
| `nb` | 挪威文 Bokmål |
| `pt` | 葡萄牙文 |
| `it` | 義大利文 |
| `sk` | 斯洛伐克文 |
| `da` | 丹麥文 |
| `ms` | 馬來文 |
| `ko` | 韓文 |

完整訊息覆蓋（每個訊息鍵值皆有對應翻譯）僅保證於 `en` 與 `zh-TW`。新增訊息鍵值時，
慣例上僅同步補上 `en`／`zh-TW` 翻譯，其餘 10 種語言暫時留空，依第 2 節的回退規則
退回英文，待後續陸續補齊。

## 4. 使用方式

### 直接取得訊息

```csharp
using OdfKit.Compliance;

string message = OdfLocalizer.GetMessage("ODF0001");
```

### 指定語系

```csharp
using System.Globalization;
using OdfKit.Compliance;

string message = OdfLocalizer.GetMessage(
    "ODF0001",
    new CultureInfo("zh-TW"));
```

### 設定全域預設語系

```csharp
using System.Globalization;
using OdfKit.Compliance;

OdfLocalizer.DefaultCulture = new CultureInfo("zh-TW");
```

## 5. 適用範圍

- 合規檢查與驗證相關訊息
- 可在地化的例外訊息與警告訊息
- 部分建議修復文字

## 6. 相關原始碼

| 檔案 | 用途 |
|------|------|
| `OdfKit/Compliance/OdfLocalizer.cs` | 訊息解析、格式化與文化回退 |
| `OdfKit/Compliance/OdfLocalizer.Languages.cs` | 語言字典註冊與各語系內容 |
| `OdfKit/Compliance/OdfLocalizer.Exceptions.cs` | 訊息鍵值與例外文字 |

## 7. 相關文件

- [ODF Profile 來源](odf-profile-sources.md)
- [ODF 格式支援矩陣](odf-format-support.md)
- [快速開始](getting-started.md)
