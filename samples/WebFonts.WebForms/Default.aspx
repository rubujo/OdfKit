<%@ Page Language="C#" %>
<!doctype html>
<html lang="zh-Hant-TW">
<head runat="server">
  <meta charset="utf-8" />
  <%= OdfKit.WebFonts.Hosting.SystemWeb.OdfWebFontHtml.StylesheetLink() %>
  <title>OdfKit WebFonts Web Forms Sample</title>
</head>
<body>
  <h1>多國罕用字</h1>
  <p>𪚥 𩙡 𦚡 𨏿 𠆩 𡘙 𡌂 𠀀一二三丨ㄩ幹</p>
  <script src="webfont-autosubset.js"
          data-odf-auto
          data-odf-font-source-id="cns-ext-b"
          data-odf-minimum="0x20000"
          data-odf-maximum="0x2FFFF"></script>
</body>
</html>
