using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Database;

/// <summary>
/// Represents the schema definition accessor for an ODB database, used to model table structures, primary keys, and foreign key relationships.
/// 表示 ODB 資料庫的 Schema 定義存取器，用於建模資料表結構、主鍵與外鍵關聯。
/// </summary>
public sealed class OdfDatabaseSchema
{
    private const string DatabaseNamespace = "urn:oasis:names:tc:opendocument:xmlns:database:1.0";
    private readonly OdfDatabaseDocument _document;
    private readonly List<OdfSchemaTable> _tables = [];

    /// <summary>
    /// 初始化 <see cref="OdfDatabaseSchema"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">相關聯的 ODB 資料庫文件</param>
    internal OdfDatabaseSchema(OdfDatabaseDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        LoadFromDocument();
    }

    /// <summary>
    /// Gets all currently defined table structures in the schema.
    /// 取得目前 Schema 中所有已定義的資料表結構。
    /// </summary>
    public IReadOnlyList<OdfSchemaTable> Tables => _tables.AsReadOnly();

    /// <summary>
    /// Adds a table structure to the schema.
    /// 新增一個資料表結構至 Schema 中。
    /// </summary>
    /// <param name="table">The table structure to add. / 要新增的資料表結構。</param>
    /// <exception cref="ArgumentNullException">When <paramref name="table"/> is <see langword="null"/>. / 當 <paramref name="table"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="InvalidOperationException">When a table with the same name already exists in the schema. / 當資料表名稱已存在於 Schema 中時擲出。</exception>
    public void AddTable(OdfSchemaTable table)
    {
        if (table is null)
        {
            throw new ArgumentNullException(nameof(table));
        }

        if (_tables.Any(t => string.Equals(t.Name, table.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfDatabaseSchema_DataTableNameAlready", table.Name));
        }

        _tables.Add(table);
        SaveToDocument();
    }

    /// <summary>
    /// Removes the table structure with the specified name from the schema.
    /// 從 Schema 中移除指定名稱的資料表結構。
    /// </summary>
    /// <param name="tableName">The name of the table to remove. / 要移除的資料表名稱。</param>
    /// <returns><see langword="true"/> if removed successfully; otherwise <see langword="false"/>. / 如果成功移除，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentException">When <paramref name="tableName"/> is empty. / 當 <paramref name="tableName"/> 為空時擲出。</exception>
    public bool RemoveTable(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseSchema_DataCannotBeEmpty_2"), nameof(tableName));
        }

        var table = _tables.FirstOrDefault(t => string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));
        if (table is null)
        {
            return false;
        }

        _tables.Remove(table);
        SaveToDocument();
        return true;
    }

    /// <summary>
    /// Synchronizes the current schema state back to the database document.
    /// 將目前的 Schema 狀態同步保存回資料庫文件中。
    /// </summary>
    public void Save()
    {
        SaveToDocument();
    }

    private void LoadFromDocument()
    {
        _tables.Clear();

        var body = FindOrCreateChild(_document.ContentDom, "body", OdfNamespaces.Office, "office");
        var database = FindOrCreateChild(body, "database", OdfNamespaces.Office, "office");
        var schemaDef = FindChildElement(database, "schema-definition", DatabaseNamespace);
        if (schemaDef is null)
        {
            return;
        }

        var tableDefs = FindChildElement(schemaDef, "table-definitions", DatabaseNamespace);
        if (tableDefs is null)
        {
            return;
        }

        foreach (var child in tableDefs.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "table-definition" &&
                child.NamespaceUri == DatabaseNamespace)
            {
                var tableName = child.GetAttribute("name", DatabaseNamespace) ?? string.Empty;
                if (string.IsNullOrEmpty(tableName))
                {
                    continue;
                }

                var table = new OdfSchemaTable(tableName);

                // 載入欄位定義
                var columnDefs = FindChildElement(child, "column-definitions", DatabaseNamespace);
                if (columnDefs is not null)
                {
                    foreach (var colChild in columnDefs.Children)
                    {
                        if (colChild.NodeType is OdfNodeType.Element &&
                            colChild.LocalName == "column-definition" &&
                            colChild.NamespaceUri == DatabaseNamespace)
                        {
                            var colName = colChild.GetAttribute("name", DatabaseNamespace) ?? string.Empty;
                            var typeName = colChild.GetAttribute("type-name", DatabaseNamespace) ?? "VARCHAR";
                            var isNullableAttr = colChild.GetAttribute("is-nullable", DatabaseNamespace);
                            var isNullable = !string.Equals(isNullableAttr, "no-nulls", StringComparison.OrdinalIgnoreCase);
                            var isAutoIncAttr = colChild.GetAttribute("is-autoincrement", DatabaseNamespace);
                            var isAutoInc = string.Equals(isAutoIncAttr, "true", StringComparison.OrdinalIgnoreCase);
                            var isUniqueAttr = colChild.GetAttribute("is-unique", DatabaseNamespace);
                            var isUnique = string.Equals(isUniqueAttr, "true", StringComparison.OrdinalIgnoreCase);
                            var defaultValue = colChild.GetAttribute("default-value", DatabaseNamespace);
                            var checkConstraint = colChild.GetAttribute("check-constraint", DatabaseNamespace);

                            table.Columns.Add(new OdfSchemaColumn(colName, typeName, isNullable, isAutoInc)
                            {
                                IsUnique = isUnique,
                                DefaultValue = string.IsNullOrEmpty(defaultValue) ? null : defaultValue,
                                CheckConstraint = string.IsNullOrEmpty(checkConstraint) ? null : checkConstraint,
                            });
                        }
                    }
                }

                // 載入主鍵與外鍵
                var keysNode = FindChildElement(child, "keys", DatabaseNamespace);
                if (keysNode is not null)
                {
                    foreach (var keyChild in keysNode.Children)
                    {
                        if (keyChild.NodeType is OdfNodeType.Element &&
                            keyChild.LocalName == "key" &&
                            keyChild.NamespaceUri == DatabaseNamespace)
                        {
                            var keyType = keyChild.GetAttribute("type", DatabaseNamespace);
                            var keyName = keyChild.GetAttribute("name", DatabaseNamespace);

                            var keyColumnsNode = FindChildElement(keyChild, "key-columns", DatabaseNamespace);
                            var keyCols = new List<OdfSchemaKeyMapping>();
                            if (keyColumnsNode is not null)
                            {
                                foreach (var kcChild in keyColumnsNode.Children)
                                {
                                    if (kcChild.NodeType is OdfNodeType.Element &&
                                        kcChild.LocalName == "key-column" &&
                                        kcChild.NamespaceUri == DatabaseNamespace)
                                    {
                                        var colName = kcChild.GetAttribute("name", DatabaseNamespace) ?? string.Empty;
                                        var relatedColName = kcChild.GetAttribute("related-column-name", DatabaseNamespace) ?? string.Empty;
                                        keyCols.Add(new OdfSchemaKeyMapping(colName, relatedColName));
                                    }
                                }
                            }

                            if (string.Equals(keyType, "primary", StringComparison.OrdinalIgnoreCase))
                            {
                                table.PrimaryKey = new OdfSchemaPrimaryKey(
                                    keyName,
                                    keyCols.Select(c => c.Column).ToList());
                            }
                            else if (string.Equals(keyType, "foreign", StringComparison.OrdinalIgnoreCase))
                            {
                                var refTable = keyChild.GetAttribute("referenced-table-name", DatabaseNamespace) ?? string.Empty;
                                var updateRule = keyChild.GetAttribute("update-rule", DatabaseNamespace);
                                var deleteRule = keyChild.GetAttribute("delete-rule", DatabaseNamespace);

                                table.ForeignKeys.Add(new OdfSchemaForeignKey(
                                    keyName,
                                    refTable,
                                    keyCols,
                                    updateRule,
                                    deleteRule));
                            }
                        }
                    }
                }

                // 載入索引定義
                var indicesNode = FindChildElement(child, "indices", DatabaseNamespace);
                if (indicesNode is not null)
                {
                    foreach (var indexChild in indicesNode.Children)
                    {
                        if (indexChild.NodeType is OdfNodeType.Element &&
                            indexChild.LocalName == "index" &&
                            indexChild.NamespaceUri == DatabaseNamespace)
                        {
                            var indexName = indexChild.GetAttribute("name", DatabaseNamespace) ?? string.Empty;
                            if (string.IsNullOrEmpty(indexName))
                            {
                                continue;
                            }

                            var isUniqueIndexAttr = indexChild.GetAttribute("unique", DatabaseNamespace);
                            var isUniqueIndex = string.Equals(isUniqueIndexAttr, "true", StringComparison.OrdinalIgnoreCase);

                            var indexColumns = new List<string>();
                            var indexColumnsNode = FindChildElement(indexChild, "index-columns", DatabaseNamespace);
                            if (indexColumnsNode is not null)
                            {
                                foreach (var icChild in indexColumnsNode.Children)
                                {
                                    if (icChild.NodeType is OdfNodeType.Element &&
                                        icChild.LocalName == "index-column" &&
                                        icChild.NamespaceUri == DatabaseNamespace)
                                    {
                                        var icName = icChild.GetAttribute("name", DatabaseNamespace) ?? string.Empty;
                                        if (!string.IsNullOrEmpty(icName))
                                        {
                                            indexColumns.Add(icName);
                                        }
                                    }
                                }
                            }

                            table.Indexes.Add(new OdfSchemaIndex(indexName, isUniqueIndex, indexColumns));
                        }
                    }
                }

                _tables.Add(table);
            }
        }
    }

    private void SaveToDocument()
    {
        var body = FindOrCreateChild(_document.ContentDom, "body", OdfNamespaces.Office, "office");
        var database = FindOrCreateChild(body, "database", OdfNamespaces.Office, "office");

        // 1. 同步寫入 schema-definition（實體結構層）
        var schemaDef = FindOrCreateChild(database, "schema-definition", DatabaseNamespace, "db");
        // 清空現有的 table-definitions
        var tableDefs = FindChildElement(schemaDef, "table-definitions", DatabaseNamespace);
        if (tableDefs is not null)
        {
            schemaDef.RemoveChild(tableDefs);
        }
        tableDefs = OdfNodeFactory.CreateElement("table-definitions", DatabaseNamespace, "db");
        schemaDef.AppendChild(tableDefs);

        // 2. 同步寫入 table-representations（表現層）
        var tableReps = FindChildElement(database, "table-representations", DatabaseNamespace);
        if (tableReps is not null)
        {
            database.RemoveChild(tableReps);
        }
        tableReps = OdfNodeFactory.CreateElement("table-representations", DatabaseNamespace, "db");

        // 插入到適當的順序位置
        var queriesNode = FindChildElement(database, "queries", DatabaseNamespace);
        if (queriesNode is not null)
        {
            database.InsertBefore(tableReps, queriesNode);
        }
        else
        {
            database.AppendChild(tableReps);
        }

        foreach (var table in _tables)
        {
            // === 寫入實體層 ===
            var tableDef = OdfNodeFactory.CreateElement("table-definition", DatabaseNamespace, "db");
            tableDef.SetAttribute("name", DatabaseNamespace, table.Name, "db");
            tableDefs.AppendChild(tableDef);

            var columnDefs = OdfNodeFactory.CreateElement("column-definitions", DatabaseNamespace, "db");
            tableDef.AppendChild(columnDefs);

            foreach (var col in table.Columns)
            {
                var colDef = OdfNodeFactory.CreateElement("column-definition", DatabaseNamespace, "db");
                colDef.SetAttribute("name", DatabaseNamespace, col.Name, "db");
                colDef.SetAttribute("type-name", DatabaseNamespace, col.TypeName, "db");
                colDef.SetAttribute("is-nullable", DatabaseNamespace, col.IsNullable ? "nullable" : "no-nulls", "db");
                if (col.IsAutoIncrement)
                {
                    colDef.SetAttribute("is-autoincrement", DatabaseNamespace, "true", "db");
                }
                if (col.IsUnique)
                {
                    colDef.SetAttribute("is-unique", DatabaseNamespace, "true", "db");
                }
                if (!string.IsNullOrEmpty(col.DefaultValue))
                {
                    colDef.SetAttribute("default-value", DatabaseNamespace, col.DefaultValue!, "db");
                }
                if (!string.IsNullOrEmpty(col.CheckConstraint))
                {
                    colDef.SetAttribute("check-constraint", DatabaseNamespace, col.CheckConstraint!, "db");
                }
                columnDefs.AppendChild(colDef);
            }

            // 寫入主鍵與外鍵
            if (table.PrimaryKey is not null || table.ForeignKeys.Count > 0)
            {
                var keysNode = OdfNodeFactory.CreateElement("keys", DatabaseNamespace, "db");
                tableDef.AppendChild(keysNode);

                if (table.PrimaryKey is not null)
                {
                    var pkNode = OdfNodeFactory.CreateElement("key", DatabaseNamespace, "db");
                    pkNode.SetAttribute("type", DatabaseNamespace, "primary", "db");
                    if (!string.IsNullOrEmpty(table.PrimaryKey.Name))
                    {
                        pkNode.SetAttribute("name", DatabaseNamespace, table.PrimaryKey.Name!, "db");
                    }

                    var keyColsNode = OdfNodeFactory.CreateElement("key-columns", DatabaseNamespace, "db");
                    pkNode.AppendChild(keyColsNode);

                    foreach (var pkCol in table.PrimaryKey.Columns)
                    {
                        var keyColNode = OdfNodeFactory.CreateElement("key-column", DatabaseNamespace, "db");
                        keyColNode.SetAttribute("name", DatabaseNamespace, pkCol, "db");
                        keyColsNode.AppendChild(keyColNode);
                    }
                    keysNode.AppendChild(pkNode);
                }

                foreach (var fk in table.ForeignKeys)
                {
                    var fkNode = OdfNodeFactory.CreateElement("key", DatabaseNamespace, "db");
                    fkNode.SetAttribute("type", DatabaseNamespace, "foreign", "db");
                    if (!string.IsNullOrEmpty(fk.Name))
                    {
                        fkNode.SetAttribute("name", DatabaseNamespace, fk.Name!, "db");
                    }
                    fkNode.SetAttribute("referenced-table-name", DatabaseNamespace, fk.ReferencedTable, "db");
                    if (!string.IsNullOrEmpty(fk.UpdateRule))
                    {
                        fkNode.SetAttribute("update-rule", DatabaseNamespace, fk.UpdateRule!, "db");
                    }
                    if (!string.IsNullOrEmpty(fk.DeleteRule))
                    {
                        fkNode.SetAttribute("delete-rule", DatabaseNamespace, fk.DeleteRule!, "db");
                    }

                    var keyColsNode = OdfNodeFactory.CreateElement("key-columns", DatabaseNamespace, "db");
                    fkNode.AppendChild(keyColsNode);

                    foreach (var map in fk.KeyColumns)
                    {
                        var keyColNode = OdfNodeFactory.CreateElement("key-column", DatabaseNamespace, "db");
                        keyColNode.SetAttribute("name", DatabaseNamespace, map.Column, "db");
                        keyColNode.SetAttribute("related-column-name", DatabaseNamespace, map.RelatedColumn, "db");
                        keyColsNode.AppendChild(keyColNode);
                    }
                    keysNode.AppendChild(fkNode);
                }
            }

            // 寫入索引定義
            if (table.Indexes.Count > 0)
            {
                var indicesNode = OdfNodeFactory.CreateElement("indices", DatabaseNamespace, "db");
                tableDef.AppendChild(indicesNode);

                foreach (var index in table.Indexes)
                {
                    var indexNode = OdfNodeFactory.CreateElement("index", DatabaseNamespace, "db");
                    indexNode.SetAttribute("name", DatabaseNamespace, index.Name, "db");
                    if (index.IsUnique)
                    {
                        indexNode.SetAttribute("unique", DatabaseNamespace, "true", "db");
                    }

                    var indexColumnsNode = OdfNodeFactory.CreateElement("index-columns", DatabaseNamespace, "db");
                    indexNode.AppendChild(indexColumnsNode);

                    foreach (var indexColumn in index.Columns)
                    {
                        var indexColumnNode = OdfNodeFactory.CreateElement("index-column", DatabaseNamespace, "db");
                        indexColumnNode.SetAttribute("name", DatabaseNamespace, indexColumn, "db");
                        indexColumnsNode.AppendChild(indexColumnNode);
                    }

                    indicesNode.AppendChild(indexNode);
                }
            }

            // === 寫入表現層 ===
            var tableRep = OdfNodeFactory.CreateElement("table-representation", DatabaseNamespace, "db");
            tableRep.SetAttribute("name", DatabaseNamespace, table.Name, "db");
            tableReps.AppendChild(tableRep);

            var columnsRepNode = OdfNodeFactory.CreateElement("columns", DatabaseNamespace, "db");
            tableRep.AppendChild(columnsRepNode);

            foreach (var col in table.Columns)
            {
                var colRep = OdfNodeFactory.CreateElement("column", DatabaseNamespace, "db");
                colRep.SetAttribute("name", DatabaseNamespace, col.Name, "db");
                columnsRepNode.AppendChild(colRep);
            }
        }
    }

    private OdfNode FindOrCreateChild(OdfNode parent, string localName, string namespaceUri, string prefix)
    {
        var child = FindChildElement(parent, localName, namespaceUri);
        if (child is not null)
        {
            return child;
        }

        child = OdfNodeFactory.CreateElement(localName, namespaceUri, prefix);
        parent.AppendChild(child);
        return child;
    }

    private OdfNode? FindChildElement(OdfNode parent, string localName, string namespaceUri)
    {
        foreach (var child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }
        return null;
    }
}

/// <summary>
/// Represents a table schema structure model.
/// 表示資料表 Schema 結構模型。
/// </summary>
public sealed class OdfSchemaTable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfSchemaTable"/> class.
    /// 初始化 <see cref="OdfSchemaTable"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">The table name. / 資料表名稱。</param>
    /// <exception cref="ArgumentException">When <paramref name="name"/> is empty. / 當 <paramref name="name"/> 為空時擲出。</exception>
    public OdfSchemaTable(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseSchema_DataCannotBeEmpty_2"), nameof(name));
        }
        Name = name;
    }

    /// <summary>
    /// Gets the table name.
    /// 取得資料表名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the list of table columns.
    /// 取得資料表欄位清單。
    /// </summary>
    public List<OdfSchemaColumn> Columns { get; } = [];

    /// <summary>
    /// Gets or sets the primary key definition of the table.
    /// 取得或設定資料表的主鍵定義。
    /// </summary>
    public OdfSchemaPrimaryKey? PrimaryKey { get; set; }

    /// <summary>
    /// Gets the list of foreign key relationship definitions of the table.
    /// 取得資料表的外鍵關聯定義清單。
    /// </summary>
    public List<OdfSchemaForeignKey> ForeignKeys { get; } = [];

    /// <summary>
    /// Gets the list of index definitions of the table.
    /// 取得資料表的索引定義清單。
    /// </summary>
    public List<OdfSchemaIndex> Indexes { get; } = [];
}

/// <summary>
/// Represents a table column definition.
/// 表示資料表欄位定義。
/// </summary>
public sealed class OdfSchemaColumn
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfSchemaColumn"/> class.
    /// 初始化 <see cref="OdfSchemaColumn"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">The column name. / 欄位名稱。</param>
    /// <param name="typeName">The data type name. / 資料型別名稱。</param>
    /// <param name="isNullable">Whether null is allowed. / 是否允許為 null。</param>
    /// <param name="isAutoIncrement">Whether this is an auto-increment column. / 是否為自動遞增欄位。</param>
    /// <exception cref="ArgumentException">When <paramref name="name"/> is empty. / 當 <paramref name="name"/> 為空時擲出。</exception>
    public OdfSchemaColumn(string name, string typeName = "VARCHAR", bool isNullable = true, bool isAutoIncrement = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseSchema_FieldCannotBeEmpty"), nameof(name));
        }
        Name = name;
        TypeName = typeName;
        IsNullable = isNullable;
        IsAutoIncrement = isAutoIncrement;
    }

    /// <summary>
    /// Gets the column name.
    /// 取得欄位名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the data type name.
    /// 取得或設定資料型別名稱。
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this column allows null.
    /// 取得或設定一個值，指示該欄位是否允許為 null。
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this column is auto-incrementing.
    /// 取得或設定一個值，指示該欄位是否為自動遞增欄位。
    /// </summary>
    public bool IsAutoIncrement { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this column has a unique constraint.
    /// 取得或設定一個值，指示該欄位是否具有唯一值約束。
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets or sets the default value expression of the column.
    /// 取得或設定欄位的預設值表達式。
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the check constraint expression of the column.
    /// 取得或設定欄位的檢查約束表達式。
    /// </summary>
    public string? CheckConstraint { get; set; }
}

/// <summary>
/// Represents a table index definition model.
/// 表示資料表索引定義模型。
/// </summary>
public sealed class OdfSchemaIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfSchemaIndex"/> class.
    /// 初始化 <see cref="OdfSchemaIndex"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">The index name. / 索引名稱。</param>
    /// <param name="isUnique">Whether this is a unique index. / 是否為唯一索引。</param>
    /// <param name="columns">The list of column names included in the index. / 索引所包含的欄位名稱清單。</param>
    /// <exception cref="ArgumentException">When <paramref name="name"/> is empty. / 當 <paramref name="name"/> 為空時擲出。</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="columns"/> is <see langword="null"/>. / 當 <paramref name="columns"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfSchemaIndex(string name, bool isUnique, IEnumerable<string> columns)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseSchema_IndexCannotBeEmpty"), nameof(name));
        }

        Name = name;
        IsUnique = isUnique;
        Columns = columns?.ToList() ?? throw new ArgumentNullException(nameof(columns));
    }

    /// <summary>
    /// Gets the index name.
    /// 取得索引名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this index is unique.
    /// 取得或設定一個值，指示該索引是否為唯一索引。
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Gets the list of column names included in the index.
    /// 取得索引所包含的欄位名稱清單。
    /// </summary>
    public List<string> Columns { get; }
}

/// <summary>
/// Represents a primary key definition model.
/// 表示主鍵定義模型。
/// </summary>
public sealed class OdfSchemaPrimaryKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfSchemaPrimaryKey"/> class.
    /// 初始化 <see cref="OdfSchemaPrimaryKey"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">The primary key constraint name. / 主鍵約束名稱。</param>
    /// <param name="columns">The list of column names included in the primary key. / 包含在主鍵中的欄位名稱清單。</param>
    /// <exception cref="ArgumentNullException">When <paramref name="columns"/> is <see langword="null"/>. / 當 <paramref name="columns"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfSchemaPrimaryKey(string? name, IEnumerable<string> columns)
    {
        Name = name;
        Columns = columns?.ToList() ?? throw new ArgumentNullException(nameof(columns));
    }

    /// <summary>
    /// Gets the primary key constraint name.
    /// 取得主鍵約束名稱。
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the list of column names included in the primary key.
    /// 取得主鍵所包含的欄位名稱清單。
    /// </summary>
    public List<string> Columns { get; }
}

/// <summary>
/// Represents a foreign key relationship definition model.
/// 表示外鍵關聯定義模型。
/// </summary>
public sealed class OdfSchemaForeignKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfSchemaForeignKey"/> class.
    /// 初始化 <see cref="OdfSchemaForeignKey"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">The foreign key constraint name. / 外鍵約束名稱。</param>
    /// <param name="referencedTable">The name of the referenced target table. / 被參照的目標資料表名稱。</param>
    /// <param name="keyColumns">The list of foreign-key-to-primary-key column mappings. / 外鍵與主鍵欄位對應清單。</param>
    /// <param name="updateRule">The update rule. / 更新規則。</param>
    /// <param name="deleteRule">The delete rule. / 刪除規則。</param>
    /// <exception cref="ArgumentException">When <paramref name="referencedTable"/> is empty. / 當 <paramref name="referencedTable"/> 為空時擲出。</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="keyColumns"/> is <see langword="null"/>. / 當 <paramref name="keyColumns"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfSchemaForeignKey(
        string? name,
        string referencedTable,
        IEnumerable<OdfSchemaKeyMapping> keyColumns,
        string? updateRule = null,
        string? deleteRule = null)
    {
        if (string.IsNullOrWhiteSpace(referencedTable))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfDatabaseSchema_NameCannotBeEmpty"), nameof(referencedTable));
        }

        Name = name;
        ReferencedTable = referencedTable;
        KeyColumns = keyColumns?.ToList() ?? throw new ArgumentNullException(nameof(keyColumns));
        UpdateRule = updateRule;
        DeleteRule = deleteRule;
    }

    /// <summary>
    /// Gets the foreign key constraint name.
    /// 取得外鍵約束名稱。
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the name of the referenced target table.
    /// 取得被參照的目標資料表名稱。
    /// </summary>
    public string ReferencedTable { get; }

    /// <summary>
    /// Gets the list of foreign-key-to-primary-key column mappings.
    /// 取得外鍵與主鍵欄位的對應清單。
    /// </summary>
    public List<OdfSchemaKeyMapping> KeyColumns { get; }

    /// <summary>
    /// Gets the update rule.
    /// 取得更新規則。
    /// </summary>
    public string? UpdateRule { get; }

    /// <summary>
    /// Gets the delete rule.
    /// 取得刪除規則。
    /// </summary>
    public string? DeleteRule { get; }
}

/// <summary>
/// Represents a mapping between primary key and foreign key columns.
/// 表示主外鍵欄位的對應對照。
/// </summary>
public sealed class OdfSchemaKeyMapping
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfSchemaKeyMapping"/> class.
    /// 初始化 <see cref="OdfSchemaKeyMapping"/> 類別的新執行個體。
    /// </summary>
    /// <param name="column">The column name in the current table. / 目前資料表欄位名稱。</param>
    /// <param name="relatedColumn">The column name in the referenced table. / 參照資料表欄位名稱。</param>
    public OdfSchemaKeyMapping(string column, string relatedColumn)
    {
        Column = column ?? string.Empty;
        RelatedColumn = relatedColumn ?? string.Empty;
    }

    /// <summary>
    /// Gets the column name in the current table.
    /// 取得目前資料表欄位名稱。
    /// </summary>
    public string Column { get; }

    /// <summary>
    /// Gets the column name in the referenced table.
    /// 取得參照資料表欄位名稱。
    /// </summary>
    public string RelatedColumn { get; }
}
