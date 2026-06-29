using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表 <c>table:tracked-changes</c> 讀寫與接受／拒絕引擎（內部協作者）。
/// </summary>
internal static class SpreadsheetDocumentTrackedChangesEngine
{
    internal static bool IsTrackingEnabled(OdfNode sheetsRoot)
    {
        OdfNode? trackedChangesNode = FindTrackedChangesNode(sheetsRoot);
        if (trackedChangesNode is null)
            return false;

        string? trackChanges = trackedChangesNode.GetAttribute("track-changes", OdfNamespaces.Table);
        return string.Equals(trackChanges, "true", StringComparison.OrdinalIgnoreCase);
    }

    internal static void SetTrackingEnabled(OdfNode sheetsRoot, bool enabled)
    {
        OdfNode trackedChangesNode = GetOrCreateTrackedChangesNode(sheetsRoot);
        if (enabled)
        {
            trackedChangesNode.SetAttribute("track-changes", OdfNamespaces.Table, "true", "table");
        }
        else
        {
            trackedChangesNode.RemoveAttribute("track-changes", OdfNamespaces.Table);
        }
    }

    internal static void RecordCellContentChange(
        SpreadsheetDocument document,
        string sheetName,
        int row,
        int column,
        OdfNode previousCellSnapshot)
    {
        if (!IsTrackingEnabled(document.SheetsRoot) || string.IsNullOrEmpty(sheetName))
            return;

        int sheetIndex = GetSheetIndex(document, sheetName);
        if (sheetIndex < 0)
            return;

        OdfNode trackedChangesNode = GetOrCreateTrackedChangesNode(document.SheetsRoot);
        string changeId = "tc_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        var changeNode = new OdfNode(OdfNodeType.Element, "cell-content-change", OdfNamespaces.Table, "table");
        changeNode.SetAttribute("id", OdfNamespaces.Table, changeId, "table");
        changeNode.SetAttribute("acceptance-state", OdfNamespaces.Table, "pending", "table");

        var addressNode = new OdfNode(OdfNodeType.Element, "cell-address", OdfNamespaces.Table, "table");
        addressNode.SetAttribute("column", OdfNamespaces.Table, column.ToString(CultureInfo.InvariantCulture), "table");
        addressNode.SetAttribute("row", OdfNamespaces.Table, row.ToString(CultureInfo.InvariantCulture), "table");
        addressNode.SetAttribute("table", OdfNamespaces.Table, sheetIndex.ToString(CultureInfo.InvariantCulture), "table");
        changeNode.AppendChild(addressNode);

        WriteOfficeChangeInfo(changeNode, "Author", DateTime.UtcNow);

        var previousNode = new OdfNode(OdfNodeType.Element, "previous", OdfNamespaces.Table, "table");
        previousNode.AppendChild(CreateChangeTrackTableCell(previousCellSnapshot));
        changeNode.AppendChild(previousNode);

        trackedChangesNode.AppendChild(changeNode);
    }

    internal static void RecordRowInsertion(
        SpreadsheetDocument document,
        string sheetName,
        int position,
        int count)
    {
        if (!IsTrackingEnabled(document.SheetsRoot) || string.IsNullOrEmpty(sheetName))
            return;

        int sheetIndex = GetSheetIndex(document, sheetName);
        if (sheetIndex < 0)
            return;

        OdfNode changeNode = CreateInsertionChangeNode(sheetIndex, "row", position, count);
        GetOrCreateTrackedChangesNode(document.SheetsRoot).AppendChild(changeNode);
    }

    internal static void RecordRowDeletion(
        SpreadsheetDocument document,
        string sheetName,
        int position,
        IReadOnlyList<OdfNode> deletedRowSnapshots)
    {
        if (!IsTrackingEnabled(document.SheetsRoot) || string.IsNullOrEmpty(sheetName) || deletedRowSnapshots.Count == 0)
            return;

        int sheetIndex = GetSheetIndex(document, sheetName);
        if (sheetIndex < 0)
            return;

        OdfNode changeNode = CreateDeletionChangeNode(sheetIndex, "row", position, deletedRowSnapshots.Count);

        foreach (OdfNode snapshot in deletedRowSnapshots)
        {
            var holder = new OdfNode(OdfNodeType.Element, "deleted-row-snapshot", OdfNamespaces.Table, "table");
            holder.AppendChild(snapshot.CloneNode(deep: true));
            changeNode.AppendChild(holder);
        }

        GetOrCreateTrackedChangesNode(document.SheetsRoot).AppendChild(changeNode);
    }

    internal static void RecordColumnInsertion(
        SpreadsheetDocument document,
        string sheetName,
        int position,
        int count)
    {
        if (!IsTrackingEnabled(document.SheetsRoot) || string.IsNullOrEmpty(sheetName))
            return;

        int sheetIndex = GetSheetIndex(document, sheetName);
        if (sheetIndex < 0)
            return;

        OdfNode changeNode = CreateInsertionChangeNode(sheetIndex, "column", position, count);
        GetOrCreateTrackedChangesNode(document.SheetsRoot).AppendChild(changeNode);
    }

    internal static void RecordColumnDeletion(
        SpreadsheetDocument document,
        string sheetName,
        int position,
        ColumnDeletionSnapshots deletedSnapshots)
    {
        if (!IsTrackingEnabled(document.SheetsRoot) || string.IsNullOrEmpty(sheetName))
            return;

        int deletedCount = deletedSnapshots.ColumnSnapshots.Count;
        if (deletedCount == 0 && deletedSnapshots.RowCellSnapshots.Count == 0)
            return;

        int sheetIndex = GetSheetIndex(document, sheetName);
        if (sheetIndex < 0)
            return;

        int spanCount = deletedCount > 0 ? deletedCount : 1;
        OdfNode changeNode = CreateDeletionChangeNode(sheetIndex, "column", position, spanCount);

        foreach (OdfNode snapshot in deletedSnapshots.ColumnSnapshots)
        {
            var holder = new OdfNode(OdfNodeType.Element, "deleted-column-snapshot", OdfNamespaces.Table, "table");
            holder.AppendChild(snapshot.CloneNode(deep: true));
            changeNode.AppendChild(holder);
        }

        foreach ((int rowIndex, OdfNode cellSnapshot) in deletedSnapshots.RowCellSnapshots)
        {
            var holder = new OdfNode(OdfNodeType.Element, "deleted-cell-snapshot", OdfNamespaces.Table, "table");
            holder.SetAttribute("row", OdfNamespaces.Table, rowIndex.ToString(CultureInfo.InvariantCulture), "table");
            holder.AppendChild(cellSnapshot.CloneNode(deep: true));
            changeNode.AppendChild(holder);
        }

        GetOrCreateTrackedChangesNode(document.SheetsRoot).AppendChild(changeNode);
    }

    internal static void RecordCellMovement(
        SpreadsheetDocument document,
        string sheetName,
        int sourceRow,
        int sourceColumn,
        int targetRow,
        int targetColumn)
    {
        if (!IsTrackingEnabled(document.SheetsRoot) || string.IsNullOrEmpty(sheetName))
            return;

        int sheetIndex = GetSheetIndex(document, sheetName);
        if (sheetIndex < 0)
            return;

        OdfNode changeNode = CreateMovementChangeNode(
            sheetIndex,
            sourceRow,
            sourceColumn,
            targetRow,
            targetColumn);

        GetOrCreateTrackedChangesNode(document.SheetsRoot).AppendChild(changeNode);
    }

    internal static IReadOnlyList<OdfSpreadsheetTrackedChangeInfo> GetTrackedChanges(SpreadsheetDocument document)
    {
        OdfNode? trackedChangesNode = FindTrackedChangesNode(document.SheetsRoot);
        if (trackedChangesNode is null)
            return [];

        List<OdfSpreadsheetTrackedChangeInfo> results = [];
        foreach (OdfNode child in trackedChangesNode.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Table)
                continue;

            OdfSpreadsheetChangeKind? kind = child.LocalName switch
            {
                "cell-content-change" => OdfSpreadsheetChangeKind.CellContentChange,
                "insertion" => OdfSpreadsheetChangeKind.Insertion,
                "deletion" => OdfSpreadsheetChangeKind.Deletion,
                "movement" => OdfSpreadsheetChangeKind.Movement,
                _ => null,
            };
            if (kind is null)
                continue;

            string? changeId = child.GetAttribute("id", OdfNamespaces.Table);
            if (string.IsNullOrEmpty(changeId))
                continue;

            (string author, DateTime changedAt) = OdfTrackedChangeMetadataReader.Read(child);
            string acceptanceState = child.GetAttribute("acceptance-state", OdfNamespaces.Table) ?? "pending";
            OdfCellAddress? cellAddress = TryReadCellAddress(document, child);
            string? previousContent = kind == OdfSpreadsheetChangeKind.CellContentChange
                ? ReadPreviousDisplayText(child)
                : null;
            string? previousFormula = kind == OdfSpreadsheetChangeKind.CellContentChange
                ? ReadPreviousFormula(child)
                : null;
            (string? sheetName, string? structuralType, int? position, int? count) = ReadStructuralAttributes(document, child);
            OdfCellAddress? sourceAddress = null;
            OdfCellAddress? targetAddress = null;

            if (kind == OdfSpreadsheetChangeKind.Movement)
            {
                (sourceAddress, targetAddress) = ReadMovementAddresses(document, child);
                sheetName ??= sourceAddress?.SheetName;
            }

            results.Add(new OdfSpreadsheetTrackedChangeInfo(
                changeId!,
                kind.Value,
                author,
                changedAt,
                cellAddress,
                previousContent,
                acceptanceState,
                previousFormula,
                sheetName,
                structuralType,
                position,
                count,
                sourceAddress,
                targetAddress));
        }

        return results.AsReadOnly();
    }

    internal static void AcceptChange(SpreadsheetDocument document, string changeId) =>
        RemoveChangeNode(document, changeId);

    internal static void RejectChange(SpreadsheetDocument document, string changeId)
    {
        OdfNode? changeNode = FindChangeNode(document.SheetsRoot, changeId);
        if (changeNode is null)
            return;

        switch (changeNode.LocalName)
        {
            case "cell-content-change":
                RestoreCellFromChange(document, changeNode);
                break;
            case "insertion":
                RejectInsertion(document, changeNode);
                break;
            case "deletion":
                RejectDeletion(document, changeNode);
                break;
            case "movement":
                RejectMovement(document, changeNode);
                break;
        }

        RemoveChangeNode(document, changeId);
    }

    internal static void AcceptAllChanges(SpreadsheetDocument document)
    {
        foreach (OdfSpreadsheetTrackedChangeInfo change in GetTrackedChanges(document))
        {
            if (IsPending(change.AcceptanceState))
                RemoveChangeNode(document, change.ChangeId);
        }
    }

    internal static void RejectAllChanges(SpreadsheetDocument document)
    {
        foreach (OdfSpreadsheetTrackedChangeInfo change in GetTrackedChanges(document))
        {
            if (IsPending(change.AcceptanceState))
                RejectChange(document, change.ChangeId);
        }
    }

    private static bool IsPending(string acceptanceState) =>
        string.IsNullOrEmpty(acceptanceState) ||
        string.Equals(acceptanceState, "pending", StringComparison.OrdinalIgnoreCase);

    private static void RestoreCellFromChange(SpreadsheetDocument document, OdfNode changeNode)
    {
        OdfCellAddress? address = TryReadCellAddress(document, changeNode);
        if (address?.SheetName is null)
            return;

        OdfTableSheet? sheet = document.FindSheet(address.Value.SheetName);
        if (sheet is null)
            return;

        OdfNode cellNode = sheet.GetCell(address.Value.Row, address.Value.Column).Node;
        OdfNode? trackCell = FindPreviousTrackCell(changeNode);
        if (trackCell is null)
            return;

        ApplyChangeTrackTableCell(cellNode, trackCell);
    }

    private static void RemoveChangeNode(SpreadsheetDocument document, string changeId)
    {
        OdfNode? changeNode = FindChangeNode(document.SheetsRoot, changeId);
        changeNode?.Parent?.RemoveChild(changeNode);
    }

    private static OdfNode? FindChangeNode(OdfNode sheetsRoot, string changeId)
    {
        OdfNode? trackedChangesNode = FindTrackedChangesNode(sheetsRoot);
        if (trackedChangesNode is null)
            return null;

        foreach (OdfNode child in trackedChangesNode.Children)
        {
            if (child.GetAttribute("id", OdfNamespaces.Table) == changeId)
                return child;
        }

        return null;
    }

    private static OdfNode GetOrCreateTrackedChangesNode(OdfNode sheetsRoot)
    {
        OdfNode? trackedChangesNode = FindTrackedChangesNode(sheetsRoot);
        if (trackedChangesNode is not null)
            return trackedChangesNode;

        trackedChangesNode = new OdfNode(OdfNodeType.Element, "tracked-changes", OdfNamespaces.Table, "table");
        if (sheetsRoot.Children.Count > 0)
            sheetsRoot.InsertBefore(trackedChangesNode, sheetsRoot.Children[0]);
        else
            sheetsRoot.AppendChild(trackedChangesNode);

        return trackedChangesNode;
    }

    private static OdfNode? FindTrackedChangesNode(OdfNode sheetsRoot)
    {
        foreach (OdfNode child in sheetsRoot.Children)
        {
            if (child.LocalName == "tracked-changes" && child.NamespaceUri == OdfNamespaces.Table)
                return child;
        }

        return null;
    }

    private static int GetSheetIndex(SpreadsheetDocument document, string sheetName)
    {
        int index = 0;
        foreach (OdfNode child in document.SheetsRoot.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "table" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            if (string.Equals(child.GetAttribute("name", OdfNamespaces.Table), sheetName, StringComparison.Ordinal))
                return index;

            index++;
        }

        return -1;
    }

    private static string? ResolveSheetName(SpreadsheetDocument document, int sheetIndex)
    {
        int index = 0;
        foreach (OdfNode child in document.SheetsRoot.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "table" ||
                child.NamespaceUri != OdfNamespaces.Table)
                continue;

            if (index == sheetIndex)
                return child.GetAttribute("name", OdfNamespaces.Table);

            index++;
        }

        return null;
    }

    private static OdfCellAddress? TryReadCellAddress(SpreadsheetDocument document, OdfNode changeNode)
    {
        OdfNode? addressNode = OdfTableSheetDomHelper.FindChildElement(changeNode, "cell-address", OdfNamespaces.Table);
        if (addressNode is null)
            return null;

        if (!int.TryParse(addressNode.GetAttribute("row", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int row) ||
            !int.TryParse(addressNode.GetAttribute("column", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int column) ||
            !int.TryParse(addressNode.GetAttribute("table", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sheetIndex))
            return null;

        string? sheetName = ResolveSheetName(document, sheetIndex);
        return string.IsNullOrEmpty(sheetName) ? null : new OdfCellAddress(row, column, sheetName);
    }

    private static string? ReadPreviousDisplayText(OdfNode changeNode)
    {
        OdfNode? trackCell = FindPreviousTrackCell(changeNode);
        return trackCell is null ? null : ReadTrackCellDisplayText(trackCell);
    }

    private static string? ReadPreviousFormula(OdfNode changeNode)
    {
        OdfNode? trackCell = FindPreviousTrackCell(changeNode);
        return trackCell?.GetAttribute("formula", OdfNamespaces.Table);
    }

    private static OdfNode? FindPreviousTrackCell(OdfNode changeNode)
    {
        OdfNode? previousNode = OdfTableSheetDomHelper.FindChildElement(changeNode, "previous", OdfNamespaces.Table);
        if (previousNode is null)
            return null;

        return OdfTableSheetDomHelper.FindChildElement(previousNode, "change-track-table-cell", OdfNamespaces.Table);
    }

    private static OdfNode CreateChangeTrackTableCell(OdfNode cellNode)
    {
        var trackCell = new OdfNode(OdfNodeType.Element, "change-track-table-cell", OdfNamespaces.Table, "table");
        CopyCellAttributes(cellNode, trackCell);

        foreach (OdfNode child in cellNode.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Text)
                trackCell.AppendChild(child.CloneNode(deep: true));
        }

        return trackCell;
    }

    private static void ApplyChangeTrackTableCell(OdfNode cellNode, OdfNode trackCell)
    {
        ClearCellValueAttributes(cellNode);

        CopyCellAttributes(trackCell, cellNode);

        var textChildren = new List<OdfNode>();
        foreach (OdfNode child in cellNode.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Text)
                textChildren.Add(child);
        }

        foreach (OdfNode child in textChildren)
            cellNode.RemoveChild(child);

        foreach (OdfNode child in trackCell.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Text)
                cellNode.AppendChild(child.CloneNode(deep: true));
        }
    }

    private static void CopyCellAttributes(OdfNode source, OdfNode target)
    {
        CopyAttribute(source, target, "value-type", OdfNamespaces.Office);
        CopyAttribute(source, target, "value", OdfNamespaces.Office);
        CopyAttribute(source, target, "boolean-value", OdfNamespaces.Office);
        CopyAttribute(source, target, "date-value", OdfNamespaces.Office);
        CopyAttribute(source, target, "formula", OdfNamespaces.Table);
        CopyAttribute(source, target, "style-name", OdfNamespaces.Table);
    }

    private static void CopyAttribute(OdfNode source, OdfNode target, string localName, string namespaceUri)
    {
        string? value = source.GetAttribute(localName, namespaceUri);
        if (value is null)
            target.RemoveAttribute(localName, namespaceUri);
        else
            target.SetAttribute(localName, namespaceUri, value, OdfNamespaces.GetPrefix(namespaceUri));
    }

    private static void ClearCellValueAttributes(OdfNode cellNode)
    {
        cellNode.RemoveAttribute("value-type", OdfNamespaces.Office);
        cellNode.RemoveAttribute("value", OdfNamespaces.Office);
        cellNode.RemoveAttribute("boolean-value", OdfNamespaces.Office);
        cellNode.RemoveAttribute("date-value", OdfNamespaces.Office);
        cellNode.RemoveAttribute("formula", OdfNamespaces.Table);
    }

    private static string ReadTrackCellDisplayText(OdfNode trackCell)
    {
        foreach (OdfNode child in trackCell.Children)
        {
            if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                return child.TextContent ?? string.Empty;
        }

        return trackCell.TextContent ?? string.Empty;
    }

    private static OdfNode CreateInsertionChangeNode(int sheetIndex, string structuralType, int position, int count)
    {
        string changeId = "tc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var changeNode = new OdfNode(OdfNodeType.Element, "insertion", OdfNamespaces.Table, "table");
        changeNode.SetAttribute("id", OdfNamespaces.Table, changeId, "table");
        changeNode.SetAttribute("acceptance-state", OdfNamespaces.Table, "pending", "table");
        changeNode.SetAttribute("type", OdfNamespaces.Table, structuralType, "table");
        changeNode.SetAttribute("position", OdfNamespaces.Table, position.ToString(CultureInfo.InvariantCulture), "table");
        changeNode.SetAttribute("count", OdfNamespaces.Table, count.ToString(CultureInfo.InvariantCulture), "table");
        changeNode.SetAttribute("table", OdfNamespaces.Table, sheetIndex.ToString(CultureInfo.InvariantCulture), "table");
        WriteOfficeChangeInfo(changeNode, "Author", DateTime.UtcNow);
        return changeNode;
    }

    private static OdfNode CreateMovementChangeNode(
        int sheetIndex,
        int sourceRow,
        int sourceColumn,
        int targetRow,
        int targetColumn)
    {
        string changeId = "tc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var changeNode = new OdfNode(OdfNodeType.Element, "movement", OdfNamespaces.Table, "table");
        changeNode.SetAttribute("id", OdfNamespaces.Table, changeId, "table");
        changeNode.SetAttribute("acceptance-state", OdfNamespaces.Table, "pending", "table");

        changeNode.AppendChild(CreateRangeAddressNode("source-range-address", sheetIndex, sourceRow, sourceColumn));
        changeNode.AppendChild(CreateRangeAddressNode("target-range-address", sheetIndex, targetRow, targetColumn));
        WriteOfficeChangeInfo(changeNode, "Author", DateTime.UtcNow);
        return changeNode;
    }

    private static OdfNode CreateRangeAddressNode(string localName, int sheetIndex, int row, int column)
    {
        var rangeNode = new OdfNode(OdfNodeType.Element, localName, OdfNamespaces.Table, "table");
        rangeNode.SetAttribute("column", OdfNamespaces.Table, column.ToString(CultureInfo.InvariantCulture), "table");
        rangeNode.SetAttribute("row", OdfNamespaces.Table, row.ToString(CultureInfo.InvariantCulture), "table");
        rangeNode.SetAttribute("table", OdfNamespaces.Table, sheetIndex.ToString(CultureInfo.InvariantCulture), "table");
        return rangeNode;
    }

    private static OdfNode CreateDeletionChangeNode(int sheetIndex, string structuralType, int position, int count)
    {
        string changeId = "tc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var changeNode = new OdfNode(OdfNodeType.Element, "deletion", OdfNamespaces.Table, "table");
        changeNode.SetAttribute("id", OdfNamespaces.Table, changeId, "table");
        changeNode.SetAttribute("acceptance-state", OdfNamespaces.Table, "pending", "table");
        changeNode.SetAttribute("type", OdfNamespaces.Table, structuralType, "table");
        changeNode.SetAttribute("position", OdfNamespaces.Table, position.ToString(CultureInfo.InvariantCulture), "table");
        changeNode.SetAttribute("table", OdfNamespaces.Table, sheetIndex.ToString(CultureInfo.InvariantCulture), "table");
        if (count > 1)
        {
            changeNode.SetAttribute("multi-deletion-spanned", OdfNamespaces.Table, count.ToString(CultureInfo.InvariantCulture), "table");
        }

        WriteOfficeChangeInfo(changeNode, "Author", DateTime.UtcNow);
        return changeNode;
    }

    private static (string? SheetName, string? StructuralType, int? Position, int? Count) ReadStructuralAttributes(
        SpreadsheetDocument document,
        OdfNode changeNode)
    {
        if (changeNode.LocalName is not ("insertion" or "deletion" or "movement"))
            return (null, null, null, null);

        string? structuralType = changeNode.GetAttribute("type", OdfNamespaces.Table);
        int? position = TryParseInt(changeNode.GetAttribute("position", OdfNamespaces.Table));
        int? count = changeNode.LocalName switch
        {
            "insertion" => TryParseInt(changeNode.GetAttribute("count", OdfNamespaces.Table)),
            "deletion" => TryParseInt(changeNode.GetAttribute("multi-deletion-spanned", OdfNamespaces.Table)) ?? 1,
            _ => TryParseInt(changeNode.GetAttribute("count", OdfNamespaces.Table)),
        };
        string? sheetName = null;

        if (int.TryParse(changeNode.GetAttribute("table", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sheetIndex))
            sheetName = ResolveSheetName(document, sheetIndex);

        return (sheetName, structuralType, position, count);
    }

    private static int? TryParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;

    private static (OdfCellAddress? Source, OdfCellAddress? Target) ReadMovementAddresses(
        SpreadsheetDocument document,
        OdfNode changeNode)
    {
        OdfCellAddress? source = TryReadRangeAddress(document, changeNode, "source-range-address");
        OdfCellAddress? target = TryReadRangeAddress(document, changeNode, "target-range-address");
        return (source, target);
    }

    private static OdfCellAddress? TryReadRangeAddress(
        SpreadsheetDocument document,
        OdfNode changeNode,
        string localName)
    {
        OdfNode? rangeNode = OdfTableSheetDomHelper.FindChildElement(changeNode, localName, OdfNamespaces.Table);
        if (rangeNode is null)
            return null;

        if (int.TryParse(rangeNode.GetAttribute("start-row", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int startRow) &&
            int.TryParse(rangeNode.GetAttribute("start-column", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int startColumn) &&
            int.TryParse(rangeNode.GetAttribute("table", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sheetIndex))
        {
            string? sheetName = ResolveSheetName(document, sheetIndex);
            return string.IsNullOrEmpty(sheetName) ? null : new OdfCellAddress(startRow, startColumn, sheetName);
        }

        if (int.TryParse(rangeNode.GetAttribute("row", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int row) &&
            int.TryParse(rangeNode.GetAttribute("column", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int column) &&
            int.TryParse(rangeNode.GetAttribute("table", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int tableIndex))
        {
            string? sheetName = ResolveSheetName(document, tableIndex);
            return string.IsNullOrEmpty(sheetName) ? null : new OdfCellAddress(row, column, sheetName);
        }

        return null;
    }

    private static void RejectMovement(SpreadsheetDocument document, OdfNode changeNode)
    {
        (OdfCellAddress? source, OdfCellAddress? target) = ReadMovementAddresses(document, changeNode);
        if (source?.SheetName is null || target?.SheetName is null)
            return;

        if (!string.Equals(source.Value.SheetName, target.Value.SheetName, StringComparison.Ordinal))
            return;

        OdfTableSheet? sheet = document.FindSheet(source.Value.SheetName);
        if (sheet is null)
            return;

        OdfTableSheetStructureEngine.MoveCell(
            sheet.TableNode,
            target.Value.Row,
            target.Value.Column,
            source.Value.Row,
            source.Value.Column);
    }

    private static void RejectInsertion(SpreadsheetDocument document, OdfNode changeNode)
    {
        (string? sheetName, string? structuralType, int? position, int? count) = ReadStructuralAttributes(document, changeNode);
        if (sheetName is null || position is null || count is null || count < 1)
            return;

        OdfTableSheet? sheet = document.FindSheet(sheetName);
        if (sheet is null)
            return;

        switch (structuralType)
        {
            case "row":
                OdfTableSheetStructureEngine.DeleteRows(sheet.TableNode, position.Value, count.Value);
                break;
            case "column":
                OdfTableSheetStructureEngine.DeleteColumns(sheet.TableNode, position.Value, count.Value);
                break;
        }
    }

    private static void RejectDeletion(SpreadsheetDocument document, OdfNode changeNode)
    {
        (string? sheetName, string? structuralType, int? position, int? _) = ReadStructuralAttributes(document, changeNode);
        if (sheetName is null || position is null)
            return;

        OdfTableSheet? sheet = document.FindSheet(sheetName);
        if (sheet is null)
            return;

        switch (structuralType)
        {
            case "row":
                RejectRowDeletion(sheet, changeNode, position.Value);
                break;
            case "column":
                RejectColumnDeletion(sheet, changeNode, position.Value);
                break;
        }
    }

    private static void RejectRowDeletion(OdfTableSheet sheet, OdfNode changeNode, int position)
    {
        List<OdfNode> snapshots = [];
        foreach (OdfNode child in changeNode.Children)
        {
            if (child.LocalName == "deleted-row-snapshot" && child.NamespaceUri == OdfNamespaces.Table && child.Children.Count > 0)
                snapshots.Add(child.Children[0].CloneNode(deep: true));
        }

        OdfTableSheetStructureEngine.RestoreRows(sheet.TableNode, position, snapshots);
    }

    private static void RejectColumnDeletion(OdfTableSheet sheet, OdfNode changeNode, int position)
    {
        List<OdfNode> columnSnapshots = [];
        List<(int RowIndex, OdfNode CellSnapshot)> rowCellSnapshots = [];

        foreach (OdfNode child in changeNode.Children)
        {
            if (child.NamespaceUri != OdfNamespaces.Table || child.Children.Count == 0)
                continue;

            if (child.LocalName == "deleted-column-snapshot")
            {
                columnSnapshots.Add(child.Children[0].CloneNode(deep: true));
                continue;
            }

            if (child.LocalName == "deleted-cell-snapshot" &&
                int.TryParse(child.GetAttribute("row", OdfNamespaces.Table), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rowIndex))
            {
                rowCellSnapshots.Add((rowIndex, child.Children[0].CloneNode(deep: true)));
            }
        }

        OdfTableSheetStructureEngine.RestoreColumns(sheet.TableNode, position, columnSnapshots);
        OdfTableSheetStructureEngine.RestoreColumnCells(sheet.TableNode, position, rowCellSnapshots);
    }

    private static void WriteOfficeChangeInfo(OdfNode changeNode, string author, DateTime date)
    {
        string dateText = OdfTrackedChangeMetadataReader.FormatChangeDate(date);
        var changeInfo = new OdfNode(OdfNodeType.Element, "change-info", OdfNamespaces.Office, "office");

        var creatorNode = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
        creatorNode.TextContent = author;
        changeInfo.AppendChild(creatorNode);

        var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
        dateNode.TextContent = dateText;
        changeInfo.AppendChild(dateNode);

        changeNode.AppendChild(changeInfo);
    }
}
