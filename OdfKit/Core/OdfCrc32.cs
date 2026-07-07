using System;
using System.Runtime.CompilerServices;

namespace OdfKit.Core;

/// <summary>
/// Computes CRC-32 (ISO-HDLC) checksums with hardware acceleration when available.
/// 在可用時使用硬體加速計算 CRC-32 (ISO-HDLC) 校驗碼。
/// </summary>
public static class OdfCrc32
{
    // 延遲初始化的 Slice-by-8 查表：僅在實際走軟體路徑時才建置，
    // 避免 ARM64 硬體加速或 net10 單次計算路徑仍付出 8KB 冷啟動配置。
    // 競爭時最多重複建置一次後被取代，結果相同，無需鎖定。
    private static uint[][]? _tables;

    private static uint[][] Tables => _tables ??= CreateTables();

    private static uint[][] CreateTables()
    {
        var tables = new uint[8][];
        for (int i = 0; i < 8; i++)
        {
            tables[i] = new uint[256];
        }

        const uint polynomial = 0xEDB88320;
        // 建立基礎的 CRC-32 查表 (Table 0)
        for (uint i = 0; i < 256; i++)
        {
            uint entry = i;
            for (int j = 0; j < 8; j++)
            {
                if ((entry & 1) == 1)
                    entry = (entry >> 1) ^ polynomial;
                else
                    entry >>= 1;
            }
            tables[0][i] = entry;
        }

        // 建立 Slice-by-8 查表 (Table 1 至 7)
        for (int i = 0; i < 256; i++)
        {
            uint entry = tables[0][i];
            for (int step = 1; step < 8; step++)
            {
                entry = (entry >> 8) ^ tables[0][entry & 0xFF];
                tables[step][i] = entry;
            }
        }

        return tables;
    }

    /// <summary>
    /// Computes the CRC-32 checksum for the specified bytes.
    /// 計算指定位元組的 CRC-32 校驗碼。
    /// </summary>
    /// <param name="bytes">The bytes to process. / 要處理的位元組。</param>
    /// <returns>The final CRC-32 checksum. / 最終 CRC-32 校驗碼。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(ReadOnlySpan<byte> bytes)
    {
#if NET10_0_OR_GREATER
        return System.IO.Hashing.Crc32.HashToUInt32(bytes);
#else
        return Compute(0xFFFFFFFF, bytes) ^ 0xFFFFFFFF;
#endif
    }

    /// <summary>
    /// Accumulates CRC-32 state with the specified bytes.
    /// 使用指定位元組累積 CRC-32 狀態。
    /// </summary>
    /// <param name="currentCrc">The previous CRC state, usually <c>0xFFFFFFFF</c>. / 先前的 CRC 狀態，通常為 <c>0xFFFFFFFF</c>。</param>
    /// <param name="bytes">The bytes to process. / 要處理的位元組。</param>
    /// <returns>The updated intermediate CRC state. / 更新後的 CRC 中間狀態。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint currentCrc, ReadOnlySpan<byte> bytes)
    {
        uint crc = currentCrc;
        int i = 0;

#if NET10_0_OR_GREATER
        if (System.Runtime.Intrinsics.Arm.Crc32.Arm64.IsSupported)
        {
            // ARM64 硬體指令加速
            while (i + 8 <= bytes.Length)
            {
                ulong chunk = ReadUInt64LittleEndian(bytes.Slice(i, 8));
                crc = System.Runtime.Intrinsics.Arm.Crc32.Arm64.ComputeCrc32(crc, chunk);
                i += 8;
            }
            while (i < bytes.Length)
            {
                crc = System.Runtime.Intrinsics.Arm.Crc32.ComputeCrc32(crc, bytes[i]);
                i++;
            }
            return crc;
        }
#endif

        // Slice-by-8 軟體高效查表（將查表提升為區域變數，避免熱迴圈內重複的延遲初始化檢查）
        uint[][] tables = Tables;
        while (i + 8 <= bytes.Length)
        {
            uint one = ReadUInt32LittleEndian(bytes.Slice(i, 4));
            uint two = ReadUInt32LittleEndian(bytes.Slice(i + 4, 4));

            uint c = crc ^ one;
            crc = tables[7][c & 0xFF]
                ^ tables[6][(c >> 8) & 0xFF]
                ^ tables[5][(c >> 16) & 0xFF]
                ^ tables[4][(c >> 24) & 0xFF]
                ^ tables[3][two & 0xFF]
                ^ tables[2][(two >> 8) & 0xFF]
                ^ tables[1][(two >> 16) & 0xFF]
                ^ tables[0][(two >> 24) & 0xFF];

            i += 8;
        }

        // 處理未滿 8 位元組的尾部殘留
        while (i < bytes.Length)
        {
            byte index = (byte)((crc ^ bytes[i]) & 0xFF);
            crc = (crc >> 8) ^ tables[0][index];
            i++;
        }

        return crc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> span)
    {
        return span[0] | ((uint)span[1] << 8) | ((uint)span[2] << 16) | ((uint)span[3] << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> span)
    {
        return span[0]
            | ((ulong)span[1] << 8)
            | ((ulong)span[2] << 16)
            | ((ulong)span[3] << 24)
            | ((ulong)span[4] << 32)
            | ((ulong)span[5] << 40)
            | ((ulong)span[6] << 48)
            | ((ulong)span[7] << 56);
    }
}
