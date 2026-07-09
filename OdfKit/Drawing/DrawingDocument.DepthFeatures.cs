using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;

namespace OdfKit.Drawing;

public partial class DrawingDocument
{
    /// <summary>
    /// Replaces text in all drawing text boxes.
    /// 取代所有繪圖文字方塊中的文字。
    /// </summary>
    /// <param name="search">The text to search for. / 要搜尋的文字。</param>
    /// <param name="replacement">The replacement text. / 替換文字。</param>
    public override void ReplaceText(string search, string replacement)
    {
        ReplaceTextInTextBoxes(search, replacement);
    }

    /// <summary>
    /// Replaces text in all drawing text boxes and returns the changed text box count.
    /// 取代所有繪圖文字方塊中的文字並傳回已變更的文字方塊數量。
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
        foreach (OdfTextBox textBox in Pages.SelectMany(page => page.TextBoxes))
        {
            if (PresentationDocument.ReplaceTextRecursive(textBox.Node, search, replacement ?? string.Empty))
            {
                changed++;
            }
        }

        return changed;
    }

    /// <summary>
    /// Updates drawing pictures by id or name.
    /// 依識別碼或名稱更新繪圖圖片。
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
        IReadOnlyList<OdfPicture> pictures = Pages.SelectMany(page => page.Pictures).ToList().AsReadOnly();
        foreach (OdfPictureUpdateRequest request in requests)
        {
            List<OdfPicture> matches = pictures.Where(p => PresentationDocument.MatchesShapeName(p.Node, request.Name)).ToList();
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

            if (PresentationDocument.ApplyPictureUpdate(matches[0].Node, request))
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
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfBatchUpdateResult UpdateShapes(IEnumerable<string> names) => UpdateShapes(names, null, null, null, null, null);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfBatchUpdateResult UpdateShapes(IEnumerable<string> names, OdfLength? x) => UpdateShapes(names, x, null, null, null, null);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfBatchUpdateResult UpdateShapes(IEnumerable<string> names, OdfLength? x, OdfLength? y) => UpdateShapes(names, x, y, null, null, null);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfBatchUpdateResult UpdateShapes(IEnumerable<string> names, OdfLength? x, OdfLength? y, OdfLength? width) => UpdateShapes(names, x, y, width, null, null);

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfBatchUpdateResult UpdateShapes(IEnumerable<string> names, OdfLength? x, OdfLength? y, OdfLength? width, OdfLength? height) => UpdateShapes(names, x, y, width, height, null);


    /// <summary>
    /// Updates drawing shapes by id or name.
    /// 依識別碼或名稱更新繪圖圖形。
    /// </summary>
    /// <param name="names">The shape ids or names. / 圖形識別碼或名稱。</param>
    /// <param name="x">The optional X-axis position. / 選用的 X 軸位置。</param>
    /// <param name="y">The optional Y-axis position. / 選用的 Y 軸位置。</param>
    /// <param name="width">The optional width. / 選用的寬度。</param>
    /// <param name="height">The optional height. / 選用的高度。</param>
    /// <param name="layerName">The optional layer name. / 選用的圖層名稱。</param>
    /// <returns>The batch update result. / 批次更新結果。</returns>
    public OdfBatchUpdateResult UpdateShapes(IEnumerable<string> names, OdfLength? x, OdfLength? y, OdfLength? width, OdfLength? height, string? layerName)
    {
        if (names is null)
        {
            throw new ArgumentNullException(nameof(names));
        }

        var result = new OdfBatchUpdateResult();
        IReadOnlyList<OdfShape> shapes = Pages.SelectMany(page => page.Shapes).ToList().AsReadOnly();
        foreach (string name in names)
        {
            List<OdfShape> matches = shapes.Where(candidate => PresentationDocument.MatchesShapeName(candidate.Node, name)).ToList();
            if (matches.Count == 0)
            {
                result.MissingNames.Add(name);
                continue;
            }

            if (matches.Count > 1)
            {
                result.AmbiguousNames.Add(name);
                continue;
            }

            if (PresentationDocument.ApplyShapeLayout(matches[0].Node, x, y, width, height, layerName))
            {
                result.UpdatedNames.Add(name);
                result.UpdatedCount++;
            }
            else
            {
                result.UnchangedNames.Add(name);
            }
        }

        return result;
    }


    /// <summary>
    /// Updates drawing shapes by id or name.
    /// 依識別碼或名稱更新繪圖圖形。
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
        IReadOnlyList<OdfShape> shapes = Pages.SelectMany(page => page.Shapes).ToList().AsReadOnly();
        foreach (OdfShapeUpdateRequest request in requests)
        {
            List<OdfShape> matches = shapes.Where(candidate => PresentationDocument.MatchesShapeName(candidate.Node, request.Name)).ToList();
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

            if (PresentationDocument.ApplyShapeUpdate(matches[0].Node, this, request))
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

    private bool MoveShapeInDocumentOrder(string name, bool toFront)
    {
        OdfNode? node = Pages.SelectMany(page => page.Shapes).Select(shape => shape.Node)
            .FirstOrDefault(candidate => PresentationDocument.MatchesShapeName(candidate, name));
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
        OdfNode[] nodes = Pages.SelectMany(page => page.Shapes).Select(shape => shape.Node).ToArray();
        OdfNode? node = nodes.FirstOrDefault(candidate => PresentationDocument.MatchesShapeName(candidate, name));
        OdfNode? reference = nodes.FirstOrDefault(candidate => PresentationDocument.MatchesShapeName(candidate, referenceName));
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
}
