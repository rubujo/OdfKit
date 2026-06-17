using System.Collections.Generic;
using System.Linq;

namespace OdfKit.Drawing;

public partial class DrawingDocument
{
    /// <summary>
    /// 取得文件中所有繪圖頁面的路徑圖形摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPathInfo> GetPaths() =>
        GetPagesSnapshot().SelectMany(p => p.GetPaths()).ToList().AsReadOnly();

    /// <summary>
    /// 取得文件中所有繪圖頁面的連接線摘要清單。
    /// </summary>
    public IReadOnlyList<OdfConnectorInfo> GetConnectors() =>
        GetPagesSnapshot().SelectMany(p => p.GetConnectors()).ToList().AsReadOnly();

    /// <summary>
    /// 取得文件中所有繪圖頁面的多邊形圖形摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPolygonInfo> GetPolygons() =>
        GetPagesSnapshot().SelectMany(p => p.GetPolygons()).ToList().AsReadOnly();

    /// <summary>
    /// 取得文件中所有繪圖頁面的自定義幾何圖形摘要清單。
    /// </summary>
    public IReadOnlyList<OdfCustomShapeInfo> GetCustomShapes() =>
        GetPagesSnapshot().SelectMany(p => p.GetCustomShapes()).ToList().AsReadOnly();

    /// <summary>
    /// 取得文件中所有繪圖頁面的群組圖形摘要清單。
    /// </summary>
    public IReadOnlyList<OdfGroupInfo> GetGroups() =>
        GetPagesSnapshot().SelectMany(p => p.GetGroups()).ToList().AsReadOnly();

    /// <summary>
    /// 取得文件中所有繪圖頁面的圖層摘要清單。
    /// </summary>
    public IReadOnlyList<OdfLayerInfo> GetLayers() =>
        GetPagesSnapshot().SelectMany(p => p.GetLayers()).ToList().AsReadOnly();

    /// <summary>
    /// 取得文件中所有繪圖頁面的文字方塊摘要清單。
    /// </summary>
    public IReadOnlyList<OdfDrawTextBoxInfo> GetTextBoxes() =>
        GetPagesSnapshot().SelectMany(p => p.GetTextBoxes()).ToList().AsReadOnly();

    /// <summary>
    /// 取得文件中所有繪圖頁面的圖片摘要清單。
    /// </summary>
    public IReadOnlyList<OdfDrawPictureInfo> GetPictures() =>
        GetPagesSnapshot().SelectMany(p => p.GetPictures()).ToList().AsReadOnly();
}
