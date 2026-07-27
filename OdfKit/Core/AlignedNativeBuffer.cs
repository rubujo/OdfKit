#if NET10_0_OR_GREATER
using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace OdfKit.Core;

/// <summary>
/// 提供以指定邊界對齊的非受控位元組緩衝區。
/// </summary>
internal sealed unsafe class AlignedNativeBuffer : MemoryManager<byte>
{
    private readonly int _length;
    private void* _pointer;
    private bool _disposed;

    /// <summary>
    /// Performs aligned native buffer.
    /// 初始化 <see cref="AlignedNativeBuffer"/> 類別的新執行個體。
    /// </summary>
    /// <param name="length">緩衝區長度，單位為位元組。</param>
    /// <param name="alignment">對齊邊界，必須為 2 的次方。</param>
    public AlignedNativeBuffer(int length, int alignment)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNegative(length, nameof(length));

        _length = length;
        _pointer = NativeMemory.AlignedAlloc((nuint)length, (nuint)alignment);
    }

    /// <summary>
    /// Gets span.
    /// 取得 Span。
    /// </summary>
    /// <inheritdoc />
    public override Span<byte> GetSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new Span<byte>(_pointer, _length);
    }
    /// <summary>
    /// Short overload of Pin that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：Pin 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public MemoryHandle Pin() => Pin(0);


    /// <summary>
    /// Performs the Pin operation.
    /// 執行 Pin 作業。
    /// </summary>
    /// <inheritdoc />
    public override MemoryHandle Pin(int elementIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if ((uint)elementIndex > (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));

        return new MemoryHandle((byte*)_pointer + elementIndex);
    }


    /// <summary>
    /// Performs the Unpin operation.
    /// 執行 Unpin 作業。
    /// </summary>
    /// <inheritdoc />
    public override void Unpin()
    {
    }

    /// <summary>
    /// Releases unmanaged resources.
    /// 釋放非受控資源。
    /// </summary>
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        NativeMemory.AlignedFree(_pointer);
        _pointer = null;
        _disposed = true;
    }
}
#endif
