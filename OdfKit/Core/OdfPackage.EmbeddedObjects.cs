using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.DOM;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Embedded Objects Extraction


    /// <summary>
    /// 取得此封裝中所內嵌的 ODF 物件資料夾路徑清單。
    /// </summary>
    /// <returns>內嵌物件路徑的集合</returns>
    public IEnumerable<string> GetEmbeddedObjects()
    {
        List<string> list = [];
        foreach (var kvp in _manifest)
        {
            // Embedded objects have media types starting with application/vnd.oasis.opendocument.*
            // and full paths that represent folders (registered in manifest with '/' at end or as parents)
            if (kvp.Key != "/" && kvp.Value.StartsWith("application/vnd.oasis.opendocument.", StringComparison.Ordinal))
            {
                list.Add(kvp.Key);
            }
        }
        return list;
    }

    /// <summary>
    /// 擷取內嵌物件的主要內容 XML 資料流。
    /// </summary>
    /// <param name="objectName">內嵌物件的路徑名稱</param>
    /// <returns>內嵌物件內容的資料流</returns>
    public Stream ExtractObjectStream(string objectName)
    {
        // 內嵌物件資料流為資料夾結構。我們會在其路徑（如 objectName/）下尋找相關項目。
        // 一般而言，內嵌物件在其子路徑下會包含 content.xml、styles.xml 等項目。
        // 若使用者要求物件主體本身，則擲出例外狀況或傳回子封裝。
        // 對於一般的內嵌擷取，使用者可透過 GetEntryStream 並搭配 objectName + "/content.xml" 來取得。
        string path = SanitizeEntryName(objectName);
        return GetEntryStream(path + "/content.xml");
    }


    #endregion
}
