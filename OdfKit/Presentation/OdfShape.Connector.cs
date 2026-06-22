using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Presentation;

public partial class OdfShape
{
    /// <summary>
    /// 設定連接線的自訂路由頂點（<c>draw:points</c>）。
    /// </summary>
    /// <param name="points">以空白分隔的座標對字串（例如 <c>0cm 0cm 1cm 1cm</c>）</param>
    /// <returns>目前圖形執行個體</returns>
    /// <exception cref="InvalidOperationException">當圖形不是連接線時擲回</exception>
    public OdfShape SetConnectorRoutePoints(string points)
    {
        EnsureConnectorShape();

        if (string.IsNullOrWhiteSpace(points))
            Node.RemoveAttribute("points", OdfNamespaces.Draw);
        else
            Node.SetAttribute("points", OdfNamespaces.Draw, points.Trim(), "draw");

        return this;
    }

    /// <summary>
    /// 以座標集合設定連接線的自訂路由頂點。
    /// </summary>
    /// <param name="points">路由頂點座標集合</param>
    /// <returns>目前圖形執行個體</returns>
    /// <exception cref="InvalidOperationException">當圖形不是連接線時擲回</exception>
    public OdfShape SetConnectorRoutePoints(IEnumerable<(OdfLength X, OdfLength Y)> points)
    {
        if (points is null)
            throw new ArgumentNullException(nameof(points));

        string pointsStr = string.Join(
            " ",
            points.Select(point => $"{point.X} {point.Y}"));
        return SetConnectorRoutePoints(pointsStr);
    }

    private void EnsureConnectorShape()
    {
        if (Node.LocalName != "connector" || Node.NamespaceUri != OdfNamespaces.Draw)
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfShape_OnlyConnectionLineGraphics"));
    }
}
