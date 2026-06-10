using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using OdfKit.Core;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet
{
    public class OdsStreamWriter : IDisposable
    {
        private readonly Stream _outputStream;
        private readonly ZipArchive _zip;
        private readonly Stream _contentEntryStream;
        private readonly XmlWriter _writer;
        private bool _isRowStarted;
        private bool _isSheetStarted;
        private bool _disposed;
        private readonly System.Collections.Generic.List<(string styleName, OdfLength width)> _columnStyles = new();
        private int _autoColumnStyleIndex = 0;

        public OdsStreamWriter(Stream outputStream)
        {
            _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
            
            // Wrap in NonSeekableStreamWrapper to force streaming, non-buffering mode in ZipArchive
            _zip = new ZipArchive(new NonSeekableStreamWrapper(_outputStream), ZipArchiveMode.Create, leaveOpen: true);
            
            // 1. Write uncompressed mimetype first
            var mimeEntry = _zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var s = mimeEntry.Open())
            {
                byte[] bytes = Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.spreadsheet");
                s.Write(bytes, 0, bytes.Length);
            }

            // 2. Write metadata, styles, and manifest entries
            WriteDefaultMetaFiles();

            // 3. Open content.xml for streaming
            var contentEntry = _zip.CreateEntry("content.xml", CompressionLevel.Fastest);
            _contentEntryStream = contentEntry.Open();
            
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = false // Minimize size
            };
            _writer = XmlWriter.Create(_contentEntryStream, settings);

            // Write ODF XML header and root document-content tags
            _writer.WriteStartDocument();
            _writer.WriteStartElement("office", "document-content", OdfNamespaces.Office);
            _writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
            _writer.WriteAttributeString("xmlns", "table", null, OdfNamespaces.Table);
            _writer.WriteAttributeString("xmlns", "text", null, OdfNamespaces.Text);
            _writer.WriteAttributeString("xmlns", "style", null, OdfNamespaces.Style);
            
            // Write body and spreadsheet wrapper
            _writer.WriteStartElement("office", "body", OdfNamespaces.Office);
            _writer.WriteStartElement("office", "spreadsheet", OdfNamespaces.Office);
        }

        public void WriteStartSheet(string sheetName)
        {
            if (_disposed) return;
            try
            {
                if (_isSheetStarted) WriteEndSheet();
                _writer.WriteStartElement("table", "table", OdfNamespaces.Table);
                _writer.WriteAttributeString("table", "name", OdfNamespaces.Table, sheetName);
                _isSheetStarted = true;
            }
            catch (Exception) { }
        }

        public void WriteColumn(OdfLength width, string? styleName = null)
        {
            if (_disposed) return;
            try
            {
                string name = string.IsNullOrEmpty(styleName)
                    ? $"co_auto_{++_autoColumnStyleIndex}"
                    : styleName!;

                _writer.WriteStartElement("table", "table-column", OdfNamespaces.Table);
                _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, name);
                _writer.WriteEndElement();

                _columnStyles.Add((name, width));
            }
            catch (Exception) { }
        }

        public void WriteStartRow(double? height = null, string? styleName = null, bool useOptimalHeight = false)
        {
            if (_disposed) return;
            try
            {
                if (_isRowStarted) WriteEndRow();
                _isRowStarted = true;
                _writer.WriteStartElement("table", "table-row", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(styleName))
                {
                    _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
                }
            }
            catch (Exception) { }
        }

        public void WriteCell(string value, string? styleName = null)
        {
            if (_disposed) return;
            try
            {
                _writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
                _writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "string");
                if (!string.IsNullOrEmpty(styleName))
                {
                    _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
                }
                _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
                _writer.WriteString(value);
                _writer.WriteEndElement(); // text:p
                _writer.WriteEndElement(); // table:cell
            }
            catch (Exception) { }
        }

        public void WriteCell(double value, string? styleName = null)
        {
            if (_disposed) return;
            try
            {
                _writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
                _writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "float");
                _writer.WriteAttributeString("office", "value", OdfNamespaces.Office, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(styleName))
                {
                    _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
                }
                _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
                _writer.WriteString(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                _writer.WriteEndElement(); // text:p
                _writer.WriteEndElement(); // table:cell
            }
            catch (Exception) { }
        }

        public void WriteCell(DateTime value, string? styleName = null, bool timezoneNaive = false)
        {
            if (_disposed) return;
            try
            {
                _writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
                _writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "date");
                
                string isoDate;
                if (value == DateTime.MinValue || value == DateTime.MaxValue)
                {
                    isoDate = timezoneNaive
                        ? value.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
                        : value.ToString("s", System.Globalization.CultureInfo.InvariantCulture) + "Z";
                }
                else
                {
                    isoDate = timezoneNaive 
                        ? value.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
                        : value.ToUniversalTime().ToString("s", System.Globalization.CultureInfo.InvariantCulture) + "Z";
                }
                    
                _writer.WriteAttributeString("office", "date-value", OdfNamespaces.Office, isoDate);
                if (!string.IsNullOrEmpty(styleName))
                {
                    _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
                }
                _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
                _writer.WriteString(isoDate);
                _writer.WriteEndElement(); // text:p
                _writer.WriteEndElement(); // table:cell
            }
            catch (Exception) { }
        }

        public void WriteCell(bool value, string? styleName = null)
        {
            if (_disposed) return;
            try
            {
                _writer.WriteStartElement("table", "table-cell", OdfNamespaces.Table);
                _writer.WriteAttributeString("office", "value-type", OdfNamespaces.Office, "boolean");
                _writer.WriteAttributeString("office", "boolean-value", OdfNamespaces.Office, value ? "true" : "false");
                if (!string.IsNullOrEmpty(styleName))
                {
                    _writer.WriteAttributeString("table", "style-name", OdfNamespaces.Table, styleName);
                }
                _writer.WriteStartElement("text", "p", OdfNamespaces.Text);
                _writer.WriteString(value ? "TRUE" : "FALSE");
                _writer.WriteEndElement(); // text:p
                _writer.WriteEndElement(); // table:cell
            }
            catch (Exception) { }
        }

        public void WriteEndRow()
        {
            if (_disposed) return;
            if (_isRowStarted)
            {
                try
                {
                    _writer.WriteEndElement(); // table-row
                }
                catch (Exception) { }
                _isRowStarted = false;
            }
        }

        public void WriteEndSheet()
        {
            if (_disposed) return;
            if (_isRowStarted) WriteEndRow();
            if (_isSheetStarted)
            {
                try
                {
                    _writer.WriteEndElement(); // table:table
                }
                catch (Exception) { }
                _isSheetStarted = false;
            }
        }

        private void WriteDefaultMetaFiles()
        {
            WriteManifest();
            // Do not write styles.xml here
            WriteMeta();
        }

        private void WriteManifest()
        {
            var entry = _zip.CreateEntry("META-INF/manifest.xml", CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = XmlWriter.Create(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("manifest", "manifest", OdfNamespaces.Manifest);
                writer.WriteAttributeString("manifest", "version", OdfNamespaces.Manifest, "1.3");
                
                writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
                writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "/");
                writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, "application/vnd.oasis.opendocument.spreadsheet");
                writer.WriteEndElement();

                writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
                writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "content.xml");
                writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, "text/xml");
                writer.WriteEndElement();

                writer.WriteStartElement("file-entry", OdfNamespaces.Manifest);
                writer.WriteAttributeString("manifest", "full-path", OdfNamespaces.Manifest, "styles.xml");
                writer.WriteAttributeString("manifest", "media-type", OdfNamespaces.Manifest, "text/xml");
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private void WriteStyles()
        {
            var entry = _zip.CreateEntry("styles.xml", CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = XmlWriter.Create(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("office", "document-styles", OdfNamespaces.Office);
                writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
                writer.WriteAttributeString("xmlns", "style", null, OdfNamespaces.Style);
                writer.WriteAttributeString("xmlns", "text", null, OdfNamespaces.Text);
                writer.WriteAttributeString("xmlns", "table", null, OdfNamespaces.Table);
                writer.WriteAttributeString("xmlns", "fo", null, OdfNamespaces.Fo);
                
                writer.WriteStartElement("office", "styles", OdfNamespaces.Office);
                writer.WriteEndElement();

                writer.WriteStartElement("office", "automatic-styles", OdfNamespaces.Office);
                foreach (var style in _columnStyles)
                {
                    writer.WriteStartElement("style", "style", OdfNamespaces.Style);
                    writer.WriteAttributeString("style", "name", OdfNamespaces.Style, style.styleName);
                    writer.WriteAttributeString("style", "family", OdfNamespaces.Style, "table-column");
                    writer.WriteStartElement("style", "table-column-properties", OdfNamespaces.Style);
                    writer.WriteAttributeString("style", "column-width", OdfNamespaces.Style, style.width.ToString());
                    writer.WriteEndElement(); // table-column-properties
                    writer.WriteEndElement(); // style
                }
                writer.WriteEndElement();

                writer.WriteStartElement("office", "master-styles", OdfNamespaces.Office);
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private void WriteMeta()
        {
            var entry = _zip.CreateEntry("meta.xml", CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = XmlWriter.Create(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("office", "document-meta", OdfNamespaces.Office);
                writer.WriteAttributeString("xmlns", "office", null, OdfNamespaces.Office);
                writer.WriteAttributeString("xmlns", "dc", null, OdfNamespaces.Dc);
                writer.WriteAttributeString("xmlns", "meta", null, OdfNamespaces.Meta);
                
                writer.WriteStartElement("office", "meta", OdfNamespaces.Office);
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_isSheetStarted) WriteEndSheet();
            
            // Close spreadsheet, body, document-content tags
            _writer.WriteEndElement(); // office:spreadsheet
            _writer.WriteEndElement(); // office:body
            _writer.WriteEndElement(); // office:document-content
            _writer.WriteEndDocument();
            
            _writer.Dispose();
            _contentEntryStream.Dispose();

            WriteStyles();

            _zip.Dispose();
        }
    }

    internal class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _baseStream;

        public NonSeekableStreamWrapper(Stream baseStream)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _baseStream.Write(buffer, offset, count);

        public override System.Threading.Tasks.Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            return _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            return _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override System.Threading.Tasks.Task FlushAsync(System.Threading.CancellationToken cancellationToken)
        {
            return _baseStream.FlushAsync(cancellationToken);
        }
    }
}
