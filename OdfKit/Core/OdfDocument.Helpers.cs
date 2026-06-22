using OdfKit.Compliance;
using OdfKit.DOM;

namespace OdfKit.Core;


public abstract partial class OdfDocument
{
    #region Helper Methods

    /// <summary>
    /// 尋找或建立 office:meta 根節點。
    /// </summary>
    /// <returns>office:meta 節點</returns>
    protected OdfNode FindOrCreateMetaRoot()
        => OdfDocumentMetadataEngine.FindOrCreateMetaRoot(MetaDom);

    /// <summary>
    /// 尋找指定名稱的設定專案。
    /// </summary>
    /// <param name="name">設定專案名稱</param>
    /// <returns>設定專案節點；若不存在則為 <see langword="null"/></returns>
    protected OdfNode? FindSettingsConfigItem(string name)
        => OdfDocumentSettingsEngine.FindSettingsConfigItem(SettingsDom, name);

    /// <summary>
    /// 尋找或建立指定名稱的設定集合節點。
    /// </summary>
    /// <param name="root">設定 DOM 根節點</param>
    /// <param name="name">設定集合名稱</param>
    /// <returns>設定集合節點</returns>
    protected OdfNode FindOrCreateSettingsNode(OdfNode root, string name)
        => OdfDocumentSettingsEngine.FindOrCreateSettingsNode(root, name);

    /// <summary>
    /// 尋找指定名稱的設定集合節點。
    /// </summary>
    /// <param name="root">設定 DOM 根節點</param>
    /// <param name="name">設定集合名稱</param>
    /// <returns>設定集合節點；若不存在則為 <see langword="null"/></returns>
    protected OdfNode? FindSettingsNode(OdfNode root, string name)
        => OdfDocumentSettingsEngine.FindSettingsNode(root, name);

    /// <summary>
    /// 尋找或建立設定 map 節點。
    /// </summary>
    /// <param name="setNode">設定集合節點</param>
    /// <param name="name">map 名稱</param>
    /// <returns>設定 map 節點</returns>
    protected OdfNode FindOrCreateMapNode(OdfNode setNode, string name)
        => OdfDocumentSettingsEngine.FindOrCreateMapNode(setNode, name);

    /// <summary>
    /// 尋找或建立設定 map entry 節點。
    /// </summary>
    /// <param name="mapNode">設定 map 節點</param>
    /// <returns>設定 map entry 節點</returns>
    protected OdfNode FindOrCreateMapEntryNode(OdfNode mapNode)
        => OdfDocumentSettingsEngine.FindOrCreateMapEntryNode(mapNode);

    /// <summary>
    /// 尋找或建立設定專案節點。
    /// </summary>
    /// <param name="entryNode">設定 map entry 節點</param>
    /// <param name="name">設定專案名稱</param>
    /// <param name="type">設定專案類型</param>
    /// <returns>設定專案節點</returns>
    protected OdfNode FindOrCreateConfigItemNode(OdfNode entryNode, string name, string type)
        => OdfDocumentSettingsEngine.FindOrCreateConfigItemNode(entryNode, name, type);

    /// <summary>
    /// 取得或設定外部連結更新模式。
    /// 0 表示從不更新，1 表示自動更新，2 表示載入時確認（詢問）。
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
    /// 取得或設定是否自動計算公式（試算表專屬，但可全域讀寫）。
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

