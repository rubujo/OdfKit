using System;
using System.Collections;
using System.Data;
using System.Globalization;
using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

public sealed partial class OdsStreamReader : System.Data.Common.DbDataReader
{
    /// <summary>
    /// Gets the nesting depth of the current row. This implementation always returns 0.
    /// 取得目前資料列的巢狀深度。此實作恆傳回 0。
    /// </summary>
    public override int Depth => 0;

    /// <summary>
    /// Gets the number of rows changed, inserted, or deleted by executing the SQL statement. This implementation always returns -1.
    /// 取得執行 SQL 陳述式所變更、插入或刪除的資料列數目。此實作恆傳回 -1。
    /// </summary>
    public override int RecordsAffected => -1;

    /// <summary>
    /// Gets a value indicating whether the data reader contains one or more rows.
    /// 取得一個值，指出資料讀取器是否包含一或多個資料列。
    /// </summary>
    public override bool HasRows => _sheetNames.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the data reader is closed.
    /// 取得一個值，指出資料讀取器是否已關閉。
    /// </summary>
    public override bool IsClosed => _closed;

    /// <summary>
    /// Gets the value of the column at the specified column index.
    /// 取得指定資料行索引處之資料行的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The value of the column. / 該資料行的值。</returns>
    public override object this[int ordinal] => GetValue(ordinal);

    /// <summary>
    /// Gets the value of the column with the specified column name.
    /// 取得指定資料行名稱處之資料行的值。
    /// </summary>
    /// <param name="name">The column name. / 資料行的名稱。</param>
    /// <returns>The value of the column. / 該資料行的值。</returns>
    public override object this[string name] => GetValue(GetOrdinal(name));

    /// <summary>
    /// Advances the data reader to the next result. This implementation always returns <see langword="false"/>.
    /// 使資料讀取器前進至下一個結果。此實作恆傳回 false。
    /// </summary>
    /// <returns><see langword="true"/> if more result sets exist; otherwise, <see langword="false"/>. / 若有更多結果集，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public override bool NextResult() => false;

    /// <summary>
    /// Gets a value indicating whether the specified column contains a null or missing value.
    /// 取得一個值，指出指定資料行是否包含 Null 或不存在的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns><see langword="true"/> if the column is null; otherwise, <see langword="false"/>. / 若資料行為 <see langword="null"/> 則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public override bool IsDBNull(int ordinal) => GetValue(ordinal) is null;

    /// <summary>
    /// Gets the name of the specified column.
    /// 取得指定資料行的名稱。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The column name. / 資料行的名稱。</returns>
    public override string GetName(int ordinal) => $"Column_{ordinal}";

    /// <summary>
    /// Gets the column index for the specified column name.
    /// 取得指定資料行名稱的資料行索引。
    /// </summary>
    /// <param name="name">The column name. / 資料行的名稱。</param>
    /// <returns>The zero-based column index, or -1 if the column is not found. / 採零起始的資料行索引；若找不到則為 -1。</returns>
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
    /// Gets the data type name of the specified column.
    /// 取得指定資料行的資料類型名稱。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The data type name. / 資料類型的名稱。</returns>
    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    /// <summary>
    /// Gets the <see cref="Type"/> of the data type of the specified column.
    /// 取得指定資料行之資料類型的 Type。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The <see cref="Type"/> of the column. / 該資料行的 <see cref="Type"/>。</returns>
    [return: System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetFieldType(int ordinal)
    {
        var val = GetValue(ordinal);
        return val is null ? typeof(object) : val.GetType();
    }

    /// <summary>
    /// Copies the values of all columns in the current row into the specified array.
    /// 將目前資料列中所有資料行的值複製到指定的陣列中。
    /// </summary>
    /// <param name="values">The <see cref="object"/> array into which values are copied. / 要將值複製入其中的 <see cref="object"/> 陣列。</param>
    /// <returns>The number of array items populated with values. / 陣列中被填入值的項目個數。</returns>
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
    /// Gets the value of the specified column as a Boolean.
    /// 取得指定資料行之布林值形式的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The Boolean value of the column. / 該資料行的布林值。</returns>
    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the value of the specified column as an 8-bit unsigned integer.
    /// 取得指定資料行之 8 位元無號整數值形式的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The byte value of the column. / 該資料行的位元組值。</returns>
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a stream of bytes from the specified column and copies it into the specified buffer.
    /// 從指定的資料行讀取位元組資料流，複製到指定的緩衝區。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <param name="dataOffset">The index in the column where the read operation begins. / 資料行中開始讀取作業的索引。</param>
    /// <param name="buffer">The buffer into which data is copied. / 要將資料複製入其中的緩衝區。</param>
    /// <param name="bufferOffset">The index in the buffer where the write operation begins. / 緩衝區中開始寫入作業的索引。</param>
    /// <param name="length">The maximum number of bytes to copy. / 要複製的最大位元組數。</param>
    /// <returns>The actual number of bytes copied. / 實際複製的位元組數。</returns>
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
    /// Gets the value of the specified column as a character.
    /// 取得指定資料行之字元形式的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The character value of the column. / 該資料行的字元值。</returns>
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a stream of characters from the specified column and copies it into the specified buffer.
    /// 從指定的資料行讀取字元資料流，複製到指定的緩衝區。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <param name="dataOffset">The index in the column where the read operation begins. / 資料行中開始讀取作業的索引。</param>
    /// <param name="buffer">The buffer into which data is copied. / 要將資料複製入其中的緩衝區。</param>
    /// <param name="bufferOffset">The index in the buffer where the write operation begins. / 緩衝區中開始寫入作業的索引。</param>
    /// <param name="length">The maximum number of characters to copy. / 要複製的最大字元數。</param>
    /// <returns>The actual number of characters copied. / 實際複製的字元數。</returns>
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
    /// Gets the value of the specified column as a globally unique identifier (GUID).
    /// 取得指定資料行之全域唯一識別碼 (GUID) 形式的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The GUID value of the column. / 該資料行的 GUID 值。</returns>
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
    /// Gets the value of the specified column as a 16-bit signed integer.
    /// 取得指定資料行之 16 位元有號整數值形式的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The 16-bit signed integer value of the column. / 該資料行的 16 位元有號整數值。</returns>
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the value of the specified column as a 32-bit signed integer.
    /// 取得指定資料行之 32 位元有號整數值形式的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The 32-bit signed integer value of the column. / 該資料行的 32 位元有號整數值。</returns>
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the value of the specified column as a 64-bit signed integer.
    /// 取得指定資料行之 64 位元有號整數值形式的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The 64-bit signed integer value of the column. / 該資料行的 64 位元有號整數值。</returns>
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the value of the specified column as a single-precision floating-point number.
    /// 取得指定資料行之單精確度浮點數值形式的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The single-precision floating-point value of the column. / 該資料行的單精確度浮點數值。</returns>
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the value of the specified column as a double-precision floating-point number.
    /// 取得指定資料行之倍精確度浮點數值形式的值.
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The double-precision floating-point value of the column. / 該資料行的倍精確度浮點數值。</returns>
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the value of the specified column as a string.
    /// 取得指定資料行之字串形式的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The string value of the column. / 該資料行的字串值。</returns>
    public override string GetString(int ordinal)
    {
        var val = GetValue(ordinal);
        if (val is null)
            return string.Empty;
        return Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>
    /// Gets the value of the specified column as a decimal number.
    /// 取得指定資料行之十進位數值形式的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The decimal value of the column. / 該資料行的十進位數值。</returns>
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the value of the specified column as a <see cref="DateTime"/>.
    /// 取得指定資料行之 DateTime 形式的值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The <see cref="DateTime"/> value of the column. / 該資料行的 <see cref="DateTime"/> 值。</returns>
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal), CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns an enumerator that can be used to iterate through rows.
    /// 傳回可用來逐一查看資料列的列舉程式。
    /// </summary>
    /// <returns>An <see cref="IEnumerator"/> that can be used to iterate through results. / 一個 <see cref="IEnumerator"/>，可用於逐一查看結果。</returns>
    public override IEnumerator GetEnumerator() => new System.Data.Common.DbEnumerator(this, true);

    /// <summary>
    /// Returns a <see cref="DataTable"/> that describes the <see cref="System.Data.Common.DbDataReader"/> metadata. This implementation always returns <see langword="null"/>.
    /// 傳回說明 DbDataReader 之中繼資料的 DataTable。此實作恆傳回 null。
    /// </summary>
    /// <returns>A <see cref="DataTable"/> that describes this reader's schema metadata, or <see langword="null"/>. / 一個 <see cref="DataTable"/>，描述此讀取器的結構描述中繼資料；或是 <see langword="null"/>。</returns>
    public override DataTable? GetSchemaTable() => null;
}
