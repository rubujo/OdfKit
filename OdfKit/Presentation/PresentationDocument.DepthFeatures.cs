using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

public partial class PresentationDocument
{
    /// <summary>
    /// Gets all text boxes across all slides.
    /// 取得所有投影片中的文字方塊。
    /// </summary>
    /// <returns>The text boxes in document order. / 依文件順序排列的文字方塊。</returns>
    public IReadOnlyList<OdfTextBox> GetTextBoxes() =>
        Slides.SelectMany(slide => slide.TextBoxes).ToList().AsReadOnly();

    /// <summary>
    /// Gets all pictures across all slides.
    /// 取得所有投影片中的圖片。
    /// </summary>
    /// <returns>The pictures in document order. / 依文件順序排列的圖片。</returns>
    public IReadOnlyList<OdfPicture> GetPictures() =>
        Slides.SelectMany(slide => slide.Pictures).ToList().AsReadOnly();

    /// <summary>
    /// Gets all general shapes across all slides.
    /// 取得所有投影片中的一般圖形。
    /// </summary>
    /// <returns>The shapes in document order. / 依文件順序排列的圖形。</returns>
    public IReadOnlyList<OdfShape> GetShapes() =>
        Slides.SelectMany(slide => slide.Shapes).ToList().AsReadOnly();

    /// <summary>
    /// Replaces text in all presentation text boxes.
    /// 取代所有簡報文字方塊中的文字。
    /// </summary>
    /// <param name="search">The text to search for. / 要搜尋的文字。</param>
    /// <param name="replacement">The replacement text. / 替換文字。</param>
    public override void ReplaceText(string search, string replacement)
    {
        ReplaceTextInTextBoxes(search, replacement);
    }

    /// <summary>
    /// Replaces text in all presentation text boxes and returns the changed text box count.
    /// 取代所有簡報文字方塊中的文字並傳回已變更的文字方塊數量。
    /// </summary>
    /// <param name="search">The text to search for. / 要搜尋的文字。</param>
    /// <param name="replacement">The replacement text. / 替換文字。</param>
    /// <returns>The number of changed text boxes. / 已變更的文字方塊數量。</returns>
    public int ReplaceTextInTextBoxes(string search, string replacement)
    {
        if (search is null)
        {
            throw new ArgumentNullException(nameof(search));
        }

        int changed = 0;
        foreach (OdfTextBox textBox in GetTextBoxes())
        {
            if (ReplaceTextRecursive(textBox.Node, search, replacement ?? string.Empty))
            {
                changed++;
            }
        }

        return changed;
    }

    /// <summary>
    /// Updates presentation pictures by id or name.
    /// 依識別碼或名稱更新簡報圖片。
    /// </summary>
    /// <param name="requests">The picture update requests. / 圖片更新要求。</param>
    /// <returns>The batch update result. / 批次更新結果。</returns>
    public OdfBatchUpdateResult UpdatePictures(IEnumerable<OdfPictureUpdateRequest> requests)
    {
        if (requests is null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        var result = new OdfBatchUpdateResult();
        foreach (OdfPictureUpdateRequest request in requests)
        {
            List<OdfPicture> matches = GetPictures().Where(p => MatchesShapeName(p.Node, request.Name)).ToList();
            if (matches.Count == 0)
            {
                result.MissingNames.Add(request.Name);
                continue;
            }

            if (matches.Count > 1)
            {
                result.AmbiguousNames.Add(request.Name);
                continue;
            }

            if (ApplyPictureUpdate(matches[0].Node, request))
            {
                result.UpdatedNames.Add(request.Name);
                result.UpdatedCount++;
            }
            else
            {
                result.UnchangedNames.Add(request.Name);
            }
        }

        return result;
    }

    /// <summary>
    /// Updates presentation shapes by id or name.
    /// 依識別碼或名稱更新簡報圖形。
    /// </summary>
    /// <param name="requests">The shape update requests. / 圖形更新要求。</param>
    /// <returns>The batch update result. / 批次更新結果。</returns>
    public OdfBatchUpdateResult UpdateShapes(IEnumerable<OdfShapeUpdateRequest> requests)
    {
        if (requests is null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        var result = new OdfBatchUpdateResult();
        IReadOnlyList<OdfShape> shapes = GetShapes();
        foreach (OdfShapeUpdateRequest request in requests)
        {
            List<OdfShape> matches = shapes.Where(shape => MatchesShapeName(shape.Node, request.Name)).ToList();
            if (matches.Count == 0)
            {
                result.MissingNames.Add(request.Name);
                continue;
            }

            if (matches.Count > 1)
            {
                result.AmbiguousNames.Add(request.Name);
                continue;
            }

            if (ApplyShapeUpdate(matches[0].Node, this, request))
            {
                result.UpdatedNames.Add(request.Name);
                result.UpdatedCount++;
            }
            else
            {
                result.UnchangedNames.Add(request.Name);
            }
        }

        return result;
    }

    /// <summary>
    /// Moves a shape to the front of its sibling drawing order.
    /// 將圖形移到同層繪圖順序最前方。
    /// </summary>
    /// <param name="name">The shape id or name. / 圖形識別碼或名稱。</param>
    /// <returns><see langword="true"/> if moved; otherwise <see langword="false"/>. / 若已移動則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool BringToFront(string name) => MoveShapeInDocumentOrder(name, toFront: true);

    /// <summary>
    /// Moves a shape to the back of its sibling drawing order.
    /// 將圖形移到同層繪圖順序最後方。
    /// </summary>
    /// <param name="name">The shape id or name. / 圖形識別碼或名稱。</param>
    /// <returns><see langword="true"/> if moved; otherwise <see langword="false"/>. / 若已移動則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool SendToBack(string name) => MoveShapeInDocumentOrder(name, toFront: false);

    /// <summary>
    /// Moves a shape before another shape in the same sibling drawing order.
    /// 將圖形移到同層繪圖順序中另一個圖形之前。
    /// </summary>
    /// <param name="name">The shape id or name to move. / 要移動的圖形識別碼或名稱。</param>
    /// <param name="referenceName">The reference shape id or name. / 參考圖形識別碼或名稱。</param>
    /// <returns><see langword="true"/> if moved; otherwise <see langword="false"/>. / 若已移動則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool MoveBefore(string name, string referenceName) =>
        MoveShapeRelative(name, referenceName, before: true);

    /// <summary>
    /// Moves a shape after another shape in the same sibling drawing order.
    /// 將圖形移到同層繪圖順序中另一個圖形之後。
    /// </summary>
    /// <param name="name">The shape id or name to move. / 要移動的圖形識別碼或名稱。</param>
    /// <param name="referenceName">The reference shape id or name. / 參考圖形識別碼或名稱。</param>
    /// <returns><see langword="true"/> if moved; otherwise <see langword="false"/>. / 若已移動則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool MoveAfter(string name, string referenceName) =>
        MoveShapeRelative(name, referenceName, before: false);

    internal static bool ApplyPictureUpdate(OdfNode node, OdfPictureUpdateRequest request)
    {
        bool changed = false;
        if (request.X.HasValue)
        {
            node.SetAttribute("x", OdfNamespaces.Svg, request.X.Value.ToString(), "svg");
            changed = true;
        }

        if (request.Y.HasValue)
        {
            node.SetAttribute("y", OdfNamespaces.Svg, request.Y.Value.ToString(), "svg");
            changed = true;
        }

        if (request.Width.HasValue)
        {
            node.SetAttribute("width", OdfNamespaces.Svg, request.Width.Value.ToString(), "svg");
            changed = true;
        }

        if (request.Height.HasValue)
        {
            node.SetAttribute("height", OdfNamespaces.Svg, request.Height.Value.ToString(), "svg");
            changed = true;
        }

        if (request.AltText is not null)
        {
            SetOptionalChildText(node, "desc", OdfNamespaces.Svg, "svg", request.AltText);
            changed = true;
        }

        return changed;
    }

    internal static bool ApplyShapeUpdate(OdfNode node, OdfDocument document, OdfShapeUpdateRequest request)
    {
        bool changed = ApplyShapeLayout(node, request.X, request.Y, request.Width, request.Height, request.LayerName, request.ZIndex);
        var shape = new OdfShape(node, document);
        if (request.FillColor is not null)
        {
            shape.FillColor = request.FillColor;
            changed = true;
        }

        if (request.StrokeColor is not null)
        {
            shape.StrokeColor = request.StrokeColor;
            changed = true;
        }

        return changed;
    }

    internal static bool ApplyShapeLayout(
        OdfNode node,
        OdfLength? x,
        OdfLength? y,
        OdfLength? width,
        OdfLength? height,
        string? layerName,
        int? zIndex = null)
    {
        bool changed = false;
        if (x.HasValue)
        {
            node.SetAttribute("x", OdfNamespaces.Svg, x.Value.ToString(), "svg");
            changed = true;
        }

        if (y.HasValue)
        {
            node.SetAttribute("y", OdfNamespaces.Svg, y.Value.ToString(), "svg");
            changed = true;
        }

        if (width.HasValue)
        {
            node.SetAttribute("width", OdfNamespaces.Svg, width.Value.ToString(), "svg");
            changed = true;
        }

        if (height.HasValue)
        {
            node.SetAttribute("height", OdfNamespaces.Svg, height.Value.ToString(), "svg");
            changed = true;
        }

        if (layerName is not null)
        {
            node.SetAttribute("layer", OdfNamespaces.Draw, layerName, "draw");
            changed = true;
        }

        if (zIndex.HasValue)
        {
            node.SetAttribute("z-index", OdfNamespaces.Draw, zIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), "draw");
            changed = true;
        }

        return changed;
    }

    internal static bool MatchesShapeName(OdfNode node, string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (string.Equals(node.GetAttribute("id", OdfNamespaces.Draw), name, StringComparison.Ordinal) ||
            string.Equals(node.GetAttribute("id", OdfNamespaces.Xml), name, StringComparison.Ordinal) ||
            string.Equals(node.GetAttribute("name", OdfNamespaces.Draw), name, StringComparison.Ordinal));

    private bool MoveShapeInDocumentOrder(string name, bool toFront)
    {
        OdfNode? node = GetShapes().Select(shape => shape.Node).FirstOrDefault(candidate => MatchesShapeName(candidate, name));
        OdfNode? parent = node?.Parent;
        if (node is null || parent is null)
        {
            return false;
        }

        parent.RemoveChild(node);
        if (toFront || parent.Children.Count == 0)
        {
            parent.AppendChild(node);
        }
        else
        {
            parent.InsertBefore(node, parent.Children[0]);
        }

        return true;
    }

    private bool MoveShapeRelative(string name, string referenceName, bool before)
    {
        OdfNode[] nodes = GetShapes().Select(shape => shape.Node).ToArray();
        OdfNode? node = nodes.FirstOrDefault(candidate => MatchesShapeName(candidate, name));
        OdfNode? reference = nodes.FirstOrDefault(candidate => MatchesShapeName(candidate, referenceName));
        if (node is null ||
            reference is null ||
            ReferenceEquals(node, reference) ||
            node.Parent is null ||
            !ReferenceEquals(node.Parent, reference.Parent))
        {
            return false;
        }

        OdfNode parent = node.Parent;
        parent.RemoveChild(node);
        if (before)
        {
            parent.InsertBefore(node, reference);
        }
        else
        {
            parent.InsertAfter(node, reference);
        }

        return true;
    }

    internal static bool ReplaceTextRecursive(OdfNode node, string search, string replacement)
    {
        bool changed = false;
        if (node.NodeType is OdfNodeType.Text)
        {
            string text = node.TextContent;
            string next = text.Replace(search, replacement);
            if (!string.Equals(text, next, StringComparison.Ordinal))
            {
                node.TextContent = next;
                changed = true;
            }
        }

        foreach (OdfNode child in node.Children)
        {
            changed |= ReplaceTextRecursive(child, search, replacement);
        }

        return changed;
    }

    private static void SetOptionalChildText(OdfNode node, string localName, string namespaceUri, string prefix, string? text)
    {
        OdfNode? child = node.Children.FirstOrDefault(childNode =>
            childNode.NodeType is OdfNodeType.Element &&
            childNode.LocalName == localName &&
            childNode.NamespaceUri == namespaceUri);
        if (string.IsNullOrWhiteSpace(text))
        {
            if (child is not null)
            {
                node.RemoveChild(child);
            }

            return;
        }

        if (child is null)
        {
            child = new OdfNode(OdfNodeType.Element, localName, namespaceUri, prefix);
            node.AppendChild(child);
        }

        child.TextContent = text!;
    }
}
