using System.Collections.Generic;

namespace OdfKit.Drawing;

public partial class OdfDrawPage
{
    /// <summary>
    /// 取得此繪圖頁面上所有路徑圖形的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPathInfo> GetPaths() =>
        OdfDrawPageShapeReadEngine.GetPaths(this);

    /// <summary>
    /// 取得此繪圖頁面上所有連接線的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfConnectorInfo> GetConnectors() =>
        OdfDrawPageShapeReadEngine.GetConnectors(this);

    /// <summary>
    /// 取得此繪圖頁面上所有多邊形圖形的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPolygonInfo> GetPolygons() =>
        OdfDrawPageShapeReadEngine.GetPolygons(this);

    /// <summary>
    /// 取得此繪圖頁面上所有自定義幾何圖形的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfCustomShapeInfo> GetCustomShapes() =>
        OdfDrawPageShapeReadEngine.GetCustomShapes(this);

    /// <summary>
    /// 取得此繪圖頁面上所有群組圖形的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfGroupInfo> GetGroups() =>
        OdfDrawPageShapeReadEngine.GetGroups(this);
}
