using System;
using OdfKit.Core;
using OdfKit.Database;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 Database 文件 mutation API 的邊界與負向案例。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public class DatabaseBoundaryTests
{
    /// <summary>
    /// 驗證 <see cref="DatabaseDocument.AddTable"/>／<see cref="DatabaseDocument.AddQuery"/>
    /// 在名稱（與查詢命令）為空白時擲出 <see cref="ArgumentException"/>。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddTable_BlankName_ThrowsArgumentException(string blankName)
    {
        using var database = DatabaseDocument.Create();
        Assert.Throws<ArgumentException>(() => database.AddTable(blankName));
    }

    /// <summary>
    /// 驗證 <see cref="DatabaseDocument.AddQuery"/> 在名稱或命令為空白時擲出 <see cref="ArgumentException"/>。
    /// </summary>
    [Fact]
    public void AddQuery_BlankNameOrCommand_ThrowsArgumentException()
    {
        using var database = DatabaseDocument.Create();
        Assert.Throws<ArgumentException>(() => database.AddQuery("", "SELECT 1"));
        Assert.Throws<ArgumentException>(() => database.AddQuery("Query1", ""));
    }

    /// <summary>
    /// 驗證以相同名稱重複新增資料表／查詢時擲出 <see cref="InvalidOperationException"/>，
    /// 避免產生重複鍵導致 ODB 載入失敗。
    /// </summary>
    [Fact]
    public void AddTableOrQuery_DuplicateName_ThrowsInvalidOperationException()
    {
        using var database = DatabaseDocument.Create();

        database.AddTable("Customers");
        Assert.Throws<InvalidOperationException>(() => database.AddTable("Customers"));

        database.AddQuery("Q1", "SELECT 1");
        Assert.Throws<InvalidOperationException>(() => database.AddQuery("Q1", "SELECT 2"));

        // 移除後應可再次新增同名項目
        Assert.True(database.RemoveTable("Customers"));
        database.AddTable("Customers");
    }

    /// <summary>
    /// 驗證 <see cref="DatabaseDocument.RemoveTable"/>／<see cref="DatabaseDocument.RemoveQuery"/>／
    /// <see cref="DatabaseDocument.RemoveDataSourceSetting"/> 在目標不存在時回傳
    /// <see langword="false"/>，而非擲出例外或靜默忽略。
    /// </summary>
    [Fact]
    public void RemoveOperations_NonExistentName_ReturnsFalse()
    {
        using var database = DatabaseDocument.Create();

        Assert.False(database.RemoveTable("NotExist"));
        Assert.False(database.RemoveQuery("NotExist"));
        Assert.False(database.RemoveDataSourceSetting("NotExist"));

        database.AddTable("Customers");
        Assert.False(database.RemoveTable("OtherTable"));
        Assert.True(database.RemoveTable("Customers"));
    }

    /// <summary>
    /// 驗證 <see cref="DatabaseDocument.GetForms"/> 面對極深層巢狀 db:component-collection 時，
    /// 以 <see cref="System.IO.InvalidDataException"/> 中止，而非引發 StackOverflowException 使進程崩潰。
    /// </summary>
    [Fact]
    public void GetForms_DeeplyNestedComponentCollection_ThrowsInsteadOfStackOverflow()
    {
        const string dbNs = "urn:oasis:names:tc:opendocument:xmlns:database:1.0";

        using var database = DatabaseDocument.Create();

        // 先透過公開 API 建立 db:forms 節點，再於其下堆疊遠超深度上限的巢狀 collection
        database.AddForm("Root");
        OdfNode body = FindChild(database.ContentDom, "body", OdfNamespaces.Office);
        OdfNode databaseNode = FindChild(body, "database", OdfNamespaces.Office);
        OdfNode forms = FindChild(databaseNode, "forms", dbNs);

        OdfNode cursor = forms;
        for (int i = 0; i < 5000; i++)
        {
            var collection = new OdfNode(OdfNodeType.Element, "component-collection", dbNs, "db");
            cursor.AppendChild(collection);
            cursor = collection;
        }

        Assert.Throws<System.IO.InvalidDataException>(() => database.GetForms());
    }

    private static OdfNode FindChild(OdfNode parent, string localName, string namespaceUri)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }

        throw new InvalidOperationException($"找不到子節點 {localName}。");
    }

    /// <summary>
    /// 驗證 <see cref="DatabaseDocument.RemoveTable"/>／<see cref="DatabaseDocument.RemoveQuery"/>／
    /// <see cref="DatabaseDocument.RemoveDataSourceSetting"/> 在名稱為空白時擲出 <see cref="ArgumentException"/>。
    /// </summary>
    [Fact]
    public void RemoveOperations_BlankName_ThrowsArgumentException()
    {
        using var database = DatabaseDocument.Create();

        Assert.Throws<ArgumentException>(() => database.RemoveTable(""));
        Assert.Throws<ArgumentException>(() => database.RemoveQuery(""));
        Assert.Throws<ArgumentException>(() => database.RemoveDataSourceSetting(""));
    }

    /// <summary>
    /// 驗證 <see cref="DatabaseDocument.FindTable"/>／<see cref="DatabaseDocument.FindQuery"/>／
    /// <see cref="DatabaseDocument.FindDataSourceSetting"/> 在目標不存在時回傳
    /// <see langword="null"/>，且不影響既有資料表／查詢／設定的查詢結果。
    /// </summary>
    [Fact]
    public void FindOperations_NonExistentName_ReturnsNull()
    {
        using var database = DatabaseDocument.Create();
        database.AddTable("Customers", "SELECT * FROM \"Customers\"");
        database.AddQuery("Query1", "SELECT 1");

        Assert.Null(database.FindTable("NotExist"));
        Assert.Null(database.FindQuery("NotExist"));
        Assert.Null(database.FindDataSourceSetting("NotExist"));

        Assert.NotNull(database.FindTable("Customers"));
        Assert.NotNull(database.FindQuery("Query1"));
    }
}
