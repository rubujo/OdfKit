using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Core;

public abstract partial class OdfDocument
{
    #region Zoom & View Settings (settings.xml)


    /// <summary>
    /// 取得或設定文件檢視縮放百分比。
    /// </summary>
    public double ZoomLevel
    {
        get => GetZoomLevelInternal();
        set => SetZoomLevelInternal(value);
    }


    #endregion
}
