using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Core;
/// <summary>
/// Adds protected helpers for ODF settings and metadata nodes.
/// 提供 ODF 設定與中繼資料節點的受保護輔助方法。
/// </summary>


public abstract partial class OdfDocument
{
    #region Helper Methods

    /// <summary>
    /// Executes the FindOrCreateMetaRoot operation.
    /// 尋找或建立 office:meta 根節點。
    /// </summary>
    /// <returns>office:meta 節點</returns>
    protected OdfNode FindOrCreateMetaRoot()
        => OdfDocumentMetadataEngine.FindOrCreateMetaRoot(MetaDom);

    /// <summary>
    /// Executes the FindSettingsConfigItem operation.
    /// 尋找指定名稱的設定專案。
    /// </summary>
    /// <param name="name">設定專案名稱</param>
    /// <returns>設定專案節點；若不存在則為 <see langword="null"/></returns>
    protected OdfNode? FindSettingsConfigItem(string name)
        => OdfDocumentSettingsEngine.FindSettingsConfigItem(SettingsDom, name);

    /// <summary>
    /// Executes the FindOrCreateSettingsNode operation.
    /// 尋找或建立指定名稱的設定集合節點。
    /// </summary>
    /// <param name="root">設定 DOM 根節點</param>
    /// <param name="name">設定集合名稱</param>
    /// <returns>設定集合節點</returns>
    protected OdfNode FindOrCreateSettingsNode(OdfNode root, string name)
        => OdfDocumentSettingsEngine.FindOrCreateSettingsNode(root, name);

    /// <summary>
    /// Executes the FindSettingsNode operation.
    /// 尋找指定名稱的設定集合節點。
    /// </summary>
    /// <param name="root">設定 DOM 根節點</param>
    /// <param name="name">設定集合名稱</param>
    /// <returns>設定集合節點；若不存在則為 <see langword="null"/></returns>
    protected OdfNode? FindSettingsNode(OdfNode root, string name)
        => OdfDocumentSettingsEngine.FindSettingsNode(root, name);

    /// <summary>
    /// Executes the FindOrCreateMapNode operation.
    /// 尋找或建立設定 map 節點。
    /// </summary>
    /// <param name="setNode">設定集合節點</param>
    /// <param name="name">map 名稱</param>
    /// <returns>設定 map 節點</returns>
    protected OdfNode FindOrCreateMapNode(OdfNode setNode, string name)
        => OdfDocumentSettingsEngine.FindOrCreateMapNode(setNode, name);

    /// <summary>
    /// Executes the FindOrCreateMapEntryNode operation.
    /// 尋找或建立設定 map entry 節點。
    /// </summary>
    /// <param name="mapNode">設定 map 節點</param>
    /// <returns>設定 map entry 節點</returns>
    protected OdfNode FindOrCreateMapEntryNode(OdfNode mapNode)
        => OdfDocumentSettingsEngine.FindOrCreateMapEntryNode(mapNode);

    /// <summary>
    /// Executes the FindOrCreateConfigItemNode operation.
    /// 尋找或建立設定專案節點。
    /// </summary>
    /// <param name="entryNode">設定 map entry 節點</param>
    /// <param name="name">設定專案名稱</param>
    /// <param name="type">設定專案類型</param>
    /// <returns>設定專案節點</returns>
    protected OdfNode FindOrCreateConfigItemNode(OdfNode entryNode, string name, string type)
        => OdfDocumentSettingsEngine.FindOrCreateConfigItemNode(entryNode, name, type);

    /// <summary>
    /// Gets or sets the external link update mode stored in document settings.
    /// 取得或設定文件設定中儲存的外部連結更新模式。
    /// </summary>
    public int LinkUpdateMode
    {
        get
        {
            var item = FindSettingsConfigItem("LinkUpdateMode");
            if (item != null && int.TryParse(item.TextContent, out var val))
                return val;
            return 2; // 預設為 2 (On request)
        }
        set
        {
            OdfDocumentSettingsEngine.SetLinkUpdateMode(SettingsDom, value, ContentKind == OdfDocumentKind.Spreadsheet);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether spreadsheet formulas are recalculated automatically.
    /// 取得或設定是否自動重新計算試算表公式。
    /// </summary>
    public bool AutoCalculate
    {
        get
        {
            var item = FindSettingsConfigItem("AutoCalculate");
            if (item != null && bool.TryParse(item.TextContent, out var val))
                return val;
            return true; // 預設為 true
        }
        set
        {
            OdfDocumentSettingsEngine.SetAutoCalculate(SettingsDom, value);
        }
    }

    #endregion
}

