using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Adapts an <see cref="IEnumerable{T}"/> or <see cref="IAsyncEnumerable{T}"/> of arbitrary objects to a <see cref="System.Data.Common.DbDataReader"/>, mapping each readable public instance property of <typeparamref name="T"/> to a column. This lets any object sequence (for example an Entity Framework Core query projection) flow through <see cref="OdsStreamWriter.WriteDataAsync(System.Data.Common.DbDataReader, bool, CancellationToken)"/> or any other <see cref="System.Data.Common.DbDataReader"/> consumer such as <c>SqlBulkCopy</c>, without OdfKit depending on any specific ORM or database provider.
/// 將任意 <see cref="IEnumerable{T}"/> 或 <see cref="IAsyncEnumerable{T}"/> 物件序列轉接為 <see cref="System.Data.Common.DbDataReader"/>，把 <typeparamref name="T"/> 的每個可讀公開執行個體屬性對應到一個資料行。這讓任意物件序列（例如 Entity Framework Core 查詢投影）可以直接餵給 <see cref="OdsStreamWriter.WriteDataAsync(System.Data.Common.DbDataReader, bool, CancellationToken)"/> 或其他 <see cref="System.Data.Common.DbDataReader"/> 消費者（如 <c>SqlBulkCopy</c>），且 OdfKit 本身不需相依任何特定 ORM 或資料庫 provider。
/// </summary>
/// <typeparam name="T">The element type whose readable public instance properties become data reader columns. / 元素型別，其可讀公開執行個體屬性將成為資料讀取器的資料行。</typeparam>
public sealed class ObjectDataReader<
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] T>
    : System.Data.Common.DbDataReader
{
    private static readonly ColumnAccessor[] Columns = BuildColumns();
    private static readonly Dictionary<string, int> OrdinalsByName = BuildOrdinalLookup();

    private readonly IEnumerator<T>? _syncEnumerator;
    private readonly IAsyncEnumerator<T>? _asyncEnumerator;
    private readonly object?[] _currentValues = new object?[Columns.Length];
    private bool _hasCurrentRow;
    private bool _disposed;

    /// <summary>
    /// Initializes an <see cref="ObjectDataReader{T}"/> from a synchronous element source.
    /// 從同步元素來源初始化 <see cref="ObjectDataReader{T}"/>。
    /// </summary>
    /// <param name="source">The element sequence to read. / 要讀取的元素序列。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>. / 當 <paramref name="source"/> 為 <see langword="null"/> 時擲出。</exception>
    public ObjectDataReader(IEnumerable<T> source)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        ThrowIfNoReadableProperties();

        _syncEnumerator = source.GetEnumerator();
    }

    /// <summary>
    /// Initializes an <see cref="ObjectDataReader{T}"/> from an asynchronous element source, such as an Entity Framework Core <c>IQueryable&lt;T&gt;.AsAsyncEnumerable()</c> projection.
    /// 從非同步元素來源（例如 Entity Framework Core <c>IQueryable&lt;T&gt;.AsAsyncEnumerable()</c> 投影）初始化 <see cref="ObjectDataReader{T}"/>。
    /// </summary>
    /// <param name="source">The asynchronous element sequence to read. / 要讀取的非同步元素序列。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>. / 當 <paramref name="source"/> 為 <see langword="null"/> 時擲出。</exception>
    public ObjectDataReader(IAsyncEnumerable<T> source)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        ThrowIfNoReadableProperties();

        _asyncEnumerator = source.GetAsyncEnumerator();
    }

    private static void ThrowIfNoReadableProperties()
    {
        if (Columns.Length == 0)
        {
            throw new ArgumentException(
                OdfLocalizer.GetMessage("Err_ObjectDataReader_NoReadableProperties", typeof(T).FullName ?? typeof(T).Name),
                nameof(T));
        }
    }

    /// <summary>
    /// Gets the number of columns mapped from the readable public instance properties of <typeparamref name="T"/>.
    /// 取得從 <typeparamref name="T"/> 可讀公開執行個體屬性對應出的資料行數目。
    /// </summary>
    public override int FieldCount => Columns.Length;

    /// <summary>
    /// Gets the nesting depth of the current row. This implementation always returns 0.
    /// 取得目前資料列的巢狀深度。此實作恆傳回 0。
    /// </summary>
    public override int Depth => 0;

    /// <summary>
    /// Gets the number of rows changed, inserted, or deleted by executing the statement. This implementation always returns -1.
    /// 取得執行陳述式所變更、插入或刪除的資料列數目。此實作恆傳回 -1。
    /// </summary>
    public override int RecordsAffected => -1;

    /// <summary>
    /// Gets a value indicating whether the data reader contains one or more rows. This implementation always returns <see langword="true"/> because the underlying sequence is not pre-enumerated.
    /// 取得一個值，指出資料讀取器是否包含一或多個資料列。因為底層序列不會預先列舉，此實作恆傳回 <see langword="true"/>。
    /// </summary>
    public override bool HasRows => true;

    /// <summary>
    /// Gets a value indicating whether the data reader is closed.
    /// 取得一個值，指出資料讀取器是否已關閉。
    /// </summary>
    public override bool IsClosed => _disposed;

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
    /// Advances to the next row of a synchronous source. Calling this on an instance constructed from an <see cref="IAsyncEnumerable{T}"/> synchronously blocks on the underlying asynchronous enumerator; callers with an asynchronous source should call <see cref="ReadAsync(CancellationToken)"/> instead.
    /// 讓同步來源前進至下一列。若對以 <see cref="IAsyncEnumerable{T}"/> 建構的執行個體呼叫此方法，會同步阻塞等待底層非同步列舉；非同步來源的呼叫端應改呼叫 <see cref="ReadAsync(CancellationToken)"/>。
    /// </summary>
    /// <returns><see langword="true"/> if there is another row; otherwise, <see langword="false"/>. / 若有下一列則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public override bool Read()
    {
        ThrowIfDisposed();

        if (_syncEnumerator is not null)
        {
            _hasCurrentRow = _syncEnumerator.MoveNext();
        }
        else
        {
            _hasCurrentRow = _asyncEnumerator!.MoveNextAsync().AsTask().GetAwaiter().GetResult();
        }

        if (_hasCurrentRow)
        {
            PopulateCurrentValues();
        }

        return _hasCurrentRow;
    }

    /// <summary>
    /// Asynchronously advances to the next row.
    /// 非同步讓資料讀取器前進至下一列。
    /// </summary>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task whose result is <see langword="true"/> if there is another row; otherwise, <see langword="false"/>. / 一個工作，其結果於有下一列時為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        _hasCurrentRow = _asyncEnumerator is not null
            ? await _asyncEnumerator.MoveNextAsync().ConfigureAwait(false)
            : _syncEnumerator!.MoveNext();

        if (_hasCurrentRow)
        {
            PopulateCurrentValues();
        }

        return _hasCurrentRow;
    }

    private void PopulateCurrentValues()
    {
        T current = _syncEnumerator is not null ? _syncEnumerator.Current : _asyncEnumerator!.Current;
        for (int i = 0; i < Columns.Length; i++)
        {
            _currentValues[i] = Columns[i].Getter(current);
        }
    }

    /// <summary>
    /// Advances to the next result. This implementation always returns <see langword="false"/>.
    /// 使資料讀取器前進至下一個結果。此實作恆傳回 <see langword="false"/>。
    /// </summary>
    /// <returns><see langword="true"/> if more result sets exist; otherwise, <see langword="false"/>. / 若有更多結果集，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public override bool NextResult() => false;

    /// <summary>
    /// Gets a value indicating whether the specified column contains a null value.
    /// 取得一個值，指出指定資料行是否包含 Null 值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns><see langword="true"/> if the column is null; otherwise, <see langword="false"/>. / 若資料行為 <see langword="null"/> 則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public override bool IsDBNull(int ordinal) => GetValue(ordinal) is null;

    /// <summary>
    /// Gets the name of the specified column, taken from the mapped property name.
    /// 取得指定資料行的名稱（取自對應的屬性名稱）。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The column name. / 資料行的名稱。</returns>
    public override string GetName(int ordinal) => Columns[ordinal].Name;

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

        return OrdinalsByName.TryGetValue(name, out int ordinal) ? ordinal : -1;
    }

    /// <summary>
    /// Gets the data type name of the specified column.
    /// 取得指定資料行的資料類型名稱。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The data type name. / 資料類型的名稱。</returns>
    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    /// <summary>
    /// Gets the <see cref="Type"/> of the specified column, taken from the mapped property type.
    /// 取得指定資料行的 <see cref="Type"/>（取自對應的屬性型別）。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The <see cref="Type"/> of the column. / 該資料行的 <see cref="Type"/>。</returns>
    public override Type GetFieldType(int ordinal) => Columns[ordinal].PropertyType;

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
    /// Gets the raw value of the specified column in the current row.
    /// 取得目前資料列指定資料行的原始值。
    /// </summary>
    /// <param name="ordinal">The zero-based column index. / 採零起始的資料行索引。</param>
    /// <returns>The value of the column. / 該資料行的值。</returns>
    public override object GetValue(int ordinal)
    {
        ThrowIfDisposed();
        return _currentValues[ordinal]!;
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
    /// 取得指定資料行之倍精確度浮點數值形式的值。
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
    /// 取得指定資料行之 <see cref="DateTime"/> 形式的值。
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
    /// Returns a <see cref="DataTable"/> that describes the reader metadata. This implementation always returns <see langword="null"/>.
    /// 傳回說明讀取器中繼資料的 <see cref="DataTable"/>。此實作恆傳回 <see langword="null"/>。
    /// </summary>
    /// <returns>A <see cref="DataTable"/> that describes this reader's schema metadata, or <see langword="null"/>. / 一個 <see cref="DataTable"/>，描述此讀取器的結構描述中繼資料；或是 <see langword="null"/>。</returns>
    public override DataTable? GetSchemaTable() => null;

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ObjectDataReader<T>), OdfLocalizer.GetMessage("Err_ObjectDataReader_Disposed"));
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _syncEnumerator?.Dispose();
            if (_asyncEnumerator is not null)
            {
                _asyncEnumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        base.Dispose(disposing);
    }

    private readonly struct ColumnAccessor(string name, Type propertyType, Func<T, object?> getter)
    {
        public string Name { get; } = name;

        public Type PropertyType { get; } = propertyType;

        public Func<T, object?> Getter { get; } = getter;
    }

    private static ColumnAccessor[] BuildColumns()
    {
        // 只組裝欄位描述、不在型別初始設定式擲出例外：靜態欄位初始設定式失敗會被包成
        // TypeInitializationException 且永久快取失敗狀態，導致同一封閉泛型型別之後
        // 每次存取都固定擲出該例外，無法復原。改由建構子（ThrowIfNoReadableProperties）
        // 在每次實例化時檢查並擲出可預期、可攔截的 ArgumentException。
        PropertyInfo[] properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        var columns = new ColumnAccessor[properties.Length];
        for (int i = 0; i < properties.Length; i++)
        {
            columns[i] = new ColumnAccessor(properties[i].Name, properties[i].PropertyType, BuildGetter(properties[i]));
        }
        return columns;
    }

    private static Func<T, object?> BuildGetter(PropertyInfo property)
    {
        ParameterExpression instance = Expression.Parameter(typeof(T), "instance");
        MemberExpression access = Expression.Property(instance, property);
        UnaryExpression convert = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<T, object?>>(convert, instance).Compile();
    }

    private static Dictionary<string, int> BuildOrdinalLookup()
    {
        var lookup = new Dictionary<string, int>(Columns.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Columns.Length; i++)
        {
            lookup[Columns[i].Name] = i;
        }
        return lookup;
    }
}
