using System;
using OdfKit.Core;
using OdfKit.Database;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 Database 文件 mutation API 的邊界與負向案例。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public class DatabaseBoundaryTests
{
    /// <summary>
    /// 驗證 <see cref="OdfDatabaseDocument.AddTable"/>／<see cref="OdfDatabaseDocument.AddQuery"/>
    /// 在名稱（與查詢命令）為空白時擲出 <see cref="ArgumentException"/>。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddTable_BlankName_ThrowsArgumentException(string blankName)
    {
        using var database = OdfDatabaseDocument.Create();
        Assert.Throws<ArgumentException>(() => database.AddTable(blankName));
    }

    /// <summary>
    /// 驗證 <see cref="OdfDatabaseDocument.AddQuery"/> 在名稱或命令為空白時擲出 <see cref="ArgumentException"/>。
    /// </summary>
    [Fact]
    public void AddQuery_BlankNameOrCommand_ThrowsArgumentException()
    {
        using var database = OdfDatabaseDocument.Create();
        Assert.Throws<ArgumentException>(() => database.AddQuery("", "SELECT 1"));
        Assert.Throws<ArgumentException>(() => database.AddQuery("Query1", ""));
    }

    /// <summary>
    /// 驗證 <see cref="OdfDatabaseDocument.RemoveTable"/>／<see cref="OdfDatabaseDocument.RemoveQuery"/>／
    /// <see cref="OdfDatabaseDocument.RemoveDataSourceSetting"/> 在目標不存在時回傳
    /// <see langword="false"/>，而非擲出例外或靜默忽略。
    /// </summary>
    [Fact]
    public void RemoveOperations_NonExistentName_ReturnsFalse()
    {
        using var database = OdfDatabaseDocument.Create();

        Assert.False(database.RemoveTable("NotExist"));
        Assert.False(database.RemoveQuery("NotExist"));
        Assert.False(database.RemoveDataSourceSetting("NotExist"));

        database.AddTable("Customers");
        Assert.False(database.RemoveTable("OtherTable"));
        Assert.True(database.RemoveTable("Customers"));
    }

    /// <summary>
    /// 驗證 <see cref="OdfDatabaseDocument.RemoveTable"/>／<see cref="OdfDatabaseDocument.RemoveQuery"/>／
    /// <see cref="OdfDatabaseDocument.RemoveDataSourceSetting"/> 在名稱為空白時擲出 <see cref="ArgumentException"/>。
    /// </summary>
    [Fact]
    public void RemoveOperations_BlankName_ThrowsArgumentException()
    {
        using var database = OdfDatabaseDocument.Create();

        Assert.Throws<ArgumentException>(() => database.RemoveTable(""));
        Assert.Throws<ArgumentException>(() => database.RemoveQuery(""));
        Assert.Throws<ArgumentException>(() => database.RemoveDataSourceSetting(""));
    }

    /// <summary>
    /// 驗證 <see cref="OdfDatabaseDocument.FindTable"/>／<see cref="OdfDatabaseDocument.FindQuery"/>／
    /// <see cref="OdfDatabaseDocument.FindDataSourceSetting"/> 在目標不存在時回傳
    /// <see langword="null"/>，且不影響既有資料表／查詢／設定的查詢結果。
    /// </summary>
    [Fact]
    public void FindOperations_NonExistentName_ReturnsNull()
    {
        using var database = OdfDatabaseDocument.Create();
        database.AddTable("Customers", "SELECT * FROM \"Customers\"");
        database.AddQuery("Query1", "SELECT 1");

        Assert.Null(database.FindTable("NotExist"));
        Assert.Null(database.FindQuery("NotExist"));
        Assert.Null(database.FindDataSourceSetting("NotExist"));

        Assert.NotNull(database.FindTable("Customers"));
        Assert.NotNull(database.FindQuery("Query1"));
    }
}
