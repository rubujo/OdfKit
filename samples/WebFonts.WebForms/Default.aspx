<%@ Page Language="C#" %>
<!doctype html>
<html lang="zh-Hant-TW">
<head runat="server">
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <%= OdfKit.WebFonts.Hosting.SystemWeb.OdfWebFontHtml.StylesheetLink() %>
  <link rel="stylesheet" href="webfont-sample.css?v=2" />
  <title>OdfKit WebFonts Web Forms Sample</title>
</head>
<body data-international-ready="pending">
  <main>
    <h1>系統字型優先的全字庫 Plus 動態 WebFont</h1>
    <p>一般文字與系統已有的 Ext-B 維持系統字型；只有缺字才載入全字庫 Plus。</p>

    <div class="controls">
      <label for="sidecarEnabled">
        <input id="sidecarEnabled" type="checkbox" checked />
        使用 Sidecar（取消後使用 managed WOFF／TTF）
      </label>
      <label for="formatSelect">輸出格式</label>
      <select id="formatSelect">
        <option value="Woff2" selected>WOFF2</option>
        <option value="Woff">WOFF</option>
        <option value="TrueType">TrueType／TTF</option>
      </select>
      <label for="fontSelect">缺字字型</label>
      <select id="fontSelect">
        <option value="cns-sung-plus" selected>全字庫宋體 Plus</option>
        <option value="cns-kai-plus">全字庫楷體 Plus</option>
      </select>
    </div>

    <label for="rareInput">造字與難字即時輸入框</label>
    <textarea id="rareInput" rows="7">【指定 CNS 造字】U+FFAE0：&#xFFAE0;
【系統字型覆蓋】一般文字 ABC 一二三；Ext-B：𠀀𠆩𪚥。
【自由輸入】可在此貼入需要驗證的完整內容。</textarea>

    <p id="status" role="status">正在準備動態 WebFont…</p>
    <section id="previewBox" class="preview font-cns-sung-plus" aria-live="polite"></section>
  </main>
  <script src="webfont-autosubset.js?v=16"></script>
  <script src="webfont-sample.js?v=2"></script>
</body>
</html>
