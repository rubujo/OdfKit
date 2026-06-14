using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODF 試算表文件（ODS）。
/// </summary>
public class SpreadsheetDocument : OdfDocument
{
    private OdfWorksheetCollection? _worksheets;

    /// <summary>
    /// 取得活頁簿中所有工作表的根節點。
    /// </summary>
    public OdfNode SheetsRoot { get; private set; } = null!;

    /// <summary>
    /// 初始化 <see cref="SpreadsheetDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">Odf 套件包</param>
    public SpreadsheetDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.spreadsheet");
        }
        InitializeSheetsRoot();
    }

    /// <summary>
    /// 建立新的 ODS 試算表文件。
    /// </summary>
    /// <returns>新的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    public static SpreadsheetDocument Create()
    {
        return (SpreadsheetDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Spreadsheet);
    }

    /// <summary>
    /// 從指定路徑載入 ODS 試算表文件。
    /// </summary>
    /// <param name="path">ODS 文件路徑。</param>
    /// <returns>載入完成的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODS 試算表時擲出。</exception>
    public new static SpreadsheetDocument Load(string path)
    {
        return EnsureSpreadsheet(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 從指定資料流載入 ODS 試算表文件。
    /// </summary>
    /// <param name="stream">包含 ODS 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODS 試算表時擲出。</exception>
    public new static SpreadsheetDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureSpreadsheet(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 取得工作表集合。
    /// </summary>
    public OdfWorksheetCollection Worksheets => _worksheets ??= new OdfWorksheetCollection(this);

    private static SpreadsheetDocument EnsureSpreadsheet(OdfDocument document)
    {
        if (document is SpreadsheetDocument spreadsheet)
        {
            return spreadsheet;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是 ODS 試算表。");
    }

    private void InitializeSheetsRoot()
    {
        var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        SheetsRoot = FindOrCreateChild(body, "spreadsheet", OdfNamespaces.Office, "office");
    }

    internal OdfNode GetOrCreateSettingsItemSet(string name)
    {
        return FindOrCreateSettingsNode(SettingsDom, name);
    }

    /// <summary>
    /// 取得預設的 content.xml 內容。
    /// </summary>
    /// <returns>預設的 XML 內容字串</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:body><office:spreadsheet></office:spreadsheet></office:body></office:document-content>";
    }

    /// <summary>
    /// 取得預設的 styles.xml 內容。
    /// </summary>
    /// <returns>預設的 XML 內容字串</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:styles></office:styles><office:automatic-styles></office:automatic-styles><office:master-styles></office:master-styles></office:document-styles>";
    }

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

        var docSettings = FindOrCreateSettingsNode(SettingsDom, "document-settings");
        
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
        if (!WorkbookStructureProtected) return true;

        var docSettings = FindSettingsNode(SettingsDom, "document-settings");
        if (docSettings is null) return false;

        OdfNode? mapNode = null;
        foreach (var child in docSettings.Children)
        {
            if (child.LocalName == "config-item-map-named" && child.GetAttribute("name", OdfNamespaces.Config) == "WorkbookSettings")
            {
                mapNode = child;
                break;
            }
        }
        if (mapNode is null || mapNode.Children.Count == 0) return false;
        var entry = mapNode.Children[0];

        string? keyStr = FindConfigItemValue(entry, "WorkbookProtectionKey");
        string? algo = FindConfigItemValue(entry, "WorkbookProtectionKeyDigestAlgorithm");
        string? saltStr = FindConfigItemValue(entry, "WorkbookProtectionKeyDigestSalt");
        string? derivation = FindConfigItemValue(entry, "WorkbookProtectionKeyDerivation");

        if (keyStr is null || algo is null || saltStr is null) return false;

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

    /// <summary>
    /// 新增命名範圍。
    /// </summary>
    /// <param name="name">命名範圍的名稱</param>
    /// <param name="range">儲存格範圍</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddNamedRange(string name, OdfCellRange range, OdfCellAddress? baseCell = null)
    {
        var namedExpressions = FindOrCreateChild(SheetsRoot, "named-expressions", OdfNamespaces.Table, "table");
        var namedRange = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
        namedRange.SetAttribute("name", OdfNamespaces.Table, name, "table");
        namedRange.SetAttribute("cell-range-address", OdfNamespaces.Table, range.ToOdfString(false), "table");
        if (baseCell.HasValue)
        {
            namedRange.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
        }
        namedExpressions.AppendChild(namedRange);
    }

    /// <summary>
    /// 新增具名運算式。
    /// </summary>
    /// <param name="name">具名運算式的名稱</param>
    /// <param name="expression">公式運算式字串</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddNamedExpression(string name, string expression, OdfCellAddress? baseCell = null)
    {
        var namedExpressions = FindOrCreateChild(SheetsRoot, "named-expressions", OdfNamespaces.Table, "table");
        var namedExpr = new OdfNode(OdfNodeType.Element, "named-expression", OdfNamespaces.Table, "table");
        namedExpr.SetAttribute("name", OdfNamespaces.Table, name, "table");
        namedExpr.SetAttribute("expression", OdfNamespaces.Table, expression, "table");
        if (baseCell.HasValue)
        {
            namedExpr.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
        }
        namedExpressions.AppendChild(namedExpr);
    }

    /// <summary>
    /// 新增資料庫範圍。
    /// </summary>
    /// <param name="name">資料庫範圍名稱</param>
    /// <param name="range">目標儲存格範圍</param>
    /// <returns>新增的 <see cref="OdfDatabaseRange"/> 執行個體</returns>
    public OdfDatabaseRange AddDatabaseRange(string name, OdfCellRange range)
    {
        var databaseRanges = FindOrCreateChild(SheetsRoot, "database-ranges", OdfNamespaces.Table, "table");
        var dbRangeNode = new OdfNode(OdfNodeType.Element, "database-range", OdfNamespaces.Table, "table");
        dbRangeNode.SetAttribute("name", OdfNamespaces.Table, name, "table");
        dbRangeNode.SetAttribute("target-range-address", OdfNamespaces.Table, range.ToOdfString(false), "table");
        databaseRanges.AppendChild(dbRangeNode);
        return new OdfDatabaseRange(dbRangeNode, this);
    }
}

/// <summary>
/// 提供活頁簿工作表的索引與列舉入口。
/// </summary>
public sealed class OdfWorksheetCollection : IEnumerable<OdfTableSheet>
{
    private readonly SpreadsheetDocument _document;

    /// <summary>
    /// 初始化 <see cref="OdfWorksheetCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">所屬試算表文件。</param>
    public OdfWorksheetCollection(SpreadsheetDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 取得目前工作表數量。
    /// </summary>
    public int Count => _document.GetSheets().Count;

    /// <summary>
    /// 依索引取得工作表。
    /// </summary>
    /// <param name="index">以 0 為基準的工作表索引。</param>
    /// <returns>指定索引的工作表。</returns>
    /// <exception cref="ArgumentOutOfRangeException">當索引超出範圍時擲出。</exception>
    public OdfTableSheet this[int index]
    {
        get
        {
            IReadOnlyList<OdfTableSheet> sheets = _document.GetSheets();
            if (index < 0 || index >= sheets.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return sheets[index];
        }
    }

    /// <summary>
    /// 依名稱取得工作表。
    /// </summary>
    /// <param name="name">工作表名稱。</param>
    /// <returns>指定名稱的工作表。</returns>
    /// <exception cref="KeyNotFoundException">當找不到指定工作表時擲出。</exception>
    public OdfTableSheet this[string name]
    {
        get
        {
            OdfTableSheet? sheet = _document.GetSheet(name);
            if (sheet is null)
            {
                throw new KeyNotFoundException($"找不到名稱為 '{name}' 的工作表。");
            }

            return sheet;
        }
    }

    /// <summary>
    /// 新增工作表。
    /// </summary>
    /// <param name="name">工作表名稱。</param>
    /// <returns>新增完成的工作表。</returns>
    public OdfTableSheet Add(string name)
    {
        return _document.AddSheet(name);
    }

    /// <summary>
    /// 取得工作表列舉器。
    /// </summary>
    /// <returns>工作表列舉器。</returns>
    public IEnumerator<OdfTableSheet> GetEnumerator()
    {
        return _document.GetSheets().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// 表示 ODF 試算表中的工作表。
/// </summary>
/// <remarks>
/// 初始化 <see cref="OdfTableSheet"/> 類別的新執行個體。
/// </remarks>
/// <param name="tableNode">工作表 XML 節點</param>
/// <param name="doc">試算表文件</param>
public class OdfTableSheet(OdfNode tableNode, SpreadsheetDocument doc)
{
    private OdfCellCollection? _cells;
    private OdfRowCollection? _rows;
    private OdfColumnCollection? _columns;
    private OdfRangeCollection? _ranges;

    /// <summary>
    /// 取得代表此工作表的 XML 節點。
    /// </summary>
    public OdfNode TableNode { get; } = tableNode;

    private readonly SpreadsheetDocument _doc = doc;

    internal SpreadsheetDocument Document => _doc;

    /// <summary>
    /// 取得或設定工作表的名稱。
    /// </summary>
    public string Name
    {
        get => TableNode.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
        set => TableNode.SetAttribute("name", OdfNamespaces.Table, value, "table");
    }

    /// <summary>
    /// 取得此工作表的儲存格集合。
    /// </summary>
    public OdfCellCollection Cells => _cells ??= new OdfCellCollection(this);

    /// <summary>
    /// 取得此工作表的列集合。
    /// </summary>
    public OdfRowCollection Rows => _rows ??= new OdfRowCollection(this);

    /// <summary>
    /// 取得此工作表的欄集合。
    /// </summary>
    public OdfColumnCollection Columns => _columns ??= new OdfColumnCollection(this);

    /// <summary>
    /// 取得此工作表的儲存格範圍集合。
    /// </summary>
    public OdfRangeCollection Ranges => _ranges ??= new OdfRangeCollection(this);

    /// <summary>
    /// 取得或設定工作表是否顯示。
    /// </summary>
    public bool Visible
    {
        get => TableNode.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
        set => TableNode.SetAttribute("visibility", OdfNamespaces.Table, value ? "visible" : "collapse", "table");
    }

    /// <summary>
    /// 取得一個值，指出此工作表是否啟用保護。
    /// </summary>
    public bool IsProtected
    {
        get => TableNode.GetAttribute("protected", OdfNamespaces.Table) == "true";
    }

    /// <summary>
    /// 啟用工作表保護，並設定雜湊後的密碼。
    /// </summary>
    /// <param name="password">密碼明文</param>
    public void Protect(string password)
    {
        byte[] salt = new byte[16];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        byte[] hash = OdfEncryption.Pbkdf2(passwordBytes, salt, 50000, 32, "sha256");

        TableNode.SetAttribute("protected", OdfNamespaces.Table, "true", "table");
        TableNode.SetAttribute("protection-key", OdfNamespaces.Table, Convert.ToBase64String(hash), "table");
        TableNode.SetAttribute("protection-key-digest-algorithm", OdfNamespaces.Table, "http://www.w3.org/2001/04/xmlenc#sha256", "table");
        TableNode.SetAttribute("protection-key-digest-salt", OdfNamespaces.Table, Convert.ToBase64String(salt), "table");
        TableNode.SetAttribute("protection-key-derivation", OdfNamespaces.Table, "PBKDF2-SHA256-50000", "table");
    }

    /// <summary>
    /// 驗證工作表保護密碼是否正確。
    /// </summary>
    /// <param name="password">要驗證的密碼</param>
    /// <returns>若驗證成功則為 true，否則為 false</returns>
    public bool VerifyPassword(string password)
    {
        if (!IsProtected) return true;

        string? keyStr = TableNode.GetAttribute("protection-key", OdfNamespaces.Table);
        string? algo = TableNode.GetAttribute("protection-key-digest-algorithm", OdfNamespaces.Table);
        string? saltStr = TableNode.GetAttribute("protection-key-digest-salt", OdfNamespaces.Table);
        string? derivation = TableNode.GetAttribute("protection-key-derivation", OdfNamespaces.Table);

        if (keyStr is null || algo is null || saltStr is null) return false;

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
            // 向下相容：舊格式
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

    private static bool CompareBytes(byte[] a, byte[] b)
    {
        return OdfEncryption.ByteArrayEquals(a, b);
    }

    /// <summary>
    /// 取得指定列與欄索引的儲存格。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <returns>儲存格物件</returns>
    public OdfCell GetCell(int row, int col)
    {
        var cellNode = GetOrCreateCellNode(row, col);
        return new OdfCell(cellNode, row, col, _doc);
    }

    /// <summary>
    /// 取得指定位址的儲存格。
    /// </summary>
    /// <param name="address">儲存格位址字串（例如 "A1" 或 "Sheet1.A1"）</param>
    /// <returns>儲存格物件</returns>
    /// <exception cref="FormatException">當儲存格格式無效時擲出</exception>
    public OdfCell GetCell(string address)
    {
        if (!OdfCellAddress.TryParse(address, out var addr))
        {
            throw new FormatException($"Invalid cell address: '{address}'");
        }
        return GetCell(addr.Row, addr.Column);
    }

    /// <summary>
    /// 合併指定的儲存格範圍，並可選擇性套用外框線。
    /// </summary>
    /// <param name="range">儲存格範圍</param>
    /// <param name="outerBorder">套用於合併範圍外部的外框線格式</param>
    public void MergeCells(OdfCellRange range, OdfBorder? outerBorder = null)
    {
        int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int startCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int endCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);

        var mainCell = GetCell(startRow, startCol);
        mainCell.Node.SetAttribute("number-columns-spanned", OdfNamespaces.Table, (endCol - startCol + 1).ToString(), "table");
        mainCell.Node.SetAttribute("number-rows-spanned", OdfNamespaces.Table, (endRow - startRow + 1).ToString(), "table");

        for (int r = startRow; r <= endRow; r++)
        {
            for (int c = startCol; c <= endCol; c++)
            {
                if (r == startRow && c == startCol) continue;
                
                var coveredNode = new OdfNode(OdfNodeType.Element, "covered-table-cell", OdfNamespaces.Table, "table");
                ReplaceCellNode(r, c, coveredNode);
                
                if (outerBorder.HasValue)
                {
                    var cellBorderTop = (r == startRow) ? outerBorder.Value : OdfBorder.None;
                    var cellBorderBottom = (r == endRow) ? outerBorder.Value : OdfBorder.None;
                    var cellBorderLeft = (c == startCol) ? outerBorder.Value : OdfBorder.None;
                    var cellBorderRight = (c == endCol) ? outerBorder.Value : OdfBorder.None;
                    
                    var coveredCell = new OdfCell(coveredNode, r, c, _doc);
                    coveredCell.SetBorders(cellBorderTop, cellBorderBottom, cellBorderLeft, cellBorderRight);
                }
            }
        }
    }

    /// <summary>
    /// 自動調整指定欄的寬度，根據內容長度來適配。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    public void AutoFitColumnWidth(int col)
    {
        var values = new List<string>();
        var rows = GetRowsList();
        foreach (var rowNode in rows)
        {
            var cells = GetCellsInRow(rowNode);
            if (col < cells.Count)
            {
                values.Add(cells[col].TextContent);
            }
        }

        var optimalWidth = CalculateOptimalColumnWidth(values);
        SetColumnWidth(col, optimalWidth);
    }

    /// <summary>
    /// 設定指定欄的寬度。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <param name="width">欄寬度</param>
    public void SetColumnWidth(int col, OdfLength width)
    {
        var colNode = GetOrCreateColumnNode(col);
        _doc.StyleEngine.GetOrCreateLocalStyle(colNode, "table-column").GetAttribute("name", OdfNamespaces.Style);
        _doc.StyleEngine.SetLocalStyleProperty(colNode, "table-column", "table-column-properties", "column-width", OdfNamespaces.Style, width.ToString(), "style");
    }

    private OdfLength CalculateOptimalColumnWidth(IEnumerable<string> cellValues, double fontSizePt = 10)
    {
        double maxWeight = 0;
        foreach (var value in cellValues)
        {
            double weight = 0;
            foreach (char c in value)
            {
                weight += (c <= 127) ? 1.0 : 1.85;
            }
            if (weight > maxWeight) maxWeight = weight;
        }

        double totalChars = maxWeight + 1.5;
        double widthInCm = totalChars * (fontSizePt / 10.0) * 0.22;
        return OdfLength.FromCentimeters(Math.Max(widthInCm, 1.0));
    }

    /// <summary>
    /// 新增條件格式。
    /// </summary>
    /// <param name="range">儲存格範圍</param>
    /// <param name="conditionValue">條件運算式</param>
    /// <param name="styleName">要套用的格式樣式名稱</param>
    public void AddConditionalFormat(OdfCellRange range, string conditionValue, string styleName)
    {
        const string calcextNs = OdfNamespaces.CalcExt;
        
        OdfNode? formatsNode = null;
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "conditional-formats" && child.NamespaceUri == calcextNs)
            {
                formatsNode = child;
                break;
            }
        }
        if (formatsNode is null)
        {
            formatsNode = new OdfNode(OdfNodeType.Element, "conditional-formats", calcextNs, "calcext");
            TableNode.AppendChild(formatsNode);
        }

        var format = new OdfNode(OdfNodeType.Element, "conditional-format", calcextNs, "calcext");
        var startAddr = range.StartAddress;
        if (startAddr.SheetName is null) startAddr = new OdfCellAddress(startAddr.Row, startAddr.Column, Name, startAddr.IsRowAbsolute, startAddr.IsColumnAbsolute, startAddr.IsSheetAbsolute);
        var endAddr = range.EndAddress;
        if (endAddr.SheetName is null) endAddr = new OdfCellAddress(endAddr.Row, endAddr.Column, Name, endAddr.IsRowAbsolute, endAddr.IsColumnAbsolute, endAddr.IsSheetAbsolute);

        string rangeAddr = $"{startAddr.ToOdfString(false)}:{endAddr.ToOdfString(false)}";
        format.SetAttribute("target-range-address", calcextNs, rangeAddr, "calcext");

        var condition = new OdfNode(OdfNodeType.Element, "condition", calcextNs, "calcext");
        condition.SetAttribute("value", calcextNs, conditionValue, "calcext");
        condition.SetAttribute("style-name", calcextNs, styleName, "calcext");
        format.AppendChild(condition);

        formatsNode.AppendChild(format);
    }

    /// <summary>
    /// 在工作表中新增 LibreOffice calcext 走勢圖群組。
    /// </summary>
    /// <param name="dataRange">走勢圖資料來源範圍。</param>
    /// <param name="hostCell">顯示走勢圖的儲存格位址。</param>
    /// <param name="type">走勢圖類型，預設為折線。</param>
    /// <exception cref="ArgumentNullException">當 dataRange 為 null 時拋出。</exception>
    public void AddSparklineGroup(OdfCellRange? dataRange, OdfCellAddress hostCell, SparklineType type = SparklineType.Line)
    {
        if (dataRange is null) throw new ArgumentNullException(nameof(dataRange));

        const string calcextNs = OdfNamespaces.CalcExt;

        OdfNode? groupsNode = null;
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "sparkline-groups" && child.NamespaceUri == calcextNs)
            {
                groupsNode = child;
                break;
            }
        }
        if (groupsNode is null)
        {
            groupsNode = new OdfNode(OdfNodeType.Element, "sparkline-groups", calcextNs, "calcext");
            TableNode.AppendChild(groupsNode);
        }

        var groupNode = new OdfNode(OdfNodeType.Element, "sparkline-group", calcextNs, "calcext");
        groupNode.SetAttribute("type", calcextNs, SparklineTypeToString(type), "calcext");
        groupsNode.AppendChild(groupNode);

        var sparklinesNode = new OdfNode(OdfNodeType.Element, "sparklines", calcextNs, "calcext");
        groupNode.AppendChild(sparklinesNode);

        var startAddr = dataRange.Value.StartAddress;
        if (startAddr.SheetName is null)
            startAddr = new OdfCellAddress(startAddr.Row, startAddr.Column, Name,
                startAddr.IsRowAbsolute, startAddr.IsColumnAbsolute, startAddr.IsSheetAbsolute);
        var endAddr = dataRange.Value.EndAddress;
        if (endAddr.SheetName is null)
            endAddr = new OdfCellAddress(endAddr.Row, endAddr.Column, Name,
                endAddr.IsRowAbsolute, endAddr.IsColumnAbsolute, endAddr.IsSheetAbsolute);
        var host = hostCell.SheetName is null
            ? new OdfCellAddress(hostCell.Row, hostCell.Column, Name, true, true, true)
            : hostCell;

        var sparklineNode = new OdfNode(OdfNodeType.Element, "sparkline", calcextNs, "calcext");
        sparklineNode.SetAttribute("dataRangeRef", calcextNs,
            $"{startAddr.ToOdfString(false)}:{endAddr.ToOdfString(false)}", "calcext");
        sparklineNode.SetAttribute("hostCellRef", calcextNs, host.ToOdfString(false), "calcext");
        sparklinesNode.AppendChild(sparklineNode);
    }

    private static string SparklineTypeToString(SparklineType type) => type switch
    {
        SparklineType.Column => "column",
        SparklineType.WinLoss => "stacked",
        _ => "line"
    };

    /// <summary>
    /// 新增資料庫範圍至此工作表。
    /// </summary>
    /// <param name="name">資料庫範圍名稱。</param>
    /// <param name="range">目標儲存格範圍。</param>
    /// <returns>新增的資料庫範圍。</returns>
    public OdfDatabaseRange AddDatabaseRange(string name, OdfCellRange range)
    {
        return _doc.AddDatabaseRange(name, range);
    }

    /// <summary>
    /// 凍結指定數量的上方列與左側欄。
    /// </summary>
    /// <param name="frozenRows">要凍結的列數。</param>
    /// <param name="frozenColumns">要凍結的欄數。</param>
    /// <exception cref="ArgumentOutOfRangeException">當列數或欄數小於 0 時擲出。</exception>
    public void FreezePanes(int frozenRows, int frozenColumns)
    {
        if (frozenRows < 0) throw new ArgumentOutOfRangeException(nameof(frozenRows));
        if (frozenColumns < 0) throw new ArgumentOutOfRangeException(nameof(frozenColumns));

        TableNode.SetAttribute("frozen-rows", OdfNamespaces.Table, frozenRows.ToString(CultureInfo.InvariantCulture), "table");
        TableNode.SetAttribute("frozen-columns", OdfNamespaces.Table, frozenColumns.ToString(CultureInfo.InvariantCulture), "table");

        var viewSettings = _doc.GetOrCreateSettingsItemSet("view-settings");
        var views = FindOrCreateChild(viewSettings, "config-item-map-indexed", OdfNamespaces.Config, "config");
        views.SetAttribute("name", OdfNamespaces.Config, "Views", "config");
        var viewEntry = FindOrCreateFirstChild(views, "config-item-map-entry", OdfNamespaces.Config, "config");
        var tables = FindOrCreateChild(viewEntry, "config-item-map-named", OdfNamespaces.Config, "config");
        tables.SetAttribute("name", OdfNamespaces.Config, "Tables", "config");
        var sheetEntry = FindOrCreateNamedMapEntry(tables, Name);

        SetConfigItem(sheetEntry, "HorizontalSplitMode", "short", frozenRows > 0 ? "2" : "0");
        SetConfigItem(sheetEntry, "VerticalSplitMode", "short", frozenColumns > 0 ? "2" : "0");
        SetConfigItem(sheetEntry, "HorizontalSplitPosition", "int", frozenRows.ToString(CultureInfo.InvariantCulture));
        SetConfigItem(sheetEntry, "VerticalSplitPosition", "int", frozenColumns.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 以分割模式（非凍結）分割工作表窗格。
    /// </summary>
    /// <param name="splitRow">水平分割線所在的列索引 (0 表示不分割)。</param>
    /// <param name="splitColumn">垂直分割線所在的欄索引 (0 表示不分割)。</param>
    /// <exception cref="ArgumentOutOfRangeException">當列索引或欄索引小於 0 時拋出。</exception>
    public void SplitPanes(int splitRow, int splitColumn)
    {
        if (splitRow < 0) throw new ArgumentOutOfRangeException(nameof(splitRow));
        if (splitColumn < 0) throw new ArgumentOutOfRangeException(nameof(splitColumn));

        var viewSettings = _doc.GetOrCreateSettingsItemSet("view-settings");
        var views = FindOrCreateChild(viewSettings, "config-item-map-indexed", OdfNamespaces.Config, "config");
        views.SetAttribute("name", OdfNamespaces.Config, "Views", "config");
        var viewEntry = FindOrCreateFirstChild(views, "config-item-map-entry", OdfNamespaces.Config, "config");
        var tables = FindOrCreateChild(viewEntry, "config-item-map-named", OdfNamespaces.Config, "config");
        tables.SetAttribute("name", OdfNamespaces.Config, "Tables", "config");
        var sheetEntry = FindOrCreateNamedMapEntry(tables, Name);

        SetConfigItem(sheetEntry, "HorizontalSplitMode", "short", splitRow > 0 ? "1" : "0");
        SetConfigItem(sheetEntry, "VerticalSplitMode", "short", splitColumn > 0 ? "1" : "0");
        SetConfigItem(sheetEntry, "HorizontalSplitPosition", "int", splitRow.ToString(CultureInfo.InvariantCulture));
        SetConfigItem(sheetEntry, "VerticalSplitPosition", "int", splitColumn.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 取得目前工作表的凍結窗格設定。
    /// </summary>
    public OdfFrozenPanes FrozenPanes
    {
        get
        {
            int frozenRows = ParseNonNegativeInt(TableNode.GetAttribute("frozen-rows", OdfNamespaces.Table));
            int frozenColumns = ParseNonNegativeInt(TableNode.GetAttribute("frozen-columns", OdfNamespaces.Table));
            return new OdfFrozenPanes(frozenRows, frozenColumns);
        }
    }

    /// <summary>
    /// 新增清單型資料驗證，並套用到指定範圍。
    /// </summary>
    /// <param name="range">要套用的儲存格範圍。</param>
    /// <param name="name">驗證規則名稱。</param>
    /// <param name="allowedValues">允許的值。</param>
    public void AddValidationList(OdfCellRange range, string name, params string[] allowedValues)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("驗證名稱不可空白。", nameof(name));
        if (allowedValues is null || allowedValues.Length == 0) throw new ArgumentException("驗證清單至少需要一個允許值。", nameof(allowedValues));

        var validations = FindOrCreateChild(TableNode, "content-validations", OdfNamespaces.Table, "table");
        var validation = FindOrCreateNamedChild(validations, "content-validation", name);
        validation.SetAttribute("name", OdfNamespaces.Table, name, "table");
        validation.SetAttribute("condition", OdfNamespaces.Table, BuildValidationListCondition(allowedValues), "table");
        validation.SetAttribute("allow-empty-cell", OdfNamespaces.Table, "true", "table");

        foreach (OdfCell cell in EnumerateCells(range))
        {
            cell.Node.SetAttribute("content-validation-name", OdfNamespaces.Table, name, "table");
        }
    }

    private IEnumerable<OdfCell> EnumerateCells(OdfCellRange range)
    {
        int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int startCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int endCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);

        for (int row = startRow; row <= endRow; row++)
        {
            for (int col = startCol; col <= endCol; col++)
            {
                yield return GetCell(row, col);
            }
        }
    }

    private static string BuildValidationListCondition(IEnumerable<string> values)
    {
        var quoted = new List<string>();
        foreach (string value in values)
        {
            quoted.Add("\"" + value.Replace("\"", "\"\"") + "\"");
        }

        return "cell-content-is-in-list(" + string.Join(";", quoted) + ")";
    }

    private OdfNode FindOrCreateNamedChild(OdfNode parent, string localName, string name)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName &&
                child.NamespaceUri == OdfNamespaces.Table &&
                child.GetAttribute("name", OdfNamespaces.Table) == name)
            {
                return child;
            }
        }

        var node = new OdfNode(OdfNodeType.Element, localName, OdfNamespaces.Table, "table");
        parent.AppendChild(node);
        return node;
    }

    private static OdfNode FindOrCreateFirstChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
            {
                return child;
            }
        }

        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        parent.AppendChild(node);
        return node;
    }

    private static OdfNode FindOrCreateNamedMapEntry(OdfNode parent, string name)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == "config-item-map-entry" &&
                child.NamespaceUri == OdfNamespaces.Config &&
                child.GetAttribute("name", OdfNamespaces.Config) == name)
            {
                return child;
            }
        }

        var entry = new OdfNode(OdfNodeType.Element, "config-item-map-entry", OdfNamespaces.Config, "config");
        entry.SetAttribute("name", OdfNamespaces.Config, name, "config");
        parent.AppendChild(entry);
        return entry;
    }

    private static void SetConfigItem(OdfNode entry, string name, string type, string value)
    {
        OdfNode? item = null;
        foreach (var child in entry.Children)
        {
            if (child.LocalName == "config-item" &&
                child.NamespaceUri == OdfNamespaces.Config &&
                child.GetAttribute("name", OdfNamespaces.Config) == name)
            {
                item = child;
                break;
            }
        }

        if (item is null)
        {
            item = new OdfNode(OdfNodeType.Element, "config-item", OdfNamespaces.Config, "config");
            item.SetAttribute("name", OdfNamespaces.Config, name, "config");
            entry.AppendChild(item);
        }

        item.SetAttribute("type", OdfNamespaces.Config, type, "config");
        item.TextContent = value;
    }

    private List<OdfNode> GetRowsList()
    {
        var list = new List<OdfNode>();
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
                list.Add(child);
        }
        return list;
    }

    private List<OdfNode> GetCellsInRow(OdfNode rowNode)
    {
        var list = new List<OdfNode>();
        foreach (var child in rowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
                list.Add(child);
        }
        return list;
    }

    private OdfNode SplitRepeatedRow(OdfNode rowNode, int targetRowIndex, int currentRowIndex, int repeatedCount)
    {
        int beforeCount = targetRowIndex - currentRowIndex;
        int afterCount = (currentRowIndex + repeatedCount) - (targetRowIndex + 1);

        OdfNode targetRowNode = rowNode;

        if (beforeCount > 0)
        {
            var beforeRow = rowNode.CloneNode(true);
            if (beforeCount > 1)
                beforeRow.SetAttribute("number-rows-repeated", OdfNamespaces.Table, beforeCount.ToString(), "table");
            else
                beforeRow.RemoveAttribute("number-rows-repeated", OdfNamespaces.Table);
            TableNode.InsertBefore(beforeRow, rowNode);
        }

        if (afterCount > 0)
        {
            var afterRow = rowNode.CloneNode(true);
            if (afterCount > 1)
                afterRow.SetAttribute("number-rows-repeated", OdfNamespaces.Table, afterCount.ToString(), "table");
            else
                afterRow.RemoveAttribute("number-rows-repeated", OdfNamespaces.Table);
            TableNode.InsertAfter(afterRow, rowNode);
        }

        targetRowNode.RemoveAttribute("number-rows-repeated", OdfNamespaces.Table);
        return targetRowNode;
    }

    private OdfNode SplitRepeatedCell(OdfNode cellNode, int targetColIndex, int currentColIndex, int repeatedCount, OdfNode rowNode)
    {
        int beforeCount = targetColIndex - currentColIndex;
        int afterCount = (currentColIndex + repeatedCount) - (targetColIndex + 1);

        OdfNode targetCellNode = cellNode;

        if (beforeCount > 0)
        {
            var beforeCell = cellNode.CloneNode(true);
            if (beforeCount > 1)
                beforeCell.SetAttribute("number-columns-repeated", OdfNamespaces.Table, beforeCount.ToString(), "table");
            else
                beforeCell.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
            rowNode.InsertBefore(beforeCell, cellNode);
        }

        if (afterCount > 0)
        {
            var afterCell = cellNode.CloneNode(true);
            if (afterCount > 1)
                afterCell.SetAttribute("number-columns-repeated", OdfNamespaces.Table, afterCount.ToString(), "table");
            else
                afterCell.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
            rowNode.InsertAfter(afterCell, cellNode);
        }

        targetCellNode.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
        return targetCellNode;
    }

    private OdfNode SplitRepeatedColumn(OdfNode colNode, int targetColIndex, int currentColIndex, int repeatedCount)
    {
        int beforeCount = targetColIndex - currentColIndex;
        int afterCount = (currentColIndex + repeatedCount) - (targetColIndex + 1);

        OdfNode targetColNode = colNode;

        if (beforeCount > 0)
        {
            var beforeCol = colNode.CloneNode(true);
            if (beforeCount > 1)
                beforeCol.SetAttribute("number-columns-repeated", OdfNamespaces.Table, beforeCount.ToString(), "table");
            else
                beforeCol.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
            TableNode.InsertBefore(beforeCol, colNode);
        }

        if (afterCount > 0)
        {
            var afterCol = colNode.CloneNode(true);
            if (afterCount > 1)
                afterCol.SetAttribute("number-columns-repeated", OdfNamespaces.Table, afterCount.ToString(), "table");
            else
                afterCol.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
            TableNode.InsertAfter(afterCol, colNode);
        }

        targetColNode.RemoveAttribute("number-columns-repeated", OdfNamespaces.Table);
        return targetColNode;
    }

    private OdfNode GetOrCreateRowNodeInternal(int row, bool forWrite)
    {
        int currentRowIndex = 0;
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                {
                    repeatedCount = rc;
                }

                if (row >= currentRowIndex && row < currentRowIndex + repeatedCount)
                {
                    if (forWrite && repeatedCount > 1)
                    {
                        return SplitRepeatedRow(child, row, currentRowIndex, repeatedCount);
                    }
                    return child;
                }
                currentRowIndex += repeatedCount;
            }
        }

        OdfNode? lastRow = null;
        while (currentRowIndex <= row)
        {
            lastRow = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
            TableNode.AppendChild(lastRow);
            currentRowIndex++;
        }
        return lastRow!;
    }

    private OdfNode GetOrCreateCellNodeInternal(OdfNode rowNode, int col, bool forWrite)
    {
        int currentColIndex = 0;
        foreach (var child in rowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                {
                    repeatedCount = rc;
                }

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                {
                    if (forWrite && repeatedCount > 1)
                    {
                        return SplitRepeatedCell(child, col, currentColIndex, repeatedCount, rowNode);
                    }
                    return child;
                }
                currentColIndex += repeatedCount;
            }
        }

        OdfNode? lastCell = null;
        while (currentColIndex <= col)
        {
            lastCell = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
            rowNode.AppendChild(lastCell);
            currentColIndex++;
        }
        return lastCell!;
    }

    /// <summary>
    /// 嘗試以唯讀方式取得指定列與欄索引的儲存格 XML 節點，不修改 DOM 結構。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <returns>儲存格 XML 節點，若不存在則為 null</returns>
    internal OdfNode? TryGetCellNode(int row, int col)
    {
        int currentRowIndex = 0;
        OdfNode? targetRowNode = null;

        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                {
                    repeatedCount = rc;
                }

                if (row >= currentRowIndex && row < currentRowIndex + repeatedCount)
                {
                    targetRowNode = child;
                    break;
                }
                currentRowIndex += repeatedCount;
            }
        }

        if (targetRowNode is null)
        {
            return null;
        }

        int currentColIndex = 0;
        foreach (var child in targetRowNode.Children)
        {
            if ((child.LocalName == "table-cell" || child.LocalName == "covered-table-cell") && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                {
                    repeatedCount = rc;
                }

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                {
                    return child;
                }
                currentColIndex += repeatedCount;
            }
        }

        return null;
    }

    private OdfNode GetOrCreateCellNode(int row, int col)
    {
        var rowNode = GetOrCreateRowNodeInternal(row, forWrite: true);
        return GetOrCreateCellNodeInternal(rowNode, col, forWrite: true);
    }

    private void ReplaceCellNode(int row, int col, OdfNode newCellNode)
    {
        var rowNode = GetOrCreateRowNodeInternal(row, forWrite: true);
        var oldCell = GetOrCreateCellNodeInternal(rowNode, col, forWrite: true);
        rowNode.InsertBefore(newCellNode, oldCell);
        rowNode.RemoveChild(oldCell);
    }

    private OdfNode GetOrCreateColumnNode(int col)
    {
        int currentColIndex = 0;
        OdfNode? insertBeforeNode = null;
        var cols = new List<OdfNode>();

        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                cols.Add(child);
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                {
                    repeatedCount = rc;
                }

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                {
                    if (repeatedCount > 1)
                    {
                        return SplitRepeatedColumn(child, col, currentColIndex, repeatedCount);
                    }
                    return child;
                }
                currentColIndex += repeatedCount;
            }
            else if (cols.Count > 0 && insertBeforeNode is null)
            {
                insertBeforeNode = child;
            }
        }

        OdfNode? lastCol = null;
        while (currentColIndex <= col)
        {
            lastCol = new OdfNode(OdfNodeType.Element, "table-column", OdfNamespaces.Table, "table");
            if (insertBeforeNode is not null)
            {
                TableNode.InsertBefore(lastCol, insertBeforeNode);
            }
            else
            {
                TableNode.AppendChild(lastCol);
            }
            currentColIndex++;
        }
        return lastCol!;
    }

    private OdfNode GetOrCreateRowNode(int row)
    {
        return GetOrCreateRowNodeInternal(row, forWrite: true);
    }

    /// <summary>
    /// 設定指定列是否可見。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="visible">是否顯示</param>
    public void SetRowVisible(int row, bool visible)
    {
        var rowNode = GetOrCreateRowNode(row);
        rowNode.SetAttribute("visibility", OdfNamespaces.Table, visible ? "visible" : "collapse", "table");
    }

    /// <summary>
    /// 設定指定欄是否可見。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <param name="visible">是否顯示</param>
    public void SetColumnVisible(int col, bool visible)
    {
        var colNode = GetOrCreateColumnNode(col);
        colNode.SetAttribute("visibility", OdfNamespaces.Table, visible ? "visible" : "collapse", "table");
    }

    /// <summary>
    /// 判斷指定列是否可見。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <returns>若顯示則為 true，否則為 false</returns>
    public bool IsRowVisible(int row)
    {
        int currentRowIndex = 0;
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "table-row" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                {
                    repeatedCount = rc;
                }

                if (row >= currentRowIndex && row < currentRowIndex + repeatedCount)
                {
                    return child.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
                }
                currentRowIndex += repeatedCount;
            }
        }
        return true;
    }

    /// <summary>
    /// 判斷指定欄是否可見。
    /// </summary>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <returns>若顯示則為 true，否則為 false</returns>
    public bool IsColumnVisible(int col)
    {
        int currentColIndex = 0;
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
            {
                int repeatedCount = 1;
                string? repStr = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(repStr) && int.TryParse(repStr, out int rc))
                {
                    repeatedCount = rc;
                }

                if (col >= currentColIndex && col < currentColIndex + repeatedCount)
                {
                    return child.GetAttribute("visibility", OdfNamespaces.Table) != "collapse";
                }
                currentColIndex += repeatedCount;
            }
        }
        return true;
    }

    /// <summary>
    /// 新增命名範圍至此工作表。
    /// </summary>
    /// <param name="name">命名範圍的名稱</param>
    /// <param name="range">儲存格範圍</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddNamedRange(string name, OdfCellRange range, OdfCellAddress? baseCell = null)
    {
        var namedExpressions = FindOrCreateChild(TableNode, "named-expressions", OdfNamespaces.Table, "table");
        var namedRange = new OdfNode(OdfNodeType.Element, "named-range", OdfNamespaces.Table, "table");
        namedRange.SetAttribute("name", OdfNamespaces.Table, name, "table");
        namedRange.SetAttribute("cell-range-address", OdfNamespaces.Table, range.ToOdfString(false), "table");
        if (baseCell.HasValue)
        {
            namedRange.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
        }
        namedExpressions.AppendChild(namedRange);
    }

    /// <summary>
    /// 取得此工作表中的命名範圍清單。
    /// </summary>
    public IReadOnlyList<OdfNamedRangeInfo> NamedRanges
    {
        get
        {
            OdfNode? namedExpressions = FindChildElement(TableNode, "named-expressions", OdfNamespaces.Table);
            if (namedExpressions is null)
            {
                return [];
            }

            List<OdfNamedRangeInfo> ranges = [];
            foreach (OdfNode child in namedExpressions.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "named-range" &&
                    child.NamespaceUri == OdfNamespaces.Table)
                {
                    string name = child.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
                    string address = child.GetAttribute("cell-range-address", OdfNamespaces.Table) ?? string.Empty;
                    string? baseAddress = child.GetAttribute("base-cell-address", OdfNamespaces.Table);
                    ranges.Add(new OdfNamedRangeInfo(name, address, baseAddress));
                }
            }

            return ranges.AsReadOnly();
        }
    }

    /// <summary>
    /// 新增具名運算式至此工作表。
    /// </summary>
    /// <param name="name">具名運算式的名稱</param>
    /// <param name="expression">公式運算式字串</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddNamedExpression(string name, string expression, OdfCellAddress? baseCell = null)
    {
        var namedExpressions = FindOrCreateChild(TableNode, "named-expressions", OdfNamespaces.Table, "table");
        var namedExpr = new OdfNode(OdfNodeType.Element, "named-expression", OdfNamespaces.Table, "table");
        namedExpr.SetAttribute("name", OdfNamespaces.Table, name, "table");
        namedExpr.SetAttribute("expression", OdfNamespaces.Table, expression, "table");
        if (baseCell.HasValue)
        {
            namedExpr.SetAttribute("base-cell-address", OdfNamespaces.Table, baseCell.Value.ToOdfString(false), "table");
        }
        namedExpressions.AppendChild(namedExpr);
    }

    /// <summary>
    /// 取得此工作表中的具名運算式清單。
    /// </summary>
    public IReadOnlyList<OdfNamedExpressionInfo> NamedExpressions
    {
        get
        {
            OdfNode? namedExpressions = FindChildElement(TableNode, "named-expressions", OdfNamespaces.Table);
            if (namedExpressions is null)
            {
                return [];
            }

            List<OdfNamedExpressionInfo> expressions = [];
            foreach (OdfNode child in namedExpressions.Children)
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "named-expression" &&
                    child.NamespaceUri == OdfNamespaces.Table)
                {
                    string name = child.GetAttribute("name", OdfNamespaces.Table) ?? string.Empty;
                    string expression = child.GetAttribute("expression", OdfNamespaces.Table) ?? string.Empty;
                    string? baseAddress = child.GetAttribute("base-cell-address", OdfNamespaces.Table);
                    expressions.Add(new OdfNamedExpressionInfo(name, expression, baseAddress));
                }
            }

            return expressions.AsReadOnly();
        }
    }

    private static int ParseNonNegativeInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) && result > 0
            ? result
            : 0;
    }

    private static OdfNode? FindChildElement(OdfNode parent, string localName, string ns)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == ns)
            {
                return child;
            }
        }

        return null;
    }

    private OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }
        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        parent.AppendChild(node);
        return node;
    }
}

/// <summary>
/// 表示工作表凍結窗格設定。
/// </summary>
/// <param name="rows">凍結列數。</param>
/// <param name="columns">凍結欄數。</param>
public readonly struct OdfFrozenPanes(int rows, int columns) : IEquatable<OdfFrozenPanes>
{
    /// <summary>
    /// 取得凍結列數。
    /// </summary>
    public int Rows { get; } = rows;

    /// <summary>
    /// 取得凍結欄數。
    /// </summary>
    public int Columns { get; } = columns;

    /// <summary>
    /// 取得是否有任何凍結窗格設定。
    /// </summary>
    public bool IsFrozen => Rows > 0 || Columns > 0;

    /// <inheritdoc />
    public bool Equals(OdfFrozenPanes other) => Rows == other.Rows && Columns == other.Columns;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfFrozenPanes other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (Rows * 397) ^ Columns;
        }
    }

    /// <summary>
    /// 比較兩個 <see cref="OdfFrozenPanes"/> 執行個體是否相等。
    /// </summary>
    public static bool operator ==(OdfFrozenPanes left, OdfFrozenPanes right) => left.Equals(right);

    /// <summary>
    /// 比較兩個 <see cref="OdfFrozenPanes"/> 執行個體是否不相等。
    /// </summary>
    public static bool operator !=(OdfFrozenPanes left, OdfFrozenPanes right) => !left.Equals(right);
}

/// <summary>
/// 表示工作表中的命名範圍。
/// </summary>
/// <param name="name">命名範圍名稱。</param>
/// <param name="cellRangeAddress">ODF 儲存格範圍位址。</param>
/// <param name="baseCellAddress">ODF 基準儲存格位址。</param>
public sealed class OdfNamedRangeInfo(string name, string cellRangeAddress, string? baseCellAddress)
{
    /// <summary>
    /// 取得命名範圍名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得 ODF 儲存格範圍位址。
    /// </summary>
    public string CellRangeAddress { get; } = cellRangeAddress ?? string.Empty;

    /// <summary>
    /// 取得 ODF 基準儲存格位址。
    /// </summary>
    public string? BaseCellAddress { get; } = baseCellAddress;
}

/// <summary>
/// 表示工作表中的具名運算式。
/// </summary>
/// <param name="name">具名運算式名稱。</param>
/// <param name="expression">ODF 運算式。</param>
/// <param name="baseCellAddress">ODF 基準儲存格位址。</param>
public sealed class OdfNamedExpressionInfo(string name, string expression, string? baseCellAddress)
{
    /// <summary>
    /// 取得具名運算式名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得 ODF 運算式。
    /// </summary>
    public string Expression { get; } = expression ?? string.Empty;

    /// <summary>
    /// 取得 ODF 基準儲存格位址。
    /// </summary>
    public string? BaseCellAddress { get; } = baseCellAddress;
}

/// <summary>
/// 提供工作表列的索引入口。
/// </summary>
public sealed class OdfRowCollection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfRowCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    public OdfRowCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// 依索引取得列。
    /// </summary>
    /// <param name="index">以 0 為基準的列索引。</param>
    /// <returns>指定列。</returns>
    public OdfSheetRow this[int index] => new(_sheet, index);
}

/// <summary>
/// 表示工作表中的一列。
/// </summary>
public sealed class OdfSheetRow
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfSheetRow"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    /// <param name="index">以 0 為基準的列索引。</param>
    public OdfSheetRow(OdfTableSheet sheet, int index)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        Index = index;
    }

    /// <summary>
    /// 取得以 0 為基準的列索引。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 取得或設定此列是否顯示。
    /// </summary>
    public bool Visible
    {
        get => _sheet.IsRowVisible(Index);
        set => _sheet.SetRowVisible(Index, value);
    }

    /// <summary>
    /// 取得此列的儲存格集合。
    /// </summary>
    public OdfRowCellCollection Cells => new(_sheet, Index);
}

/// <summary>
/// 提供列內儲存格的索引入口。
/// </summary>
public sealed class OdfRowCellCollection
{
    private readonly OdfTableSheet _sheet;
    private readonly int _row;

    /// <summary>
    /// 初始化 <see cref="OdfRowCellCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    /// <param name="row">以 0 為基準的列索引。</param>
    public OdfRowCellCollection(OdfTableSheet sheet, int row)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _row = row;
    }

    /// <summary>
    /// 依欄索引取得列內儲存格。
    /// </summary>
    /// <param name="column">以 0 為基準的欄索引。</param>
    /// <returns>指定儲存格。</returns>
    public OdfCell this[int column] => _sheet.GetCell(_row, column);
}

/// <summary>
/// 提供工作表欄的索引入口。
/// </summary>
public sealed class OdfColumnCollection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfColumnCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    public OdfColumnCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// 依索引取得欄。
    /// </summary>
    /// <param name="index">以 0 為基準的欄索引。</param>
    /// <returns>指定欄。</returns>
    public OdfSheetColumn this[int index] => new(_sheet, index);
}

/// <summary>
/// 表示工作表中的一欄。
/// </summary>
public sealed class OdfSheetColumn
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfSheetColumn"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    /// <param name="index">以 0 為基準的欄索引。</param>
    public OdfSheetColumn(OdfTableSheet sheet, int index)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        Index = index;
    }

    /// <summary>
    /// 取得以 0 為基準的欄索引。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 取得或設定此欄是否顯示。
    /// </summary>
    public bool Visible
    {
        get => _sheet.IsColumnVisible(Index);
        set => _sheet.SetColumnVisible(Index, value);
    }

    /// <summary>
    /// 設定欄寬。
    /// </summary>
    /// <param name="width">欄寬。</param>
    public void SetWidth(OdfLength width)
    {
        _sheet.SetColumnWidth(Index, width);
    }

    /// <summary>
    /// 依目前內容自動調整欄寬。
    /// </summary>
    public void AutoFit()
    {
        _sheet.AutoFitColumnWidth(Index);
    }
}

/// <summary>
/// 提供工作表儲存格範圍的索引入口。
/// </summary>
public sealed class OdfRangeCollection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfRangeCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    public OdfRangeCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// 依 Excel 樣式範圍字串取得範圍。
    /// </summary>
    /// <param name="address">範圍位址，例如 <c>A1:C3</c>。</param>
    /// <returns>範圍選取物件。</returns>
    public OdfCellRangeSelection this[string address] => new(_sheet, OdfCellRange.ParseExcel(address));

    /// <summary>
    /// 依列與欄索引取得範圍。
    /// </summary>
    /// <param name="startRow">起始列索引。</param>
    /// <param name="startColumn">起始欄索引。</param>
    /// <param name="endRow">結束列索引。</param>
    /// <param name="endColumn">結束欄索引。</param>
    /// <returns>範圍選取物件。</returns>
    public OdfCellRangeSelection this[int startRow, int startColumn, int endRow, int endColumn] =>
        new(_sheet, new OdfCellRange(startRow, startColumn, endRow, endColumn, _sheet.Name));
}

/// <summary>
/// 表示工作表中的一個範圍選取。
/// </summary>
public sealed class OdfCellRangeSelection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfCellRangeSelection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    /// <param name="range">儲存格範圍。</param>
    public OdfCellRangeSelection(OdfTableSheet sheet, OdfCellRange range)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        Range = EnsureSheetName(range, sheet.Name);
    }

    /// <summary>
    /// 取得此選取代表的儲存格範圍。
    /// </summary>
    public OdfCellRange Range { get; }

    /// <summary>
    /// 合併此範圍的儲存格。
    /// </summary>
    public void Merge()
    {
        _sheet.MergeCells(Range);
    }

    /// <summary>
    /// 將此範圍加入命名範圍。
    /// </summary>
    /// <param name="name">命名範圍名稱。</param>
    public void NameAs(string name)
    {
        _sheet.AddNamedRange(name, Range);
    }

    /// <summary>
    /// 為此範圍新增篩選。
    /// </summary>
    /// <param name="name">資料庫範圍名稱。</param>
    /// <param name="conditions">篩選條件。</param>
    public void AddFilter(string name, params (int fieldNumber, string op, string value)[] conditions)
    {
        _sheet.AddDatabaseRange(name, Range).SetFilter(conditions);
    }

    /// <summary>
    /// 為此範圍新增條件格式。
    /// </summary>
    /// <param name="condition">條件運算式。</param>
    /// <param name="styleName">套用的樣式名稱。</param>
    public void AddConditionalFormat(string condition, string styleName)
    {
        _sheet.AddConditionalFormat(Range, condition, styleName);
    }

    /// <summary>
    /// 為此範圍新增清單型資料驗證。
    /// </summary>
    /// <param name="name">驗證規則名稱。</param>
    /// <param name="allowedValues">允許的值。</param>
    public void AddValidationList(string name, params string[] allowedValues)
    {
        _sheet.AddValidationList(Range, name, allowedValues);
    }

    private static OdfCellRange EnsureSheetName(OdfCellRange range, string sheetName)
    {
        var start = range.StartAddress;
        var end = range.EndAddress;

        if (start.SheetName is null)
        {
            start = new OdfCellAddress(start.Row, start.Column, sheetName, start.IsRowAbsolute, start.IsColumnAbsolute, start.IsSheetAbsolute);
        }

        if (end.SheetName is null)
        {
            end = new OdfCellAddress(end.Row, end.Column, sheetName, end.IsRowAbsolute, end.IsColumnAbsolute, end.IsSheetAbsolute);
        }

        return new OdfCellRange(start, end);
    }
}

/// <summary>
/// 提供工作表儲存格的索引入口。
/// </summary>
public sealed class OdfCellCollection
{
    private readonly OdfTableSheet _sheet;

    /// <summary>
    /// 初始化 <see cref="OdfCellCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="sheet">所屬工作表。</param>
    public OdfCellCollection(OdfTableSheet sheet)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
    }

    /// <summary>
    /// 依 A1 樣式位址取得儲存格。
    /// </summary>
    /// <param name="address">儲存格位址，例如 <c>A1</c>。</param>
    /// <returns>指定位置的儲存格。</returns>
    public OdfCell this[string address] => _sheet.GetCell(address);

    /// <summary>
    /// 依列與欄索引取得儲存格。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引。</param>
    /// <param name="column">以 0 為基準的欄索引。</param>
    /// <returns>指定位置的儲存格。</returns>
    public OdfCell this[int row, int column] => _sheet.GetCell(row, column);
}

/// <summary>
/// 表示 ODF 工作表中的一個儲存格。
/// </summary>
/// <remarks>
/// 初始化 <see cref="OdfCell"/> 類別的新執行個體。
/// </remarks>
/// <param name="node">儲存格 XML 節點</param>
/// <param name="row">以 0 為基準的列索引</param>
/// <param name="col">以 0 為基準的欄索引</param>
/// <param name="doc">試算表文件</param>
public class OdfCell(OdfNode node, int row, int col, SpreadsheetDocument doc)
{
    /// <summary>
    /// 取得代表儲存格的 XML 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    /// <summary>
    /// 取得以 0 為基準的列索引。
    /// </summary>
    public int Row { get; } = row;

    /// <summary>
    /// 取得以 0 為基準的欄索引。
    /// </summary>
    public int Column { get; } = col;

    private readonly SpreadsheetDocument _doc = doc;

    /// <summary>
    /// 取得或設定儲存格資料值的型態。
    /// </summary>
    public string ValueType
    {
        get => Node.GetAttribute("value-type", OdfNamespaces.Office) ?? string.Empty;
        set => Node.SetAttribute("value-type", OdfNamespaces.Office, value, "office");
    }

    /// <summary>
    /// 取得或設定儲存格的原始數值或內容（字串格式）。
    /// </summary>
    public string Value
    {
        get => Node.GetAttribute("value", OdfNamespaces.Office) ?? string.Empty;
        set => Node.SetAttribute("value", OdfNamespaces.Office, value, "office");
    }

    /// <summary>
    /// 取得或設定儲存格的常用型別值。
    /// </summary>
    public object? CellValue
    {
        get
        {
            return ValueType switch
            {
                "float" => double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    ? number
                    : null,
                "boolean" => bool.TryParse(Node.GetAttribute("boolean-value", OdfNamespaces.Office), out bool flag)
                    ? flag
                    : null,
                "date" => Node.GetAttribute("date-value", OdfNamespaces.Office),
                "string" => TextContent,
                _ => string.IsNullOrEmpty(TextContent) ? null : TextContent
            };
        }
        set
        {
            switch (value)
            {
                case null:
                    ClearValue();
                    break;
                case string text:
                    SetValue(text);
                    break;
                case bool flag:
                    SetValue(flag);
                    break;
                case DateTime date:
                    SetValue(date);
                    break;
                case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    SetValue(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                    break;
                default:
                    SetValue(value.ToString() ?? string.Empty);
                    break;
            }
        }
    }

    /// <summary>
    /// 取得或設定儲存格套用的表格樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => Node.GetAttribute("style-name", OdfNamespaces.Table);
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                Node.RemoveAttribute("style-name", OdfNamespaces.Table);
            }
            else
            {
                Node.SetAttribute("style-name", OdfNamespaces.Table, value!, "table");
            }
        }
    }

    /// <summary>
    /// 取得或設定儲存格顯示的文字內容。
    /// </summary>
    public string TextContent
    {
        get
        {
            foreach (var child in Node.Children)
            {
                if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    return child.TextContent;
                }
            }
            return Node.TextContent;
        }
        set
        {
            SetCellTextContent(value);
        }
    }

    /// <summary>
    /// 取得或設定儲存格的公式。
    /// </summary>
    public string Formula
    {
        get => Node.GetAttribute("formula", OdfNamespaces.Table) ?? string.Empty;
        set => Node.SetAttribute("formula", OdfNamespaces.Table, value, "table");
    }

    /// <summary>
    /// 設定儲存格的數值。
    /// </summary>
    /// <param name="val">數值</param>
    public void SetValue(double val)
    {
        ValueType = "float";
        Value = val.ToString(System.Globalization.CultureInfo.InvariantCulture);
        TextContent = val.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 設定儲存格的布林值。
    /// </summary>
    /// <param name="val">布林值</param>
    public void SetValue(bool val)
    {
        ValueType = "boolean";
        Node.SetAttribute("boolean-value", OdfNamespaces.Office, val ? "true" : "false", "office");
        TextContent = val ? "TRUE" : "FALSE";
    }

    /// <summary>
    /// 設定儲存格的日期時間值。
    /// </summary>
    /// <param name="date">日期時間</param>
    /// <param name="useTimezoneNaive">是否忽略時區轉換，使用本地時間格式</param>
    public void SetValue(DateTime date, bool useTimezoneNaive = false)
    {
        ValueType = "date";
        string isoDate;
        if (date == DateTime.MinValue || date == DateTime.MaxValue)
        {
            isoDate = useTimezoneNaive 
                ? date.ToString("yyyy-MM-ddTHH:mm:ss")
                : date.ToString("yyyy-MM-ddTHH:mm:ss") + "Z";
        }
        else
        {
            isoDate = useTimezoneNaive 
                ? date.ToString("yyyy-MM-ddTHH:mm:ss")
                : date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        }
        Node.SetAttribute("date-value", OdfNamespaces.Office, isoDate, "office");
        TextContent = isoDate;
    }

    /// <summary>
    /// 設定儲存格的文字內容。
    /// </summary>
    /// <param name="text">文字字串</param>
    public void SetValue(string text)
    {
        ValueType = "string";
        TextContent = text;
    }

    private void ClearValue()
    {
        Node.RemoveAttribute("value-type", OdfNamespaces.Office);
        Node.RemoveAttribute("value", OdfNamespaces.Office);
        Node.RemoveAttribute("boolean-value", OdfNamespaces.Office);
        Node.RemoveAttribute("date-value", OdfNamespaces.Office);
        TextContent = string.Empty;
    }

    private void SetCellTextContent(string text)
    {
        var toRemove = new List<OdfNode>();
        foreach (var child in Node.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Text)
                toRemove.Add(child);
        }
        foreach (var child in toRemove)
        {
            Node.RemoveChild(child);
        }

        var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        bool needsWrap = false;
        
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\n')
            {
                pNode.AppendChild(new OdfNode(OdfNodeType.Element, "line-break", OdfNamespaces.Text, "text"));
                needsWrap = true;
                i++;
            }
            else if (text[i] == '\t')
            {
                pNode.AppendChild(new OdfNode(OdfNodeType.Element, "tab", OdfNamespaces.Text, "text"));
                i++;
            }
            else if (text[i] == ' ')
            {
                int spaceCount = 0;
                while (i < text.Length && text[i] == ' ')
                {
                    spaceCount++;
                    i++;
                }

                if (spaceCount == 1)
                {
                    pNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = " " });
                }
                else
                {
                    pNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = " " });
                    var sNode = new OdfNode(OdfNodeType.Element, "s", OdfNamespaces.Text, "text");
                    sNode.SetAttribute("c", OdfNamespaces.Text, (spaceCount - 1).ToString(), "text");
                    pNode.AppendChild(sNode);
                }
            }
            else
            {
                int start = i;
                while (i < text.Length && text[i] != '\n' && text[i] != '\t' && text[i] != ' ')
                {
                    i++;
                }
                string segment = text.Substring(start, i - start);
                pNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = segment });
            }
        }

        Node.AppendChild(pNode);

        if (needsWrap)
        {
            SetStyleProperty("table-cell-properties", "wrap-option", OdfNamespaces.Fo, "wrap", "fo");
        }
    }

    /// <summary>
    /// 設定此儲存格的四面框線樣式。
    /// </summary>
    /// <param name="top">上框線</param>
    /// <param name="bottom">下框線</param>
    /// <param name="left">左框線</param>
    /// <param name="right">右框線</param>
    public void SetBorders(OdfBorder? top, OdfBorder? bottom, OdfBorder? left, OdfBorder? right)
    {
        if (top.HasValue) SetStyleProperty("table-cell-properties", "border-top", OdfNamespaces.Fo, top.Value.ToString(), "fo");
        if (bottom.HasValue) SetStyleProperty("table-cell-properties", "border-bottom", OdfNamespaces.Fo, bottom.Value.ToString(), "fo");
        if (left.HasValue) SetStyleProperty("table-cell-properties", "border-left", OdfNamespaces.Fo, left.Value.ToString(), "fo");
        if (right.HasValue) SetStyleProperty("table-cell-properties", "border-right", OdfNamespaces.Fo, right.Value.ToString(), "fo");
    }

    /// <summary>
    /// 新增條件格式對應規則。
    /// </summary>
    /// <param name="condition">條件值（例如 "cell-content()=1"）</param>
    /// <param name="applyStyleName">要套用的格式樣式名稱</param>
    /// <param name="baseCell">基準儲存格位址</param>
    public void AddConditionalFormatMap(string condition, string applyStyleName, OdfCellAddress? baseCell = null)
    {
        var styleNode = _doc.StyleEngine.GetOrCreateLocalStyle(Node, "table-cell");
        var mapNode = new OdfNode(OdfNodeType.Element, "map", OdfNamespaces.Style, "style");
        mapNode.SetAttribute("condition", OdfNamespaces.Style, condition, "style");
        mapNode.SetAttribute("apply-style-name", OdfNamespaces.Style, applyStyleName, "style");
        if (baseCell.HasValue)
        {
            mapNode.SetAttribute("base-cell-address", OdfNamespaces.Style, baseCell.Value.ToOdfString(false), "style");
        }
        styleNode.AppendChild(mapNode);
    }

    private void SetStyleProperty(string propertiesElement, string propertyAttr, string propertyNs, string value, string propertyPrefix)
    {
        _doc.StyleEngine.SetLocalStyleProperty(Node, "table-cell", propertiesElement, propertyAttr, propertyNs, value, propertyPrefix);
    }
}
