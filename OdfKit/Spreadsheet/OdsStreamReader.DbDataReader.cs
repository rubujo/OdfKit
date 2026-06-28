using System;
using System.Collections;
using System.Data;
using System.Globalization;
using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

public sealed partial class OdsStreamReader : System.Data.Common.DbDataReader
{
    /// <summary>
    /// 取得目前資料列的巢狀深度。此實作恆傳回 0。
    /// </summary>
    public override int Depth => 0;

    /// <summary>
    /// 取得執行 SQL 陳述式所變更、插入或刪除的資料列數目。此實作恆傳回 -1。
    /// </summary>
    public override int RecordsAffected => -1;

    /// <summary>
    /// 取得一個值，指出資料讀取器是否包含一或多個資料列。
    /// </summary>
    public override bool HasRows => _sheetNames.Count > 0;

    /// <summary>
    /// 取得一個值，指出資料讀取器是否已關閉。
    /// </summary>
    public override bool IsClosed => _closed;

    /// <summary>
    /// 取得指定資料行索引處之資料行的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的值</returns>
    public override object this[int ordinal] => GetValue(ordinal);

    /// <summary>
    /// 取得指定資料行名稱處之資料行的值。
    /// </summary>
    /// <param name="name">資料行的名稱</param>
    /// <returns>該資料行的值</returns>
    public override object this[string name] => GetValue(GetOrdinal(name));

    /// <summary>
    /// 使資料讀取器前進至下一個結果。此實作恆傳回 false。
    /// </summary>
    /// <returns>若有更多結果集，則為 true；否則為 false</returns>
    public override bool NextResult() => false;

    /// <summary>
    /// 取得一個值，指出指定資料行是否包含 Null 或不存在的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>若資料行為 Null 則為 true；否則為 false</returns>
    public override bool IsDBNull(int ordinal) => GetValue(ordinal) is null;

    /// <summary>
    /// 取得指定資料行的名稱。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>資料行的名稱</returns>
    public override string GetName(int ordinal) => $"Column_{ordinal}";

    /// <summary>
    /// 取得指定資料行名稱的資料行索引。
    /// </summary>
    /// <param name="name">資料行的名稱</param>
    /// <returns>以零起始的資料行索引；若找不到則為 -1</returns>
    public override int GetOrdinal(string name)
    {
        if (string.IsNullOrEmpty(name))
            return -1;

        if (name.StartsWith("Column_", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(name.Substring(7), out int col))
        {
            return col;
        }

        if (int.TryParse(name, out int val))
        {
            return val;
        }

        return -1;
    }

    /// <summary>
    /// 取得指定資料行的資料類型名稱。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>資料類型的名稱</returns>
    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    /// <summary>
    /// 取得指定資料行之資料類型的 Type。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的 Type</returns>
    [return: System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetFieldType(int ordinal)
    {
        var val = GetValue(ordinal);
        return val is null ? typeof(object) : val.GetType();
    }

    /// <summary>
    /// 將目前資料列中所有資料行的值複製到指定的陣列中。
    /// </summary>
    /// <param name="values">要將值複製入其中的 Object 陣列</param>
    /// <returns>陣列中被填入值的項目個數</returns>
    public override int GetValues(object[] values)
    {
        if (values is null)
            throw new ArgumentNullException(nameof(values));

        int count = Math.Min(FieldCount, values.Length);
        for (int i = 0; i < count; i++)
        {
            values[i] = GetValue(i) ?? DBNull.Value;
        }
        return count;
    }

    /// <summary>
    /// 取得指定資料行之布林值形式的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的布林值</returns>
    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// 取得指定資料行之 8 位元無號整數值形式的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的位元組值</returns>
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// 從指定的資料行讀取位元組資料流，複製到指定的緩衝區。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <param name="dataOffset">資料行中開始讀取作業的索引</param>
    /// <param name="buffer">要將資料複製入其中的緩衝區</param>
    /// <param name="bufferOffset">緩衝區中開始寫入作業的索引</param>
    /// <param name="length">要複製的最大位元組數</param>
    /// <returns>實際複製的位元組數</returns>
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        var val = GetValue(ordinal);
        if (val is byte[] bytes)
        {
            if (buffer is null)
                return bytes.Length;

            int srcLen = Math.Max(0, bytes.Length - (int)dataOffset);
            int copyLen = Math.Min(srcLen, length);
            if (copyLen > 0)
            {
                Buffer.BlockCopy(bytes, (int)dataOffset, buffer, bufferOffset, copyLen);
            }
            return copyLen;
        }

        if (val is string str)
        {
            byte[] strBytes = System.Text.Encoding.UTF8.GetBytes(str);
            if (buffer is null)
                return strBytes.Length;

            int srcLen = Math.Max(0, strBytes.Length - (int)dataOffset);
            int copyLen = Math.Min(srcLen, length);
            if (copyLen > 0)
            {
                Buffer.BlockCopy(strBytes, (int)dataOffset, buffer, bufferOffset, copyLen);
            }
            return copyLen;
        }

        throw new NotSupportedException(OdfLocalizer.GetMessage("Err_DbDataReader_GetBytesNotSupported"));
    }

    /// <summary>
    /// 取得指定資料行之字元形式的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的字元值</returns>
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// 從指定的資料行讀取字元資料流，複製到指定的緩衝區。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <param name="dataOffset">資料行中開始讀取作業的索引</param>
    /// <param name="buffer">要將資料複製入其中的緩衝區</param>
    /// <param name="bufferOffset">緩衝區中開始寫入作業的索引</param>
    /// <param name="length">要複製的最大字元數</param>
    /// <returns>實際複製的字元數</returns>
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        var val = GetValue(ordinal);
        if (val is string str)
        {
            if (buffer is null)
                return str.Length;

            int srcLen = Math.Max(0, str.Length - (int)dataOffset);
            int copyLen = Math.Min(srcLen, length);
            if (copyLen > 0)
            {
                str.CopyTo((int)dataOffset, buffer, bufferOffset, copyLen);
            }
            return copyLen;
        }

        throw new NotSupportedException(OdfLocalizer.GetMessage("Err_DbDataReader_GetCharsNotSupported"));
    }

    /// <summary>
    /// 取得指定資料行之全域唯一識別碼 (GUID) 形式的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的 GUID 值</returns>
    public override Guid GetGuid(int ordinal)
    {
        var val = GetValue(ordinal);
        if (val is Guid g)
            return g;

        if (val is string s && Guid.TryParse(s, out Guid parsed))
            return parsed;

        throw new InvalidCastException(OdfLocalizer.GetMessage("Err_DbDataReader_CannotCastToGuid"));
    }

    /// <summary>
    /// 取得指定資料行之 16 位元有號整數值形式的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的 16 位元有號整數值</returns>
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// 取得指定資料行之 32 位元有號整數值形式的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的 32 位元有號整數值</returns>
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// 取得指定資料行之 64 位元有號整數值形式的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的 64 位元有號整數值</returns>
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// 取得指定資料行之單精確度浮點數值形式的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的單精確度浮點數值</returns>
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// 取得指定資料行之倍精確度浮點數值形式的值.
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的倍精確度浮點數值</returns>
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// 取得指定資料行之字串形式的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的字串值</returns>
    public override string GetString(int ordinal)
    {
        var val = GetValue(ordinal);
        if (val is null)
            return string.Empty;
        return Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// 取得指定資料行之十進位數值形式的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的十進位數值</returns>
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// 取得指定資料行之 DateTime 形式的值。
    /// </summary>
    /// <param name="ordinal">以零起始的資料行索引</param>
    /// <returns>該資料行的 DateTime 值</returns>
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// 傳回可用來逐一查看資料列的列舉程式。
    /// </summary>
    /// <returns>一個 IEnumerator，可用於逐一查看結果</returns>
    public override IEnumerator GetEnumerator() => new System.Data.Common.DbEnumerator(this, true);

    /// <summary>
    /// 傳回說明 DbDataReader 之中繼資料的 DataTable。此實作恆傳回 null。
    /// </summary>
    /// <returns>一個 DataTable，描述此讀取器的結構描述中繼資料；或是 null</returns>
    public override DataTable? GetSchemaTable() => null;
}
