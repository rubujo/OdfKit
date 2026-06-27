using System;
using System.Runtime.CompilerServices;

namespace OdfKit.Core;

/// <summary>
/// 提供高效且零分配的 CRC-32 (ISO-HDLC) 校驗碼計算。
/// 支援在 net10.0 與 ARM64 架構下使用 CPU 硬體指令加速；
/// 於其餘平台與 netstandard2.0 下自動 Fallback 至 Slice-by-8 查表法。
/// </summary>
public static class OdfCrc32
{
    private static readonly uint[][] Tables;

    static OdfCrc32()
    {
        Tables = new uint[8][];
        for (int i = 0; i < 8; i++)
        {
            Tables[i] = new uint[256];
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
            Tables[0][i] = entry;
        }

        // 建立 Slice-by-8 查表 (Table 1 至 7)
        for (int i = 0; i < 256; i++)
        {
            uint entry = Tables[0][i];
            for (int step = 1; step < 8; step++)
            {
                entry = (entry >> 8) ^ Tables[0][entry & 0xFF];
                Tables[step][i] = entry;
            }
        }
    }

    /// <summary>
    /// 計算指定位元組 Span 的 CRC-32 值。
    /// </summary>
    /// <param name="bytes">要計算的唯讀位元組 Span</param>
    /// <returns>CRC-32 校驗碼</returns>
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
    /// 以現有的 CRC 種子（Seed）累積計算指定位元組 Span 的 CRC-32 值。
    /// </summary>
    /// <param name="currentCrc">之前的 CRC 狀態值（累計中間狀態，或 0xFFFFFFFF）</param>
    /// <param name="bytes">要計算的唯讀位元組 Span</param>
    /// <returns>新的 CRC 中間狀態值</returns>
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

        // Slice-by-8 軟體高效查表
        while (i + 8 <= bytes.Length)
        {
            uint one = ReadUInt32LittleEndian(bytes.Slice(i, 4));
            uint two = ReadUInt32LittleEndian(bytes.Slice(i + 4, 4));

            uint c = crc ^ one;
            crc = Tables[7][c & 0xFF]
                ^ Tables[6][(c >> 8) & 0xFF]
                ^ Tables[5][(c >> 16) & 0xFF]
                ^ Tables[4][(c >> 24) & 0xFF]
                ^ Tables[3][two & 0xFF]
                ^ Tables[2][(two >> 8) & 0xFF]
                ^ Tables[1][(two >> 16) & 0xFF]
                ^ Tables[0][(two >> 24) & 0xFF];

            i += 8;
        }

        // 處理未滿 8 位元組的尾部殘留
        while (i < bytes.Length)
        {
            byte index = (byte)((crc ^ bytes[i]) & 0xFF);
            crc = (crc >> 8) ^ Tables[0][index];
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
