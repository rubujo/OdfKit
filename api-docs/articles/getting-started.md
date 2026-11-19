---
title: 快速開始 / Getting started
---

# 快速開始 / Getting started

從原始碼建置 OdfKit，或在應用程式中加入 `ProjectReference`。目前主要交付管道為 GitHub 原始碼與隨 `v0.0.1` tag 自動更新的 GitHub Release 交付快照，仍未發佈至 nuget.org。

```powershell
dotnet build
dotnet add YourApp.csproj reference path\to\OdfKit\OdfKit\OdfKit.csproj
```

建立第一份 ODT：

```csharp
using OdfKit.Text;

using TextDocument document = TextDocument.Create();
document.Body.Headings.Add("報告", 1);
document.Body.Paragraphs.Add("這是一份 ODF 文字文件。");
document.Save("report.odt");
```

- [完整快速開始](https://github.com/rubujo/OdfKit/blob/main/docs/getting-started.md)
- [可執行範例](https://github.com/rubujo/OdfKit/tree/main/samples)
- [WebFont 多國罕字套件](../../docs/webfonts.md)
- [NativeAOT 支援與部署邊界](../../docs/nativeaot.md)
