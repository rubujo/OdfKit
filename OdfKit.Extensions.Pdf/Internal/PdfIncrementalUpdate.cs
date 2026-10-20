using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OdfKit.Export.PdfInternal;

/// <summary>
/// 把 PDF 物件模型序列化回 PDF 語法。
/// </summary>
internal static class PdfSerializer
{
    internal static void Write(Stream output, object? value)
    {
        switch (value)
        {
            case null:
                PdfCore.WriteAscii(output, "null");
                break;
            case bool flag:
                PdfCore.WriteAscii(output, flag ? "true" : "false");
                break;
            case long number:
                PdfCore.WriteAscii(output, number.ToString(CultureInfo.InvariantCulture));
                break;
            case int number32:
                PdfCore.WriteAscii(output, number32.ToString(CultureInfo.InvariantCulture));
                break;
            case double real:
                PdfCore.WriteAscii(output, PdfCore.Number(real));
                break;
            case PdfName name:
                PdfCore.WriteAscii(output, PdfCore.Name(name.Value));
                break;
            case PdfRef reference:
                PdfCore.WriteAscii(output, reference.Number.ToString(CultureInfo.InvariantCulture) + " " + reference.Generation.ToString(CultureInfo.InvariantCulture) + " R");
                break;
            case PdfString text:
                PdfCore.WriteAscii(output, "<" + PdfCore.Hex(text.Value) + ">");
                break;
            case string literal:
                PdfCore.WriteAscii(output, PdfCore.TextString(literal));
                break;
            case PdfDict dictionary:
                WriteDictionary(output, dictionary, null);
                break;
            case List<object?> array:
                PdfCore.WriteAscii(output, "[");
                for (int i = 0; i < array.Count; i++)
                {
                    if (i > 0)
                    {
                        PdfCore.WriteAscii(output, " ");
                    }

                    Write(output, array[i]);
                }

                PdfCore.WriteAscii(output, "]");
                break;
            case PdfStream stream:
                WriteDictionary(output, stream.Dictionary, stream.Raw.Length);
                PdfCore.WriteAscii(output, "\nstream\n");
                output.Write(stream.Raw, 0, stream.Raw.Length);
                PdfCore.WriteAscii(output, "\nendstream");
                break;
            default:
                throw new InvalidOperationException("Unsupported PDF value type: " + value.GetType().Name);
        }
    }

    private static void WriteDictionary(Stream output, PdfDict dictionary, int? length)
    {
        PdfCore.WriteAscii(output, "<<");
        foreach (KeyValuePair<string, object?> pair in dictionary)
        {
            if (length is not null && pair.Key == "Length")
            {
                continue;
            }

            PdfCore.WriteAscii(output, " " + PdfCore.Name(pair.Key) + " ");
            Write(output, pair.Value);
        }

        if (length is int streamLength)
        {
            PdfCore.WriteAscii(output, " /Length " + streamLength.ToString(CultureInfo.InvariantCulture));
        }

        PdfCore.WriteAscii(output, " >>");
    }
}

/// <summary>
/// 對既有 PDF 加上增量更新：新物件與改寫的物件附加在檔案尾端，並接上新的交叉參照區段，原有內容與簽章範圍不變。
/// Appends an incremental update to an existing PDF: new and replaced objects, then a new cross-reference section.
/// </summary>
internal sealed class PdfIncrementalUpdate(PdfFile file)
{
    private readonly SortedDictionary<int, object?> _objects = [];
    private int _next = file.NextObjectNumber;

    internal int Add(object? value)
    {
        int number = _next++;
        _objects[number] = value;
        return number;
    }

    internal void Replace(int number, object? value) => _objects[number] = value;

    internal byte[] Build()
    {
        using var output = new MemoryStream(file.Data.Length + 4096);
        output.Write(file.Data, 0, file.Data.Length);
        if (file.Data.Length > 0 && file.Data[file.Data.Length - 1] != '\n')
        {
            output.WriteByte((byte)'\n');
        }

        var offsets = new SortedDictionary<int, long>();
        foreach (KeyValuePair<int, object?> item in _objects)
        {
            offsets[item.Key] = output.Position;
            PdfCore.WriteAscii(output, item.Key.ToString(CultureInfo.InvariantCulture) + " " + GenerationFor(item.Key).ToString(CultureInfo.InvariantCulture) + " obj\n");
            PdfSerializer.Write(output, file.EncryptForWrite(item.Value, item.Key, GenerationFor(item.Key)));
            PdfCore.WriteAscii(output, "\nendobj\n");
        }

        PdfDict trailer = file.Trailer;
        var trailerEntries = new PdfDict();
        foreach (string key in new[] { "Root", "Info", "ID", "Encrypt" })
        {
            if (trailer.TryGetValue(key, out object? value))
            {
                trailerEntries[key] = value;
            }
        }

        if (file.LastXrefIsStream)
        {
            WriteXrefStream(output, offsets, trailerEntries);
        }
        else
        {
            WriteXrefTable(output, offsets, trailerEntries);
        }

        return output.ToArray();
    }

    private int GenerationFor(int number) => file.GetGeneration(number);

    private void WriteXrefTable(MemoryStream output, SortedDictionary<int, long> offsets, PdfDict trailerEntries)
    {
        long xrefOffset = output.Position;
        var sb = new StringBuilder("xref\n");
        int? runStart = null;
        var run = new List<int>();
        void Flush()
        {
            if (run.Count == 0)
            {
                return;
            }

            sb.Append(run[0].ToString(CultureInfo.InvariantCulture)).Append(' ').Append(run.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (int number in run)
            {
                sb.Append(offsets[number].ToString("D10", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(GenerationFor(number).ToString("D5", CultureInfo.InvariantCulture)).Append(" n \n");
            }

            run.Clear();
            runStart = null;
        }

        foreach (int number in offsets.Keys)
        {
            if (runStart is not null && number != run[run.Count - 1] + 1)
            {
                Flush();
            }

            runStart ??= number;
            run.Add(number);
        }

        Flush();
        PdfCore.WriteAscii(output, sb.ToString());
        var trailer = new PdfDict();
        trailer["Size"] = (long)_next;
        foreach (KeyValuePair<string, object?> pair in trailerEntries)
        {
            trailer[pair.Key] = pair.Value;
        }

        trailer["Prev"] = file.LastXrefOffset;
        PdfCore.WriteAscii(output, "trailer\n");
        PdfSerializer.Write(output, trailer);
        PdfCore.WriteAscii(output, "\nstartxref\n" + xrefOffset.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
    }

    private void WriteXrefStream(MemoryStream output, SortedDictionary<int, long> offsets, PdfDict trailerEntries)
    {
        int streamNumber = _next++;
        long streamOffset = output.Position;
        offsets[streamNumber] = streamOffset;

        var index = new List<object?>();
        var data = new MemoryStream();
        int? runStart = null;
        int runLength = 0;
        int previous = -2;
        foreach (KeyValuePair<int, long> entry in offsets)
        {
            if (entry.Key != previous + 1)
            {
                if (runStart is int start)
                {
                    index.Add((long)start);
                    index.Add((long)runLength);
                }

                runStart = entry.Key;
                runLength = 0;
            }

            data.WriteByte(1);
            data.WriteByte((byte)(entry.Value >> 24));
            data.WriteByte((byte)(entry.Value >> 16));
            data.WriteByte((byte)(entry.Value >> 8));
            data.WriteByte((byte)entry.Value);
            int generation = streamNumber == entry.Key ? 0 : GenerationFor(entry.Key);
            data.WriteByte((byte)(generation >> 8));
            data.WriteByte((byte)generation);
            runLength++;
            previous = entry.Key;
        }

        if (runStart is int lastStart)
        {
            index.Add((long)lastStart);
            index.Add((long)runLength);
        }

        var dictionary = new PdfDict
        {
            ["Type"] = new PdfName("XRef"),
            ["Size"] = (long)_next,
            ["W"] = new List<object?> { 1L, 4L, 2L },
            ["Index"] = index,
        };
        foreach (KeyValuePair<string, object?> pair in trailerEntries)
        {
            dictionary[pair.Key] = pair.Value;
        }

        dictionary["Prev"] = file.LastXrefOffset;
        PdfCore.WriteAscii(output, streamNumber.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
        PdfSerializer.Write(output, new PdfStream(dictionary, data.ToArray()));
        PdfCore.WriteAscii(output, "\nendobj\nstartxref\n" + streamOffset.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
    }
}
