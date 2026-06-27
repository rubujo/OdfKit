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
    /// 初始化 <see cref="AlignedNativeBuffer"/> 類別的新執行個體。
    /// </summary>
    /// <param name="length">緩衝區長度，單位為位元組。</param>
    /// <param name="alignment">對齊邊界，必須為 2 的次方。</param>
    public AlignedNativeBuffer(int length, int alignment)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        _length = length;
        _pointer = NativeMemory.AlignedAlloc((nuint)length, (nuint)alignment);
    }

    /// <inheritdoc />
    public override Span<byte> GetSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new Span<byte>(_pointer, _length);
    }

    /// <inheritdoc />
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if ((uint)elementIndex > (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));

        return new MemoryHandle((byte*)_pointer + elementIndex);
    }

    /// <inheritdoc />
    public override void Unpin()
    {
    }

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
