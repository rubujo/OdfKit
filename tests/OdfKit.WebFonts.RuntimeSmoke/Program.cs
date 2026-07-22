using System.Text;
using OdfKit.WebFonts.OpenType;

if (!WebFontRuntimeCapabilities.IsWoff2Available || !RuntimeBrotliCodec.IsAvailable)
{
    throw new InvalidOperationException("The net8.0 runtime did not expose Brotli through the netstandard2.0 asset.");
}

byte[] source = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("ODFKIT-WOFF2-RUNTIME-", 256)));
byte[] compressed = RuntimeBrotliCodec.Compress(source);
var decoded = new byte[source.Length];
if (!RuntimeBrotliCodec.TryDecompress(compressed, decoded, out int written)
    || written != source.Length
    || !source.SequenceEqual(decoded))
{
    throw new InvalidDataException("The runtime Brotli round trip failed.");
}

Console.WriteLine("netstandard2.0 Brotli runtime round trip passed.");
