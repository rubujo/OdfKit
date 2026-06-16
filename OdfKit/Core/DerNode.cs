using System;
using System.Collections.Generic;

namespace OdfKit.Core;

internal class DerNode(byte tag, byte[] value)
{
    public byte Tag { get; set; } = tag;
    public byte[] Value { get; set; } = value;
    public List<DerNode> Children { get; } = [];
    public int StartOffset { get; set; }
    public int Length { get; set; }
    public byte[] RawBytes { get; set; } = [];
}
