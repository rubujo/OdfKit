using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// Represents a presentation slide.
/// 表示簡報投影片（Slide）的類別。
/// </summary>
/// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
/// <param name="doc">The owning presentation document instance. / 所屬的簡報文件執行個體。</param>
public partial class OdfSlide(OdfNode node, PresentationDocument doc)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    internal OdfNode Node { get; } = node;

    /// <summary>
    /// Gets the owning presentation document.
    /// 取得所屬的簡報文件。
    /// </summary>
    public PresentationDocument Document { get; } = doc;

    /// <summary>
    /// Gets or sets the slide name.
    /// 取得或設定投影片名稱。
    /// </summary>
    public string Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("name", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// Gets or sets the master page name used by the slide.
    /// 取得或設定投影片使用的母片名稱。
    /// </summary>
    public string MasterPageName
    {
        get => Node.GetAttribute("master-page-name", OdfNamespaces.Draw) ?? string.Empty;
        set => Node.SetAttribute("master-page-name", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// Gets or sets the page layout name used by the slide.
    /// 取得或設定投影片使用的版面配置名稱。
    /// </summary>
    public string? PresentationPageLayoutName
    {
        get => Node.GetAttribute("presentation-page-layout-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("presentation-page-layout-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("presentation-page-layout-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// Gets or sets the slide background color, such as <c>#FFFFFF</c>.
    /// 取得或設定投影片背景色（例如 <c>#FFFFFF</c>）。
    /// </summary>
    public string? BackgroundColor
    {
        get
        {
            string? styleName = Node.GetAttribute("style-name", OdfNamespaces.Draw);
            return string.IsNullOrWhiteSpace(styleName)
                ? null
                : Document.StyleEngine.GetStyleProperty(styleName!, "fill-color", OdfNamespaces.Draw, "drawing-page");
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Document.StyleEngine.SetLocalStyleProperty(Node, "drawing-page", "drawing-page-properties", "fill", OdfNamespaces.Draw, null, "draw");
                Document.StyleEngine.SetLocalStyleProperty(Node, "drawing-page", "drawing-page-properties", "fill-color", OdfNamespaces.Draw, null, "draw");
                return;
            }

            Document.StyleEngine.SetLocalStyleProperty(Node, "drawing-page", "drawing-page-properties", "fill", OdfNamespaces.Draw, "solid", "draw");
            Document.StyleEngine.SetLocalStyleProperty(Node, "drawing-page", "drawing-page-properties", "fill-color", OdfNamespaces.Draw, value, "draw");
        }
    }

    /// <summary>
    /// Gets or sets the style name used by the slide header.
    /// 取得或設定投影片頁首使用的樣式名稱。
    /// </summary>
    public string? UseHeaderName
    {
        get => Node.GetAttribute("use-header-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("use-header-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("use-header-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// Gets or sets the style name used by the slide footer.
    /// 取得或設定投影片頁尾使用的樣式名稱。
    /// </summary>
    public string? UseFooterName
    {
        get => Node.GetAttribute("use-footer-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("use-footer-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("use-footer-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// Gets or sets the style name used by the slide date and time field.
    /// 取得或設定投影片日期與時間使用的樣式名稱。
    /// </summary>
    public string? UseDateTimeName
    {
        get => Node.GetAttribute("use-date-time-name", OdfNamespaces.Presentation);
        set
        {
            if (value is null)
            {
                Node.RemoveAttribute("use-date-time-name", OdfNamespaces.Presentation);
            }
            else
            {
                Node.SetAttribute("use-date-time-name", OdfNamespaces.Presentation, value, "presentation");
            }
        }
    }

    /// <summary>
    /// Gets the slide notes page.
    /// 取得投影片的備忘錄頁面（Notes Page）。
    /// </summary>
    public OdfNotesPage SpeakerNotesPage
    {
        get
        {
            var notesNode = Node.FindChildElement("notes", OdfNamespaces.Presentation);
            if (notesNode is null)
            {
                notesNode = new OdfNode(OdfNodeType.Element, "notes", OdfNamespaces.Presentation, "presentation");
                Node.AppendChild(notesNode);
            }
            return new OdfNotesPage(notesNode, this);
        }
    }

    /// <summary>
    /// Finds the existing slide notes page without creating one.
    /// 尋找現有投影片備忘錄頁面，且不會建立新頁面。
    /// </summary>
    /// <returns>The notes page, or <see langword="null"/> if none exists. / 備忘錄頁面；若不存在則為 <see langword="null"/>。</returns>
    public OdfNotesPage? FindSpeakerNotesPage()
    {
        OdfNode? notesNode = Node.FindChildElement("notes", OdfNamespaces.Presentation);
        return notesNode is null ? null : new OdfNotesPage(notesNode, this);
    }

    /// <summary>
    /// Removes the slide notes page.
    /// 移除投影片備忘錄頁面。
    /// </summary>
    /// <returns><see langword="true"/> if the page was removed; otherwise <see langword="false"/>. / 若已移除頁面則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveSpeakerNotesPage()
    {
        OdfNode? notesNode = Node.FindChildElement("notes", OdfNamespaces.Presentation);
        return notesNode is not null && Node.RemoveChild(notesNode);
    }

    /// <summary>
    /// Clears speaker-note paragraphs while preserving other notes-page content.
    /// 清除主講人備忘錄段落，同時保留其他備忘錄頁面內容。
    /// </summary>
    /// <returns>The number of removed paragraphs. / 已移除的段落數量。</returns>
    public int ClearSpeakerNotes() => FindSpeakerNotesPage()?.ClearSpeakerNotes() ?? 0;

    /// <summary>
    /// Gets or sets the slide speaker notes text.
    /// 取得或設定投影片備忘錄文字。
    /// </summary>
    public string SpeakerNotes
    {
        get => SpeakerNotesPage.SpeakerNotesText;
        set => SpeakerNotesPage.SpeakerNotesText = value;
    }

    /// <summary>
    /// Gets the paragraph text of the slide speaker notes.
    /// 取得投影片備忘錄的段落文字。
    /// </summary>
    public IReadOnlyList<string> SpeakerNoteParagraphs => SpeakerNotesPage.SpeakerNoteParagraphs;

    /// <summary>
    /// Sets slide speaker notes as multiple paragraphs.
    /// 以多段落形式設定投影片備忘錄文字。
    /// </summary>
    /// <param name="paragraphs">The paragraph text collection. / 段落文字集合。</param>
    /// <returns>The current slide. / 目前投影片。</returns>
    public OdfSlide SetSpeakerNotes(IEnumerable<string> paragraphs)
    {
        SpeakerNotesPage.SetSpeakerNotes(paragraphs);
        return this;
    }

    /// <summary>
    /// Gets the animation root node.
    /// 取得動畫根節點。
    /// </summary>
    public OdfAnimationNode AnimationRoot
    {
        get
        {
            const string AnimNs = "urn:oasis:names:tc:opendocument:xmlns:animation:1.0";

            OdfNode? timingRoot = null;
            foreach (var child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element && child.LocalName is "par" && child.NamespaceUri is AnimNs &&
                    child.GetAttribute("node-type", OdfNamespaces.Presentation) is "timing-root")
                {
                    timingRoot = child;
                    break;
                }
            }
            if (timingRoot is null)
            {
                timingRoot = new OdfNode(OdfNodeType.Element, "par", AnimNs, "anim");
                timingRoot.SetAttribute("node-type", OdfNamespaces.Presentation, "timing-root", "presentation");
                Node.AppendChild(timingRoot);
            }

            OdfNode? mainSeq = null;
            foreach (var child in timingRoot.Children)
            {
                if (child.NodeType is OdfNodeType.Element && child.LocalName is "seq" && child.NamespaceUri is AnimNs)
                {
                    string? nodeType = child.GetAttribute("node-type", OdfNamespaces.Presentation);
                    if (nodeType is "main-sequence")
                    {
                        mainSeq = child;
                        break;
                    }
                }
            }
            if (mainSeq is null)
            {
                mainSeq = new OdfNode(OdfNodeType.Element, "seq", AnimNs, "anim");
                mainSeq.SetAttribute("node-type", OdfNamespaces.Presentation, "main-sequence", "presentation");
                timingRoot.AppendChild(mainSeq);
            }
            return new OdfAnimationNode(mainSeq);
        }
    }

    /// <summary>
    /// Gets the summary list of all placeholders in the slide.
    /// 取得投影片中所有預留位置的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPlaceholderInfo> GetPlaceholderInfos() =>
        OdfSlidePlaceholderReadEngine.GetPlaceholders(this);

    /// <summary>
    /// Gets the read-only list of all placeholders in the slide.
    /// 取得投影片中所有預留位置的唯讀清單。
    /// </summary>
    public IReadOnlyList<OdfPlaceholder> Placeholders
    {
        get
        {
            List<OdfPlaceholder> list = [];
            foreach (var child in Node.Children)
            {
                if (child.NodeType is OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw)
                {
                    string? ph = child.GetAttribute("placeholder", OdfNamespaces.Presentation);
                    if (ph is "true")
                    {
                        list.Add(new OdfPlaceholder(child, this));
                    }
                }
            }
            return list.AsReadOnly();
        }
    }

    /// <summary>
    /// Finds the first placeholder of the requested type.
    /// 尋找第一個指定類型的預留位置。
    /// </summary>
    /// <param name="type">The placeholder type. / 預留位置類型。</param>
    /// <returns>The matching placeholder, or <see langword="null"/>. / 相符的預留位置；若不存在則為 <see langword="null"/>。</returns>
    public OdfPlaceholder? FindPlaceholder(OdfPlaceholderType type)
    {
        foreach (OdfPlaceholder placeholder in Placeholders)
        {
            if (placeholder.PlaceholderType == type)
                return placeholder;
        }

        return null;
    }

    /// <summary>
    /// Removes the specified placeholder and dependent animation effects.
    /// 移除指定預留位置及其相依動畫效果。
    /// </summary>
    /// <param name="placeholder">The placeholder to remove. / 要移除的預留位置。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemovePlaceholder(OdfPlaceholder placeholder)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(placeholder, nameof(placeholder));
        if (!ReferenceEquals(placeholder.Slide, this) || placeholder.Node.Parent != Node)
            return false;

        string? id = GetIdentifier(placeholder.Node);
        Node.RemoveChild(placeholder.Node);
        if (!string.IsNullOrEmpty(id))
            RemoveDependentNodes(Node, id!);
        return true;
    }

    /// <summary>
    /// Removes all placeholders and their dependent animation effects.
    /// 移除所有預留位置及其相依動畫效果。
    /// </summary>
    /// <returns>The number removed. / 移除數量。</returns>
    public int ClearPlaceholders()
    {
        List<OdfPlaceholder> placeholders = [.. Placeholders];
        int removed = 0;
        foreach (OdfPlaceholder placeholder in placeholders)
        {
            if (RemovePlaceholder(placeholder))
                removed++;
        }
        return removed;
    }

    /// <summary>
    /// Gets the text boxes on the slide.
    /// 取得投影片上的文字方塊清單。
    /// </summary>
    public IReadOnlyList<OdfTextBox> TextBoxes => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            ContainsDescendant(node, "text-box", OdfNamespaces.Draw),
        node => new OdfTextBox(node, this));

    /// <summary>
    /// Gets the pictures on the slide.
    /// 取得投影片上的圖片清單。
    /// </summary>
    public IReadOnlyList<OdfPicture> Pictures => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            ContainsDescendant(node, "image", OdfNamespaces.Draw),
        node => new OdfPicture(node, this));

    /// <summary>
    /// Gets the media objects on the slide.
    /// 取得投影片上的媒體物件清單。
    /// </summary>
    public IReadOnlyList<OdfMediaObject> MediaObjects => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            ContainsDescendant(node, "plugin", OdfNamespaces.Draw),
        node => new OdfMediaObject(node, this));

    /// <summary>
    /// Gets the embedded tables on the slide.
    /// 取得投影片上的嵌入表格清單。
    /// </summary>
    public IReadOnlyList<OdfEmbeddedTable> EmbeddedTables => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            ContainsDescendant(node, "table", OdfNamespaces.Table),
        node => new OdfEmbeddedTable(FindDescendant(node, "table", OdfNamespaces.Table)!, Document, node));

    /// <summary>
    /// Adds an embedded table to the slide.
    /// 新增嵌入表格至投影片。
    /// </summary>
    /// <param name="rows">The row count. / 列數。</param>
    /// <param name="columns">The column count. / 欄數。</param>
    /// <param name="x">The X-axis coordinate. / X 軸座標。</param>
    /// <param name="y">The Y-axis coordinate. / Y 軸座標。</param>
    /// <param name="width">The width. / 寬度。</param>
    /// <param name="height">The height. / 高度。</param>
    /// <returns>The newly created embedded table. / 新建立的嵌入表格。</returns>
    public OdfEmbeddedTable AddTable(
        int rows,
        int columns,
        OdfKit.Styles.OdfLength x,
        OdfKit.Styles.OdfLength y,
        OdfKit.Styles.OdfLength width,
        OdfKit.Styles.OdfLength height)
    {
        OdfNode frame = CreateDrawingFrame(x, y, width, height);
        AddDrawingObjectNode(frame);
        return new OdfShape(frame, this).AddEmbeddedTable(rows, columns);
    }

    /// <summary>
    /// Finds an embedded table by its containing drawing object identifier.
    /// 依外層繪圖物件識別碼尋找嵌入表格。
    /// </summary>
    /// <param name="id">The exact drawing identifier. / 精確的繪圖識別碼。</param>
    /// <returns>The matching table, or <see langword="null"/>. / 相符的表格；若不存在則為 <see langword="null"/>。</returns>
    public OdfEmbeddedTable? FindEmbeddedTable(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        foreach (OdfEmbeddedTable table in EmbeddedTables)
        {
            if (string.Equals(table.Id, id, StringComparison.Ordinal))
                return table;
        }

        return null;
    }

    /// <summary>
    /// Removes an embedded table and its containing drawing object.
    /// 移除嵌入表格及其外層繪圖物件。
    /// </summary>
    /// <param name="id">The exact drawing identifier. / 精確的繪圖識別碼。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveEmbeddedTable(string id) =>
        FindEmbeddedTable(id) is not null && RemoveDrawingObject(id);

    /// <summary>
    /// Removes all embedded tables and their containing drawing objects.
    /// 移除所有嵌入表格及其外層繪圖物件。
    /// </summary>
    /// <returns>The number of removed embedded tables. / 已移除的嵌入表格數量。</returns>
    public int ClearEmbeddedTables()
    {
        List<OdfEmbeddedTable> tables = [.. EmbeddedTables];
        int removed = 0;
        foreach (OdfEmbeddedTable table in tables)
        {
            if (RemoveEmbeddedTable(table.Id))
                removed++;
        }

        return removed;
    }

    /// <summary>
    /// Finds a media object by its drawing identifier.
    /// 依繪圖識別碼尋找媒體物件。
    /// </summary>
    /// <param name="id">The exact drawing identifier. / 精確的繪圖識別碼。</param>
    /// <returns>The matching media object, or <see langword="null"/>. / 相符的媒體物件；若不存在則為 <see langword="null"/>。</returns>
    public OdfMediaObject? FindMediaObject(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        foreach (OdfMediaObject media in MediaObjects)
        {
            if (string.Equals(media.Id, id, StringComparison.Ordinal))
                return media;
        }

        return null;
    }

    /// <summary>
    /// Removes a media object and its dependent animation effects.
    /// 移除媒體物件及其相依動畫效果。
    /// </summary>
    /// <param name="id">The exact drawing identifier. / 精確的繪圖識別碼。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveMediaObject(string id) =>
        FindMediaObject(id) is not null && RemoveDrawingObject(id);

    /// <summary>
    /// Removes all media objects and their dependent animation effects.
    /// 移除所有媒體物件及其相依動畫效果。
    /// </summary>
    /// <returns>The number of removed media objects. / 已移除的媒體物件數量。</returns>
    public int ClearMediaObjects()
    {
        List<OdfMediaObject> mediaObjects = [.. MediaObjects];
        int removed = 0;
        foreach (OdfMediaObject media in mediaObjects)
        {
            if (RemoveMediaObject(media.Id))
                removed++;
        }

        return removed;
    }

    /// <summary>
    /// Gets the general shapes on the slide.
    /// 取得投影片上的一般圖形清單。
    /// </summary>
    public IReadOnlyList<OdfShape> Shapes => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw &&
            node.LocalName is "rect" or "ellipse" or "custom-shape" or "line" or "connector" or "polyline",
        node => new OdfShape(node, this));

    /// <summary>
    /// Gets the connectors on the slide.
    /// 取得投影片上的連接線清單。
    /// </summary>
    public IReadOnlyList<OdfShape> Connectors => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw && node.LocalName == "connector",
        node => new OdfShape(node, this));

    /// <summary>
    /// Gets the drawing groups on the slide.
    /// 取得投影片上的繪圖群組清單。
    /// </summary>
    public IReadOnlyList<OdfKit.Drawing.OdfDrawGroup> Groups => FindDrawingObjects(
        node => node.NamespaceUri == OdfNamespaces.Draw && node.LocalName == "g",
        node => new OdfKit.Drawing.OdfDrawGroup(node, Document));

    /// <summary>
    /// Adds a drawing group to the slide.
    /// 新增繪圖群組至投影片。
    /// </summary>
    /// <param name="name">The optional group name. / 選用的群組名稱。</param>
    /// <returns>The newly created group. / 新建立的群組。</returns>
    public OdfKit.Drawing.OdfDrawGroup AddGroup(string? name)
    {
        var groupNode = new OdfNode(OdfNodeType.Element, "g", OdfNamespaces.Draw, "draw");
        string id = global::OdfKit.Internal.OdfStringHelper.CreatePrefixedGuid("grp_");
        groupNode.SetAttribute("id", OdfNamespaces.Draw, id, "draw");
        groupNode.SetAttribute("id", OdfNamespaces.Xml, id, "xml");
        if (!string.IsNullOrEmpty(name))
            groupNode.SetAttribute("name", OdfNamespaces.Draw, name!, "draw");
        AddDrawingObjectNode(groupNode);
        return new OdfKit.Drawing.OdfDrawGroup(groupNode, Document);
    }

    /// <summary>
    /// Adds an unnamed drawing group to the slide.
    /// 新增未命名的繪圖群組至投影片。
    /// </summary>
    /// <returns>The newly created group. / 新建立的群組。</returns>
    public OdfKit.Drawing.OdfDrawGroup AddGroup() => AddGroup(null);

    /// <summary>
    /// Finds a drawing group by its identifier.
    /// 依識別碼尋找繪圖群組。
    /// </summary>
    /// <param name="id">The exact drawing identifier. / 精確的繪圖識別碼。</param>
    /// <returns>The matching group, or <see langword="null"/>. / 相符的群組；若不存在則為 <see langword="null"/>。</returns>
    public OdfKit.Drawing.OdfDrawGroup? FindGroup(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        foreach (OdfKit.Drawing.OdfDrawGroup group in Groups)
        {
            if (string.Equals(group.Id, id, StringComparison.Ordinal))
                return group;
        }

        return null;
    }

    /// <summary>
    /// Removes a drawing group and its dependent animation effects.
    /// 移除繪圖群組及其相依動畫效果。
    /// </summary>
    /// <param name="id">The exact drawing identifier. / 精確的繪圖識別碼。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveGroup(string id) => FindGroup(id) is not null && RemoveDrawingObject(id);

    /// <summary>
    /// Removes all drawing groups and their dependent animation effects.
    /// 移除所有繪圖群組及其相依動畫效果。
    /// </summary>
    /// <returns>The number of removed groups. / 已移除的群組數量。</returns>
    public int ClearGroups()
    {
        List<OdfKit.Drawing.OdfDrawGroup> groups = [.. Groups];
        int removed = 0;
        foreach (OdfKit.Drawing.OdfDrawGroup group in groups)
        {
            if (RemoveGroup(group.Id))
                removed++;
        }

        return removed;
    }

    /// <summary>
    /// Finds a connector by its drawing identifier.
    /// 依繪圖識別碼尋找連接線。
    /// </summary>
    /// <param name="id">The exact drawing identifier. / 精確的繪圖識別碼。</param>
    /// <returns>The matching connector, or <see langword="null"/>. / 相符的連接線；若不存在則為 <see langword="null"/>。</returns>
    public OdfShape? FindConnector(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        foreach (OdfShape connector in Connectors)
        {
            if (string.Equals(connector.Id, id, StringComparison.Ordinal))
                return connector;
        }

        return null;
    }

    /// <summary>
    /// Removes a connector and its dependent animation effects.
    /// 移除連接線及其相依動畫效果。
    /// </summary>
    /// <param name="id">The exact drawing identifier. / 精確的繪圖識別碼。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveConnector(string id) => FindConnector(id) is not null && RemoveDrawingObject(id);

    /// <summary>
    /// Removes all connectors and their dependent animation effects.
    /// 移除所有連接線及其相依動畫效果。
    /// </summary>
    /// <returns>The number of removed connectors. / 已移除的連接線數量。</returns>
    public int ClearConnectors()
    {
        List<OdfShape> connectors = [.. Connectors];
        int removed = 0;
        foreach (OdfShape connector in connectors)
        {
            if (RemoveConnector(connector.Id))
                removed++;
        }

        return removed;
    }

    /// <summary>
    /// Finds a top-level drawing object by its draw or XML identifier.
    /// 依 draw 或 XML 識別碼尋找最上層繪圖物件。
    /// </summary>
    /// <param name="id">The exact object identifier. / 物件的精確識別碼。</param>
    /// <returns>The matching object, or <see langword="null"/>. / 相符的物件；若不存在則為 <see langword="null"/>。</returns>
    public OdfShape? FindDrawingObject(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        foreach (OdfNode child in Node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.NamespaceUri == OdfNamespaces.Draw &&
                HasIdentifier(child, id))
            {
                return new OdfShape(child, this);
            }
        }

        return null;
    }

    /// <summary>
    /// Removes a top-level drawing object and dependent connectors and animation effects.
    /// 移除最上層繪圖物件，以及依賴該物件的連接線與動畫效果。
    /// </summary>
    /// <param name="id">The exact object identifier. / 物件的精確識別碼。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveDrawingObject(string id)
    {
        OdfShape? shape = FindDrawingObject(id);
        if (shape?.Node.Parent is null)
            return false;

        shape.Node.Parent.RemoveChild(shape.Node);
        RemoveDependentNodes(Node, id);
        return true;
    }

    /// <summary>
    /// Removes all top-level drawing objects and their dependent animation effects while preserving notes and unknown content.
    /// 移除所有最上層繪圖物件與其相依動畫效果，並保留備忘稿與未知內容。
    /// </summary>
    /// <returns>The number of removed top-level drawing objects. / 已移除的最上層繪圖物件數量。</returns>
    public int ClearDrawingObjects()
    {
        List<(OdfNode Node, string? Id)> objects = [];
        foreach (OdfNode child in Node.Children)
        {
            if (child.NodeType is OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw)
            {
                objects.Add((child, GetIdentifier(child)));
            }
        }

        foreach ((OdfNode node, _) in objects)
            Node.RemoveChild(node);
        foreach ((_, string? id) in objects)
        {
            if (!string.IsNullOrEmpty(id))
                RemoveDependentNodes(Node, id!);
        }
        return objects.Count;
    }

    private static bool HasIdentifier(OdfNode node, string id) =>
        string.Equals(node.GetAttribute("id", OdfNamespaces.Draw), id, StringComparison.Ordinal) ||
        string.Equals(node.GetAttribute("id", OdfNamespaces.Xml), id, StringComparison.Ordinal);

    private static string? GetIdentifier(OdfNode node) =>
        node.GetAttribute("id", OdfNamespaces.Draw) ?? node.GetAttribute("id", OdfNamespaces.Xml);

    private static void RemoveDependentNodes(OdfNode root, string id)
    {
        const string smilNs = "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0";
        List<OdfNode> removals = [];
        foreach (OdfNode child in root.Children)
        {
            bool connectorReference = child.NamespaceUri == OdfNamespaces.Draw &&
                child.LocalName == "connector" &&
                (string.Equals(child.GetAttribute("start-shape", OdfNamespaces.Draw), id, StringComparison.Ordinal) ||
                 string.Equals(child.GetAttribute("end-shape", OdfNamespaces.Draw), id, StringComparison.Ordinal));
            bool animationReference = string.Equals(
                child.GetAttribute("targetElement", smilNs), id, StringComparison.Ordinal);
            if (connectorReference || animationReference)
                removals.Add(child);
            else
                RemoveDependentNodes(child, id);
        }

        foreach (OdfNode removal in removals)
            root.RemoveChild(removal);
    }
}
