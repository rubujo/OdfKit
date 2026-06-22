using System.Collections.Generic;
using System.IO;

namespace OdfKit.Core;

public sealed partial class OdfPackage
{
    #region Public API

    /// <summary>
    /// 檢查封裝中是否包含指定名稱的專案。
    /// </summary>
    /// <param name="name">專案的相對路徑名稱</param>
    /// <returns>若專案存在則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public bool HasEntry(string name)
        => OdfPackageEntryAccessEngine.HasEntry(EntryCollaborators, name);

    /// <summary>
    /// 提供 ODF 封裝中實體專案的基本資訊。
    /// </summary>
    public class OdfPackageEntryInfo
    {
        /// <summary>
        /// 取得專案的相對路徑。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 初始化 <see cref="OdfPackageEntryInfo"/> 類別的新執行個體。
        /// </summary>
        /// <param name="path">專案的相對路徑</param>
        public OdfPackageEntryInfo(string path) => Path = path;
    }

    /// <summary>
    /// 取得封裝中所有實體專案的資訊集合。
    /// </summary>
    /// <returns>所有專案的資訊集合</returns>
    public IEnumerable<OdfPackageEntryInfo> GetEntries()
        => OdfPackageEntryAccessEngine.GetEntries(EntryCollaborators);

    /// <summary>
    /// 讀取指定路徑專案的完整內容位元組。
    /// </summary>
    /// <param name="path">專案的相對路徑名稱</param>
    /// <returns>專案的位元組陣列內容</returns>
    public byte[] ReadEntry(string path)
        => OdfPackageEntryAccessEngine.ReadEntry(EntryCollaborators, path);

    /// <summary>
    /// 將目前 ODF 封裝儲存到指定的目標資料流中。
    /// </summary>
    /// <param name="stream">要寫入的目標資料流</param>
    /// <param name="options">單次儲存設定選項；若為 <see langword="null"/>，則使用封裝預設選項</param>
    public void Save(Stream stream, OdfSaveOptions? options = null)
    {
        SaveToStream(stream, options);
    }

    /// <summary>
    /// 取得指定專案的唯讀資料流。
    /// </summary>
    /// <param name="name">專案的相對路徑名稱</param>
    /// <returns>代表專案內容的資料流</returns>
    public Stream GetEntryStream(string name)
        => OdfPackageEntryAccessEngine.GetEntryStream(EntryCollaborators, name);

    /// <summary>
    /// 將指定的位元組內容寫入或覆寫封裝中的專案。
    /// </summary>
    /// <param name="name">專案的相對路徑名稱</param>
    /// <param name="content">要寫入的位元組內容</param>
    /// <param name="mediaType">專案的 MIME 媒體類型</param>
    public void WriteEntry(string name, byte[] content, string mediaType)
        => OdfPackageEntryAccessEngine.WriteEntry(EntryCollaborators, name, content, mediaType);

    /// <summary>
    /// 將指定的資料流內容寫入或覆寫封裝中的專案。
    /// </summary>
    /// <param name="name">專案的相對路徑名稱</param>
    /// <param name="contentStream">要寫入的內容來源資料流</param>
    /// <param name="mediaType">專案的 MIME 媒體類型</param>
    public void WriteEntry(string name, Stream contentStream, string mediaType)
        => OdfPackageEntryAccessEngine.WriteEntry(EntryCollaborators, name, contentStream, mediaType);

    /// <summary>
    /// 從封裝中移除指定的專案。
    /// </summary>
    /// <param name="name">要移除的專案相對路徑名稱</param>
    public void RemoveEntry(string name)
        => OdfPackageEntryAccessEngine.RemoveEntry(EntryCollaborators, name);

    /// <summary>
    /// 清理封裝中未被參照的圖片等媒體檔案。
    /// </summary>
    /// <param name="referencedMediaPaths">所有目前正被參照的媒體檔案路徑集合</param>
    /// <remarks>
    /// 此方法僅依路徑清單比對移除 <c>Pictures/</c> 下的 ZIP 媒體專案，不會檢查或同步移除
    /// <c>content.xml</c>／<c>styles.xml</c> 中殘留的 <c>draw:image</c> DOM 參照節點。
    /// 呼叫端必須自行確保 <paramref name="referencedMediaPaths"/> 與目前 DOM 實際參照狀態一致，
    /// 否則殘留的 DOM 參照會指向已被刪除的媒體專案而形成懸空連結，可能導致真實 ODF 應用程式
    /// （例如 LibreOffice）拒絕開啟整份文件。
    /// </remarks>
    public void PruneUnusedMedia(IEnumerable<string> referencedMediaPaths)
        => OdfPackageEntryAccessEngine.PruneUnusedMedia(EntryCollaborators, referencedMediaPaths);

    /// <summary>
    /// 設定 ODF 封裝的主要 MIME 媒體類型。
    /// </summary>
    /// <param name="mimetype">媒體類型字串</param>
    public void SetMimeType(string mimetype)
        => OdfPackageEntryAccessEngine.SetMimeType(EntryCollaborators, mimetype);

    #endregion

    #region Embedded Objects Extraction

    /// <summary>
    /// 取得此封裝中所內嵌的 ODF 物件資料夾路徑清單。
    /// </summary>
    /// <returns>內嵌物件路徑的集合</returns>
    public IEnumerable<string> GetEmbeddedObjects()
        => OdfPackageEntryAccessEngine.GetEmbeddedObjects(EntryCollaborators);

    /// <summary>
    /// 擷取內嵌物件的主要內容 XML 資料流。
    /// </summary>
    /// <param name="objectName">內嵌物件的路徑名稱</param>
    /// <returns>內嵌物件內容的資料流</returns>
    public Stream ExtractObjectStream(string objectName)
        => OdfPackageEntryAccessEngine.ExtractObjectStream(EntryCollaborators, objectName);

    #endregion
}
