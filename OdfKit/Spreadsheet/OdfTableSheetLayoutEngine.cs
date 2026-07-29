using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表版面配置引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetLayoutEngine
{
    internal static void AutoFitColumnWidth(OdfTableSheetMutationContext context, int col)
    {
        _ = AutoFitColumnWidth(context, col, new OdfAutoFitOptions(), CancellationToken.None);
    }

    internal static OdfLength AutoFitColumnWidth(
        OdfTableSheetMutationContext context,
        int col,
        OdfAutoFitOptions options,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<int, OdfLength> widths =
            AutoFitColumnWidths(context, [col], options, cancellationToken);
        return widths[col];
    }

    internal static IReadOnlyDictionary<int, OdfLength> AutoFitColumnWidths(
        OdfTableSheetMutationContext context,
        IEnumerable<int> columns,
        OdfAutoFitOptions options,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(columns, nameof(columns));
        ValidateOptions(options);

        var requested = new HashSet<int>();
        foreach (int column in columns)
        {
            if (column < 0)
                throw new ArgumentOutOfRangeException(nameof(columns));
            requested.Add(column);
        }

        var results = new Dictionary<int, OdfLength>();
        if (requested.Count == 0)
            return results;
        var sortedColumns = new List<int>(requested);
        sortedColumns.Sort();

        if (options.Mode == OdfAutoFitMode.Reader)
        {
            foreach (int column in sortedColumns)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SetColumnOptimalWidth(context, column, true);
                results[column] = GetColumnWidth(context, column) ?? options.DefaultColumnWidth;
            }
            return results;
        }

        var maximumWidths = new Dictionary<int, double>();
        foreach (int column in sortedColumns)
            maximumWidths[column] = options.MinimumColumnWidth.ToCentimeters();

        var operation = new LayoutOperation(options, cancellationToken);
        foreach (PhysicalCell cell in EnumeratePhysicalCells(context.TableNode))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cell.Node.LocalName == "covered-table-cell")
                continue;
            int index = sortedColumns.BinarySearch(cell.Column);
            if (index < 0)
                index = ~index;
            int end = cell.Column + cell.RepeatedColumns;
            while (index < sortedColumns.Count &&
                sortedColumns[index] < end)
            {
                int column = sortedColumns[index];
                double width = MeasureCell(
                    context,
                    cell.Node,
                    cell.Row,
                    column,
                    availableWidthCentimeters: null,
                    forceWrap: false,
                    operation).WidthCentimeters;
                maximumWidths[column] = Math.Max(maximumWidths[column], width);
                index++;
            }
        }

        double maximumAllowed = options.MaximumColumnWidth.ToCentimeters();
        using (context.Document.BeginUpdate())
        {
            foreach (int column in sortedColumns)
            {
                double width = Math.Min(maximumWidths[column], maximumAllowed);
                var length = OdfLength.FromCentimeters(width);
                SetColumnWidth(context, column, length);
                results[column] = length;
            }
        }

        return results;
    }

    internal static OdfLength AutoFitRowHeight(
        OdfTableSheetMutationContext context,
        int row,
        OdfAutoFitOptions options,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<int, OdfLength> heights =
            AutoFitRowHeights(context, [row], options, cancellationToken);
        return heights[row];
    }

    internal static IReadOnlyDictionary<int, OdfLength> AutoFitRowHeights(
        OdfTableSheetMutationContext context,
        IEnumerable<int> rows,
        OdfAutoFitOptions options,
        CancellationToken cancellationToken)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(rows, nameof(rows));
        ValidateOptions(options);

        var requested = new HashSet<int>();
        foreach (int row in rows)
        {
            if (row < 0)
                throw new ArgumentOutOfRangeException(nameof(rows));
            requested.Add(row);
        }

        var results = new Dictionary<int, OdfLength>();
        if (requested.Count == 0)
            return results;

        var sortedRows = new List<int>(requested);
        sortedRows.Sort();
        var physicalRows = new Dictionary<int, PhysicalRow>();
        int requestedIndex = 0;
        foreach (PhysicalRow physicalRow in EnumeratePhysicalRows(context.TableNode))
        {
            while (requestedIndex < sortedRows.Count &&
                sortedRows[requestedIndex] < physicalRow.Row)
            {
                requestedIndex++;
            }
            int index = requestedIndex;
            int end = physicalRow.Row + physicalRow.RepeatedRows;
            while (index < sortedRows.Count && sortedRows[index] < end)
            {
                physicalRows[sortedRows[index]] = physicalRow;
                index++;
            }
            requestedIndex = index;
            if (requestedIndex == sortedRows.Count)
                break;
        }

        if (options.Mode == OdfAutoFitMode.Reader)
        {
            foreach (int row in sortedRows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (physicalRows.TryGetValue(row, out PhysicalRow physicalRow) &&
                    physicalRow.RepeatedRows == 1)
                {
                    SetRowOptimalHeight(context, physicalRow.Node, true);
                }
                else
                {
                    SetRowOptimalHeight(context, row, true);
                }
                results[row] = GetRowHeight(context, row) ?? options.MinimumRowHeight;
            }
            return results;
        }

        var operation = new LayoutOperation(options, cancellationToken);
        double minimum = options.MinimumRowHeight.ToCentimeters();
        double maximum = options.MaximumRowHeight.ToCentimeters();
        var measuredRows = new Dictionary<OdfNode, double>();
        using (context.Document.BeginUpdate())
        {
            foreach (int row in sortedRows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double height = minimum;
                if (physicalRows.TryGetValue(row, out PhysicalRow physicalRow))
                {
                    if (!measuredRows.TryGetValue(physicalRow.Node, out height))
                    {
                        height = MeasureRow(
                            context,
                            physicalRow,
                            minimum,
                            options,
                            operation);
                        measuredRows[physicalRow.Node] = height;
                    }
                }

                var length = OdfLength.FromCentimeters(Math.Min(height, maximum));
                if (physicalRows.TryGetValue(row, out physicalRow) &&
                    physicalRow.RepeatedRows == 1)
                {
                    SetRowHeight(context, physicalRow.Node, length);
                }
                else
                {
                    SetRowHeight(context, row, length);
                }
                results[row] = length;
            }
        }

        return results;
    }

    internal static void SetColumnWidth(
        OdfTableSheetMutationContext context,
        int col,
        OdfLength width)
    {
        var colNode = context.GetOrCreateColumn(col);
        context.Document.StyleEngine.SetLocalStyleProperty(
            colNode,
            "table-column",
            "table-column-properties",
            "use-optimal-column-width",
            OdfNamespaces.Style,
            "false",
            "style",
            deferSave: true);
        context.Document.StyleEngine.SetLocalStyleProperty(
            colNode,
            "table-column",
            "table-column-properties",
            "column-width",
            OdfNamespaces.Style,
            width.ToString(),
            "style");
    }

    internal static void SetColumnOptimalWidth(
        OdfTableSheetMutationContext context,
        int col,
        bool useOptimal)
    {
        var colNode = context.GetOrCreateColumn(col);
        if (useOptimal)
        {
            context.Document.StyleEngine.SetLocalStyleProperty(
                colNode,
                "table-column",
                "table-column-properties",
                "column-width",
                OdfNamespaces.Style,
                null,
                propAttrPrefix: null,
                deferSave: true);
        }
        context.Document.StyleEngine.SetLocalStyleProperty(
            colNode,
            "table-column",
            "table-column-properties",
            "use-optimal-column-width",
            OdfNamespaces.Style,
            useOptimal ? "true" : "false",
            "style");
    }

    internal static void SetRowOptimalHeight(
        OdfTableSheetMutationContext context,
        int row,
        bool useOptimal)
    {
        var rowNode = context.GetOrCreateRow(row, forWrite: true);
        SetRowOptimalHeight(context, rowNode, useOptimal);
    }

    private static void SetRowOptimalHeight(
        OdfTableSheetMutationContext context,
        OdfNode rowNode,
        bool useOptimal)
    {
        if (useOptimal)
        {
            context.Document.StyleEngine.SetLocalStyleProperty(
                rowNode,
                "table-row",
                "table-row-properties",
                "row-height",
                OdfNamespaces.Style,
                null,
                propAttrPrefix: null,
                deferSave: true);
        }
        context.Document.StyleEngine.SetLocalStyleProperty(
            rowNode,
            "table-row",
            "table-row-properties",
            "use-optimal-row-height",
            OdfNamespaces.Style,
            useOptimal ? "true" : "false",
            "style");
    }

    internal static bool IsRowOptimalHeight(OdfTableSheetMutationContext context, int row)
    {
        OdfNode? rowNode = OdfTableSheetDomAccessEngine.TryFindRowNode(context.TableNode, row);
        if (rowNode is null)
            return false;
        string? styleName = rowNode.GetAttribute("style-name", OdfNamespaces.Table);
        if (string.IsNullOrEmpty(styleName))
            return false;
        string? val = context.Document.StyleEngine.GetStyleProperty(
            styleName!,
            "use-optimal-row-height",
            OdfNamespaces.Style,
            "table-row");
        return val == "true";
    }

    internal static void SetRowHeight(
        OdfTableSheetMutationContext context,
        int row,
        OdfLength? height)
    {
        var rowNode = context.GetOrCreateRow(row, forWrite: true);
        SetRowHeight(context, rowNode, height);
    }

    private static void SetRowHeight(
        OdfTableSheetMutationContext context,
        OdfNode rowNode,
        OdfLength? height)
    {
        if (height is not null)
        {
            context.Document.StyleEngine.SetLocalStyleProperty(
                rowNode,
                "table-row",
                "table-row-properties",
                "use-optimal-row-height",
                OdfNamespaces.Style,
                "false",
                "style",
                deferSave: true);
        }
        context.Document.StyleEngine.SetLocalStyleProperty(
            rowNode,
            "table-row",
            "table-row-properties",
            "row-height",
            OdfNamespaces.Style,
            height?.ToString(),
            "style");
    }

    internal static OdfLength? GetRowHeight(
        OdfTableSheetMutationContext context,
        int row)
    {
        OdfNode? rowNode = OdfTableSheetDomAccessEngine.TryFindRowNode(context.TableNode, row);
        if (rowNode is null)
            return null;
        string? styleName = rowNode.GetAttribute("style-name", OdfNamespaces.Table);
        if (string.IsNullOrEmpty(styleName))
            return null;
        string? val = context.Document.StyleEngine.GetStyleProperty(
            styleName!,
            "row-height",
            OdfNamespaces.Style,
            "table-row");
        return OdfLength.TryParse(val, out OdfLength length)
            ? (OdfLength?)length
            : null;
    }

    internal static OdfLength? GetColumnWidth(
        OdfTableSheetMutationContext context,
        int col)
    {
        OdfNode? colNode = OdfTableSheetDomAccessEngine.TryFindColumnNode(
            context.TableNode,
            col);
        string? styleName = colNode?.GetAttribute("style-name", OdfNamespaces.Table);
        if (string.IsNullOrEmpty(styleName))
            return null;
        string? val = context.Document.StyleEngine.GetStyleProperty(
            styleName!,
            "column-width",
            OdfNamespaces.Style,
            "table-column");
        return OdfLength.TryParse(val, out OdfLength length)
            ? (OdfLength?)length
            : null;
    }

    private static OdfTextMeasureResult MeasureCell(
        OdfTableSheetMutationContext context,
        OdfNode cellNode,
        int row,
        int column,
        double? availableWidthCentimeters,
        bool forceWrap,
        LayoutOperation operation)
    {
        operation.RecordCell();
        operation.EnsureEmbeddedFonts(context.Document);
        var cell = new OdfCell(cellNode, row, column, context.Document, context.SheetName);
        string text = cell.FormattedValue;
        operation.RecordText(text);

        string styleName = cell.StyleName ?? string.Empty;
        OdfStyleEngine styleEngine = context.Document.StyleEngine;
        string fontFamily = ResolveFontFamily(styleEngine, styleName, text, operation.Options);
        double fontSize = ResolveFontSize(styleEngine, styleName, operation.Options);
        bool bold = styleEngine.GetStyleProperty(
            styleName,
            "font-weight",
            OdfNamespaces.Fo,
            "text") == "bold";
        bool italic = styleEngine.GetStyleProperty(
            styleName,
            "font-style",
            OdfNamespaces.Fo,
            "text") == "italic";
        bool wrap = forceWrap ||
            styleEngine.GetStyleProperty(
                styleName,
                "wrap-option",
                OdfNamespaces.Fo,
                "table-cell") == "wrap" ||
            text.IndexOfAny(['\r', '\n']) >= 0;
        double rotation = ParseFiniteDouble(styleEngine.GetStyleProperty(
            styleName,
            "rotation-angle",
            OdfNamespaces.Style,
            "table-cell"));

        double horizontalInset = GetInset(
            styleEngine,
            styleName,
            horizontal: true,
            operation.Options.HorizontalPadding.ToCentimeters());
        double verticalInset = GetInset(
            styleEngine,
            styleName,
            horizontal: false,
            operation.Options.VerticalPadding.ToCentimeters());
        double? textWidth = availableWidthCentimeters is double available
            ? Math.Max(available - horizontalInset, 0.01)
            : null;

        var request = new OdfTextMeasureRequest
        {
            Text = text,
            FontFamily = fontFamily,
            FontSizePoints = fontSize,
            IsBold = bold,
            IsItalic = italic,
            WritingMode = OdfWritingModeExtensions.FromOdfToken(
                styleEngine.GetStyleProperty(
                    styleName,
                    "writing-mode",
                    OdfNamespaces.Style,
                    "table-cell")),
            AvailableWidthCentimeters = textWidth,
            Wrap = wrap && textWidth is not null,
            RotationDegrees = rotation,
            MaximumTextElements = operation.Options.MaximumTextElementsPerBlock
        };
        OdfRichText? richText = cell.GetRichText();
        if (richText is not null && richText.Runs.Count > 0)
        {
            operation.RecordRuns(richText.Runs.Count);
            foreach (OdfRichTextRun run in richText.Runs)
            {
                request.Runs.Add(
                    new OdfTextMeasureRun
                    {
                        Text = run.Text,
                        FontFamily = string.IsNullOrWhiteSpace(run.FontFamily)
                            ? fontFamily
                            : run.FontFamily!,
                        FontSizePoints = run.FontSizePoints is double runSize &&
                            IsFinite(runSize) &&
                            runSize > 0
                                ? Math.Min(runSize, 1_000)
                                : fontSize,
                        IsBold = run.Bold || bold,
                        IsItalic = run.Italic || italic,
                    });
            }
            request.MaximumRuns = operation.Options.MaximumRichTextRuns;
        }

        OdfTextMeasureResult measured = operation.Measure(request);
        double width = measured.WidthCentimeters + horizontalInset;
        double height = measured.HeightCentimeters + verticalInset;
        if (!IsFinite(width) || !IsFinite(height) || width < 0 || height < 0)
            throw new InvalidOperationException();
        return new OdfTextMeasureResult(width, height, measured.LineCount, measured.IsExact);
    }

    private static double MeasureRow(
        OdfTableSheetMutationContext context,
        PhysicalRow row,
        double minimum,
        OdfAutoFitOptions options,
        LayoutOperation operation)
    {
        double height = minimum;
        int column = 0;
        foreach (OdfNode cellNode in row.Node.Children)
        {
            if ((cellNode.LocalName != "table-cell" &&
                    cellNode.LocalName != "covered-table-cell") ||
                cellNode.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            int repeated = OdfTableSheetRepeatSplitEngine.GetRepeatCount(
                cellNode,
                "number-columns-repeated");
            if (cellNode.LocalName != "covered-table-cell")
            {
                int span = GetPositiveIntegerAttribute(
                    cellNode,
                    "number-columns-spanned",
                    1);
                double available = GetAvailableCellWidth(
                    context,
                    column,
                    span,
                    operation);
                OdfTextMeasureResult measurement = MeasureCell(
                    context,
                    cellNode,
                    row.Row,
                    column,
                    available,
                    forceWrap: false,
                    operation);
                height = Math.Max(height, measurement.HeightCentimeters);
            }
            column += repeated;
        }
        return height;
    }

    private static string ResolveFontFamily(
        OdfStyleEngine styleEngine,
        string styleName,
        string text,
        OdfAutoFitOptions options)
    {
        bool hasNonAscii = false;
        foreach (char value in text)
        {
            if (value > 0x7f)
            {
                hasNonAscii = true;
                break;
            }
        }

        string? family = hasNonAscii
            ? styleEngine.GetStyleProperty(
                styleName,
                "font-name-asian",
                OdfNamespaces.Style,
                "text")
            : null;
        family ??= styleEngine.GetStyleProperty(
            styleName,
            "font-name",
            OdfNamespaces.Style,
            "text");
        return string.IsNullOrWhiteSpace(family)
            ? options.DefaultFontFamily
            : family!;
    }

    private static double ResolveFontSize(
        OdfStyleEngine styleEngine,
        string styleName,
        OdfAutoFitOptions options)
    {
        string? raw = styleEngine.GetStyleProperty(
            styleName,
            "font-size",
            OdfNamespaces.Fo,
            "text");
        if (OdfLength.TryParse(raw, out OdfLength length) &&
            length.Unit is not OdfUnit.Percentage and not OdfUnit.Em)
        {
            double points = length.ToPoints();
            if (IsFinite(points) && points > 0)
                return Math.Min(points, 1_000);
        }
        return options.DefaultFontSizePoints;
    }

    private static double GetInset(
        OdfStyleEngine styleEngine,
        string styleName,
        bool horizontal,
        double fallback)
    {
        string first = horizontal ? "padding-left" : "padding-top";
        string second = horizontal ? "padding-right" : "padding-bottom";
        string? common = styleEngine.GetStyleProperty(
            styleName,
            "padding",
            OdfNamespaces.Fo,
            "table-cell");
        double commonValue = ParseAbsoluteLength(common, fallback / 2);
        double value = ParseAbsoluteLength(
            styleEngine.GetStyleProperty(
                styleName,
                first,
                OdfNamespaces.Fo,
                "table-cell"),
            commonValue);
        value += ParseAbsoluteLength(
            styleEngine.GetStyleProperty(
                styleName,
                second,
                OdfNamespaces.Fo,
                "table-cell"),
            commonValue);
        return Math.Max(value, 0);
    }

    private static double GetAvailableCellWidth(
        OdfTableSheetMutationContext context,
        int column,
        int span,
        LayoutOperation operation)
    {
        double total = 0;
        for (int offset = 0; offset < span; offset++)
        {
            total += operation.GetColumnWidthCentimeters(
                context,
                column + offset);
        }
        return total;
    }

    private static double ParseAbsoluteLength(string? value, double fallback)
    {
        if (!OdfLength.TryParse(value, out OdfLength length) ||
            length.Unit is OdfUnit.Percentage or OdfUnit.Em)
        {
            return fallback;
        }

        double centimeters = length.ToCentimeters();
        return IsFinite(centimeters) && centimeters >= 0
            ? centimeters
            : fallback;
    }

    private static double ParseFiniteDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) &&
        IsFinite(number)
            ? number
            : 0;

    private static int GetPositiveIntegerAttribute(
        OdfNode node,
        string localName,
        int fallback)
    {
        string? raw = node.GetAttribute(localName, OdfNamespaces.Table);
        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int value) &&
            value > 0
                ? value
                : fallback;
    }

    private static IEnumerable<PhysicalCell> EnumeratePhysicalCells(OdfNode tableNode)
    {
        int row = 0;
        foreach (OdfNode child in tableNode.Children)
        {
            if (OdfTableSheetDomAccessEngine.RowContainerNames.Contains(child.LocalName) &&
                child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (OdfNode inner in child.Children)
                {
                    if (inner.LocalName != "table-row" ||
                        inner.NamespaceUri != OdfNamespaces.Table)
                    {
                        continue;
                    }
                    foreach (PhysicalCell cell in EnumerateRowCells(inner, row))
                        yield return cell;
                    row += OdfTableSheetRepeatSplitEngine.GetRepeatCount(
                        inner,
                        "number-rows-repeated");
                }
            }
            else if (child.LocalName == "table-row" &&
                child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (PhysicalCell cell in EnumerateRowCells(child, row))
                    yield return cell;
                row += OdfTableSheetRepeatSplitEngine.GetRepeatCount(
                    child,
                    "number-rows-repeated");
            }
        }
    }

    private static IEnumerable<PhysicalRow> EnumeratePhysicalRows(OdfNode tableNode)
    {
        int row = 0;
        foreach (OdfNode child in tableNode.Children)
        {
            if (OdfTableSheetDomAccessEngine.RowContainerNames.Contains(child.LocalName) &&
                child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (OdfNode inner in child.Children)
                {
                    if (inner.LocalName != "table-row" ||
                        inner.NamespaceUri != OdfNamespaces.Table)
                    {
                        continue;
                    }
                    int repeated = OdfTableSheetRepeatSplitEngine.GetRepeatCount(
                        inner,
                        "number-rows-repeated");
                    yield return new PhysicalRow(inner, row, repeated);
                    row += repeated;
                }
            }
            else if (child.LocalName == "table-row" &&
                child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeated = OdfTableSheetRepeatSplitEngine.GetRepeatCount(
                    child,
                    "number-rows-repeated");
                yield return new PhysicalRow(child, row, repeated);
                row += repeated;
            }
        }
    }

    private static IEnumerable<PhysicalCell> EnumerateRowCells(
        OdfNode rowNode,
        int row)
    {
        int column = 0;
        foreach (OdfNode child in rowNode.Children)
        {
            if ((child.LocalName != "table-cell" &&
                    child.LocalName != "covered-table-cell") ||
                child.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            int repeated = OdfTableSheetRepeatSplitEngine.GetRepeatCount(
                child,
                "number-columns-repeated");
            yield return new PhysicalCell(child, row, column, repeated);
            column += repeated;
        }
    }

    private static void ValidateOptions(OdfAutoFitOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));
        if (options.Mode == OdfAutoFitMode.Precise && options.TextMeasurer is null)
            throw new ArgumentNullException(nameof(options));
        if (options.MaximumCells < 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (options.MaximumTextElements < 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (options.MaximumTextElementsPerBlock < 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (options.MaximumMeasurementCacheEntries < 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (options.MaximumRichTextRuns < 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (options.MaximumEmbeddedFonts < 1 || options.MaximumEmbeddedFontBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (!IsFinite(options.DefaultFontSizePoints) ||
            options.DefaultFontSizePoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        ValidateRange(
            options.MinimumColumnWidth,
            options.MaximumColumnWidth,
            nameof(options.MinimumColumnWidth));
        ValidateRange(
            options.MinimumRowHeight,
            options.MaximumRowHeight,
            nameof(options.MinimumRowHeight));
        _ = PositiveCentimeters(
            options.DefaultColumnWidth,
            nameof(options.DefaultColumnWidth));
        _ = NonNegativeCentimeters(
            options.HorizontalPadding,
            nameof(options.HorizontalPadding));
        _ = NonNegativeCentimeters(
            options.VerticalPadding,
            nameof(options.VerticalPadding));
    }

    private static void ValidateRange(
        OdfLength minimum,
        OdfLength maximum,
        string parameterName)
    {
        double min = PositiveCentimeters(minimum, parameterName);
        double max = PositiveCentimeters(maximum, parameterName);
        if (min > max)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private static double PositiveCentimeters(
        OdfLength length,
        string parameterName)
    {
        double value = length.ToCentimeters();
        if (!IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(parameterName);
        return value;
    }

    private static double NonNegativeCentimeters(
        OdfLength length,
        string parameterName)
    {
        double value = length.ToCentimeters();
        if (!IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(parameterName);
        return value;
    }

    private readonly record struct PhysicalCell(
        OdfNode Node,
        int Row,
        int Column,
        int RepeatedColumns);

    private readonly record struct PhysicalRow(
        OdfNode Node,
        int Row,
        int RepeatedRows);

    private readonly record struct MeasurementKey(
        string Text,
        string FontFamily,
        double FontSizePoints,
        bool IsBold,
        bool IsItalic,
        OdfWritingMode WritingMode,
        double? AvailableWidthCentimeters,
        bool Wrap,
        double RotationDegrees);

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private sealed class LayoutOperation
    {
        private readonly Dictionary<MeasurementKey, OdfTextMeasureResult> _cache = [];
        private readonly Dictionary<int, double> _columnWidths = [];
        private readonly CancellationToken _cancellationToken;
        private int _cellCount;
        private long _textElementCount;
        private int _richTextRunCount;
        private bool _embeddedFontsInitialized;

        internal LayoutOperation(
            OdfAutoFitOptions options,
            CancellationToken cancellationToken)
        {
            Options = options;
            _cancellationToken = cancellationToken;
        }

        internal OdfAutoFitOptions Options { get; }

        internal void RecordCell()
        {
            _cellCount++;
            if (_cellCount > Options.MaximumCells)
                throw new InvalidOperationException();
        }

        internal void RecordText(string text)
        {
            if (text.Length > Options.MaximumTextElementsPerBlock)
                throw new InvalidOperationException();
            _textElementCount += text.Length;
            if (_textElementCount > Options.MaximumTextElements)
                throw new InvalidOperationException();
        }

        internal void RecordRuns(int count)
        {
            _richTextRunCount = checked(_richTextRunCount + count);
            if (_richTextRunCount > Options.MaximumRichTextRuns)
                throw new InvalidOperationException();
        }

        internal void EnsureEmbeddedFonts(SpreadsheetDocument document)
        {
            if (_embeddedFontsInitialized ||
                Options.Mode != OdfAutoFitMode.Precise ||
                !Options.UseEmbeddedFonts)
            {
                return;
            }
            if (ReferenceEquals(document.FontContext, OdfFontContext.Default))
                throw new InvalidOperationException();
            document.FontContext.RegisterEmbeddedFonts(
                document,
                Options.MaximumEmbeddedFonts,
                Options.MaximumEmbeddedFontBytes);
            _embeddedFontsInitialized = true;
        }

        internal OdfTextMeasureResult Measure(OdfTextMeasureRequest request)
        {
            if (request.Runs.Count > 0)
            {
                IOdfTextLayoutMeasurer runMeasurer = Options.Mode == OdfAutoFitMode.Precise
                    ? Options.TextMeasurer!
                    : OdfFastTextLayoutMeasurer.Instance;
                return runMeasurer.Measure(request, _cancellationToken);
            }
            var key = new MeasurementKey(
                request.Text,
                request.FontFamily,
                request.FontSizePoints,
                request.IsBold,
                request.IsItalic,
                request.WritingMode,
                request.AvailableWidthCentimeters,
                request.Wrap,
                request.RotationDegrees);
            bool cacheable = request.Text.Length <= 512 &&
                Options.MaximumMeasurementCacheEntries > 0;
            if (cacheable && _cache.TryGetValue(key, out OdfTextMeasureResult cached))
                return cached;

            IOdfTextLayoutMeasurer measurer = Options.Mode == OdfAutoFitMode.Precise
                ? Options.TextMeasurer!
                : OdfFastTextLayoutMeasurer.Instance;
            OdfTextMeasureResult measured = measurer.Measure(
                request,
                _cancellationToken);
            if (cacheable && _cache.Count < Options.MaximumMeasurementCacheEntries)
                _cache[key] = measured;
            return measured;
        }

        internal double GetColumnWidthCentimeters(
            OdfTableSheetMutationContext context,
            int column)
        {
            if (_columnWidths.TryGetValue(column, out double cached))
                return cached;

            OdfLength width =
                GetColumnWidth(context, column) ??
                Options.DefaultColumnWidth;
            double centimeters = Math.Max(width.ToCentimeters(), 0.01);
            _columnWidths[column] = centimeters;
            return centimeters;
        }
    }
}
