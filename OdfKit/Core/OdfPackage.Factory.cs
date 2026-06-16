using System;
using System.IO;
using OdfKit.DOM;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Factory Methods


    /// <summary>
    /// 從指定的檔案路徑開啟既有的 ODF 封裝。
    /// </summary>
    /// <param name="path">ODF 檔案的路徑</param>
    /// <param name="options">載入選項</param>
    /// <returns>開啟的 <see cref="OdfPackage"/> 執行個體</returns>
    public static OdfPackage Open(string path, OdfLoadOptions? options = null)
    {
        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        OdfPackage package = new(OdfPackageMode.ReadWrite, stream, false, options, null);
        try
        {
            package.InitializeLoad();
            return package;
        }
        catch
        {
            package.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 從指定的資料流開啟既有的 ODF 封裝。
    /// </summary>
    /// <param name="stream">包含 ODF 封裝資料的資料流</param>
    /// <param name="leaveOpen">若在處置封裝後保持資料流開啟，則為 <see langword="true"/>；否則為 <see langword="false"/></param>
    /// <param name="options">載入選項</param>
    /// <returns>開啟的 <see cref="OdfPackage"/> 執行個體</returns>
    public static OdfPackage Open(Stream stream, bool leaveOpen = false, OdfLoadOptions? options = null)
    {
        OdfPackage package = new(OdfPackageMode.ReadWrite, stream, leaveOpen, options, null);
        try
        {
            package.InitializeLoad();
            return package;
        }
        catch
        {
            if (!leaveOpen)
                stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 在指定的檔案路徑建立一個新的 ODF 封裝。
    /// </summary>
    /// <param name="path">要建立的檔案路徑</param>
    /// <param name="options">儲存與加密選項</param>
    /// <returns>建立的 <see cref="OdfPackage"/> 執行個體</returns>
    public static OdfPackage Create(string path, OdfSaveOptions? options = null)
    {
        FileStream stream = new(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        return new OdfPackage(OdfPackageMode.Create, stream, false, null, options);
    }

    /// <summary>
    /// 在指定的資料流建立一個新的 ODF 封裝。
    /// </summary>
    /// <param name="stream">要寫入 ODF 封裝的資料流</param>
    /// <param name="leaveOpen">若在處置封裝後保持資料流開啟，則為 <see langword="true"/>；否則為 <see langword="false"/></param>
    /// <param name="options">儲存與加密選項</param>
    /// <returns>建立的 <see cref="OdfPackage"/> 執行個體</returns>
    public static OdfPackage Create(Stream stream, bool leaveOpen = false, OdfSaveOptions? options = null)
    {
        return new OdfPackage(OdfPackageMode.Create, stream, leaveOpen, null, options);
    }


    #endregion
}
