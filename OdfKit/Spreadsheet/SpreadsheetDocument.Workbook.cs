using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class SpreadsheetDocument
{
    #region Workbook & Sheet Management

    /// <summary>
    /// 新增指定名稱的工作表。
    /// </summary>
    /// <param name="name">工作表名稱</param>
    /// <returns>新增的 <see cref="OdfTableSheet"/> 執行個體</returns>
    public OdfTableSheet AddSheet(string name)
    {
        var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
        table.SetAttribute("name", OdfNamespaces.Table, name, "table");
        SheetsRoot.AppendChild(table);
        return new OdfTableSheet(table, this);
    }

    /// <summary>
    /// 取得指定名稱的工作表。
    /// </summary>
    /// <param name="name">工作表名稱</param>
    /// <returns>找不到則傳回 null</returns>
    public OdfTableSheet? GetSheet(string name)
    {
        foreach (var child in SheetsRoot.Children)
        {
            if (child.LocalName == "table" && child.NamespaceUri == OdfNamespaces.Table &&
                child.GetAttribute("name", OdfNamespaces.Table) == name)
            {
                return new OdfTableSheet(child, this);
            }
        }
        return null;
    }

    /// <summary>
    /// 取得目前活頁簿中所有的工作表。
    /// </summary>
    /// <returns>工作表唯讀清單</returns>
    public IReadOnlyList<OdfTableSheet> GetSheets()
    {
        var list = new List<OdfTableSheet>();
        foreach (var child in SheetsRoot.Children)
        {
            if (child.LocalName == "table" && child.NamespaceUri == OdfNamespaces.Table)
            {
                list.Add(new OdfTableSheet(child, this));
            }
        }
        return list;
    }

    /// <summary>
    /// 取得一個值，指出活頁簿結構是否受到保護。
    /// </summary>
    public bool WorkbookStructureProtected
    {
        get
        {
            var val = FindSettingsConfigItem("StructureProtected");
            return val is not null && val.TextContent == "true";
        }
    }

    /// <summary>
    /// 啟用活頁簿保護，並設定雜湊後的保護密碼。
    /// </summary>
    /// <param name="password">密碼明文</param>
    public void ProtectWorkbook(string password)
    {
        byte[] salt = new byte[16];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        byte[] hash = OdfEncryption.Pbkdf2(passwordBytes, salt, 50000, 32, "sha256");

        var docSettings = FindOrCreateSettingsNode(SettingsDom, "ooo:document-settings");

        OdfNode? mapNode = null;
        foreach (var child in docSettings.Children)
        {
            if (child.LocalName == "config-item-map-named" && child.GetAttribute("name", OdfNamespaces.Config) == "WorkbookSettings")
            {
                mapNode = child;
                break;
            }
        }
        if (mapNode is null)
        {
            mapNode = OdfNodeFactory.CreateElement("config-item-map-named", OdfNamespaces.Config, "config");
            mapNode.SetAttribute("name", OdfNamespaces.Config, "WorkbookSettings", "config");
            docSettings.AppendChild(mapNode);
        }

        OdfNode? entry = null;
        if (mapNode.Children.Count > 0)
        {
            entry = mapNode.Children[0];
        }
        else
        {
            entry = OdfNodeFactory.CreateElement("config-item-map-entry", OdfNamespaces.Config, "config");
            mapNode.AppendChild(entry);
        }

        var itemProt = FindOrCreateConfigItemNode(entry, "StructureProtected", "boolean");
        itemProt.TextContent = "true";

        var itemKey = FindOrCreateConfigItemNode(entry, "WorkbookProtectionKey", "string");
        itemKey.TextContent = Convert.ToBase64String(hash);

        var itemAlgo = FindOrCreateConfigItemNode(entry, "WorkbookProtectionKeyDigestAlgorithm", "string");
        itemAlgo.TextContent = "http://www.w3.org/2001/04/xmlenc#sha256";

        var itemSalt = FindOrCreateConfigItemNode(entry, "WorkbookProtectionKeyDigestSalt", "string");
        itemSalt.TextContent = Convert.ToBase64String(salt);

        var itemDerivation = FindOrCreateConfigItemNode(entry, "WorkbookProtectionKeyDerivation", "string");
        itemDerivation.TextContent = "PBKDF2-SHA256-50000";
    }

    /// <summary>
    /// 驗證指定的活頁簿密碼是否正確。
    /// </summary>
    /// <param name="password">要驗證的密碼</param>
    /// <returns>若驗證成功則為 true，否則為 false</returns>
    public bool VerifyWorkbookPassword(string password)
    {
        if (!WorkbookStructureProtected)
            return true;

        var docSettings = FindSettingsNode(SettingsDom, "ooo:document-settings");
        if (docSettings is null)
            return false;

        OdfNode? mapNode = null;
        foreach (var child in docSettings.Children)
        {
            if (child.LocalName == "config-item-map-named" && child.GetAttribute("name", OdfNamespaces.Config) == "WorkbookSettings")
            {
                mapNode = child;
                break;
            }
        }
        if (mapNode is null || mapNode.Children.Count == 0)
            return false;
        var entry = mapNode.Children[0];

        string? keyStr = FindConfigItemValue(entry, "WorkbookProtectionKey");
        string? algo = FindConfigItemValue(entry, "WorkbookProtectionKeyDigestAlgorithm");
        string? saltStr = FindConfigItemValue(entry, "WorkbookProtectionKeyDigestSalt");
        string? derivation = FindConfigItemValue(entry, "WorkbookProtectionKeyDerivation");

        if (keyStr is null || algo is null || saltStr is null)
            return false;

        byte[] salt = Convert.FromBase64String(saltStr);
        byte[] expectedHash = Convert.FromBase64String(keyStr);
        byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

        byte[] input = new byte[salt.Length + passwordBytes.Length];
        Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, input, salt.Length, passwordBytes.Length);

        byte[] actualHash;
        if (derivation == "PBKDF2-SHA256-50000" &&
            (algo == "http://www.w3.org/2001/04/xmlenc#sha256" || algo == "http://www.w3.org/2000/09/xmldsig#sha256"))
        {
            actualHash = OdfEncryption.Pbkdf2(passwordBytes, salt, 50000, 32, "sha256");
        }
        else if (string.IsNullOrEmpty(derivation) &&
                 (algo == "http://www.w3.org/2001/04/xmlenc#sha256" || algo == "http://www.w3.org/2000/09/xmldsig#sha256"))
        {
            // 向下相容：舊格式使用 SHA-256 單次雜湊
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                actualHash = sha.ComputeHash(input);
            }
        }
        else
        {
            return false;
        }

        return CompareBytes(expectedHash, actualHash);
    }

    private string? FindConfigItemValue(OdfNode entry, string name)
    {
        foreach (var child in entry.Children)
        {
            if (child.LocalName == "config-item" && child.GetAttribute("name", OdfNamespaces.Config) == name)
            {
                return child.TextContent;
            }
        }
        return null;
    }

    private static bool CompareBytes(byte[] a, byte[] b)
    {
        return OdfEncryption.ByteArrayEquals(a, b);
    }

    /// <summary>
    /// 合併來源文件的內容節點。
    /// </summary>
    /// <param name="sourceDoc">來源文件</param>
    /// <param name="options">合併選項</param>
    /// <param name="renameMap">重命名對照表</param>
    /// <exception cref="ArgumentException">當來源文件類型不正確時擲出</exception>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var srcSpreadsheet = sourceDoc as SpreadsheetDocument ?? throw new ArgumentException("Source document must be a SpreadsheetDocument.");

        foreach (var child in srcSpreadsheet.SheetsRoot.Children)
        {
            if (child.NodeType == OdfNodeType.Element)
            {
                var imported = OdfNode.ImportNode(child, srcSpreadsheet.Package, Package);
                RemapStylesInNodes(imported, renameMap);
                SheetsRoot.AppendChild(imported);
            }
        }
    }

    #endregion
}
