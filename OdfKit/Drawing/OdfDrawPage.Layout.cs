using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Drawing;

/// <summary>
/// Provides task-oriented drawing page layout operations.
/// 提供任務導向的繪圖頁面配置作業。
/// </summary>
public partial class OdfDrawPage
{
    /// <summary>
    /// Aligns the specified shapes while preserving their sizes.
    /// 對齊指定圖形並保留其大小。
    /// </summary>
    /// <param name="shapeIds">The drawing shape identifiers. / 繪圖圖形識別碼。</param>
    /// <param name="alignment">The requested alignment. / 要套用的對齊模式。</param>
    /// <returns>A structured layout result. / 結構化版面配置結果。</returns>
    public OdfShapeLayoutResult AlignShapes(IEnumerable<string> shapeIds, OdfShapeAlignment alignment)
    {
        List<ShapeBounds> shapes = ResolveBounds(shapeIds, out OdfShapeLayoutResult result);
        if (shapes.Count < 2)
            return result;

        double target = alignment switch
        {
            OdfShapeAlignment.Left => shapes.Min(shape => shape.X),
            OdfShapeAlignment.Center => shapes.Average(shape => shape.X + (shape.Width / 2d)),
            OdfShapeAlignment.Right => shapes.Max(shape => shape.X + shape.Width),
            OdfShapeAlignment.Top => shapes.Min(shape => shape.Y),
            OdfShapeAlignment.Middle => shapes.Average(shape => shape.Y + (shape.Height / 2d)),
            OdfShapeAlignment.Bottom => shapes.Max(shape => shape.Y + shape.Height),
            _ => throw new ArgumentOutOfRangeException(nameof(alignment))
        };

        foreach (ShapeBounds shape in shapes)
        {
            double value = alignment switch
            {
                OdfShapeAlignment.Left => target,
                OdfShapeAlignment.Center => target - (shape.Width / 2d),
                OdfShapeAlignment.Right => target - shape.Width,
                OdfShapeAlignment.Top => target,
                OdfShapeAlignment.Middle => target - (shape.Height / 2d),
                OdfShapeAlignment.Bottom => target - shape.Height,
                _ => throw new ArgumentOutOfRangeException(nameof(alignment))
            };
            SetPosition(shape, alignment <= OdfShapeAlignment.Right, value, result);
        }

        return result;
    }

    /// <summary>
    /// Distributes the specified shapes with equal gaps between their bounds.
    /// 依圖形邊界以相等間距分布指定圖形。
    /// </summary>
    /// <param name="shapeIds">The drawing shape identifiers. / 繪圖圖形識別碼。</param>
    /// <param name="distribution">The requested distribution direction. / 要套用的分布方向。</param>
    /// <returns>A structured layout result. / 結構化版面配置結果。</returns>
    public OdfShapeLayoutResult DistributeShapes(IEnumerable<string> shapeIds, OdfShapeDistribution distribution)
    {
        List<ShapeBounds> shapes = ResolveBounds(shapeIds, out OdfShapeLayoutResult result);
        if (shapes.Count < 3)
            return result;

        bool horizontal = distribution switch
        {
            OdfShapeDistribution.Horizontal => true,
            OdfShapeDistribution.Vertical => false,
            _ => throw new ArgumentOutOfRangeException(nameof(distribution))
        };
        shapes.Sort((left, right) => (horizontal ? left.X : left.Y).CompareTo(horizontal ? right.X : right.Y));
        double start = horizontal ? shapes[0].X : shapes[0].Y;
        double end = horizontal
            ? shapes[shapes.Count - 1].X + shapes[shapes.Count - 1].Width
            : shapes[shapes.Count - 1].Y + shapes[shapes.Count - 1].Height;
        double occupied = shapes.Sum(shape => horizontal ? shape.Width : shape.Height);
        double gap = (end - start - occupied) / (shapes.Count - 1);
        double cursor = start;

        foreach (ShapeBounds shape in shapes)
        {
            SetPosition(shape, horizontal, cursor, result);
            cursor += (horizontal ? shape.Width : shape.Height) + gap;
        }

        return result;
    }

    private List<ShapeBounds> ResolveBounds(IEnumerable<string> shapeIds, out OdfShapeLayoutResult result)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(shapeIds, nameof(shapeIds));

        result = new OdfShapeLayoutResult();
        var shapes = new List<ShapeBounds>();
        foreach (string id in shapeIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
        {
            OdfNode? node = FindShapeNode(Node, id);
            if (node is null)
            {
                result.MissingShapeIds.Add(id);
                continue;
            }

            if (!TryReadCentimeters(node, "x", out double x) ||
                !TryReadCentimeters(node, "y", out double y) ||
                !TryReadCentimeters(node, "width", out double width) ||
                !TryReadCentimeters(node, "height", out double height))
            {
                result.InvalidGeometryShapeIds.Add(id);
                continue;
            }

            shapes.Add(new ShapeBounds(id, node, x, y, width, height));
        }
        return shapes;
    }

    private static bool TryReadCentimeters(OdfNode node, string localName, out double value)
    {
        if (OdfLength.TryParse(node.GetAttribute(localName, OdfNamespaces.Svg), out OdfLength length))
        {
            value = length.ToCentimeters();
            return true;
        }
        value = 0;
        return false;
    }

    private static void SetPosition(ShapeBounds shape, bool horizontal, double value, OdfShapeLayoutResult result)
    {
        double current = horizontal ? shape.X : shape.Y;
        if (Math.Abs(current - value) <= 0.000001d)
            return;
        shape.Node.SetAttribute(horizontal ? "x" : "y", OdfNamespaces.Svg, OdfLength.FromCentimeters(value).ToString(), "svg");
        result.UpdatedShapeIds.Add(shape.Id);
    }

    private sealed record ShapeBounds(string Id, OdfNode Node, double X, double Y, double Width, double Height);
}
