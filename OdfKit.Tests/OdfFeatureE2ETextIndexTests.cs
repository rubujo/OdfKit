using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests
{
    public partial class OdfFeatureE2ETests
    {
        #region Feature 1: ODT TOC / Index
        [Fact]
        public void F1_Toc_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddHeading("Introduction", 1);
            doc.AddTableOfContents();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var tocNode = reloaded.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "table-of-content" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(tocNode);
            Assert.Equal("Table of Contents", tocNode.GetAttribute("name", OdfNamespaces.Text));
        }

        [Fact]
        public void F1_AlphabeticalIndex_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddAlphabeticalIndex();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var idxNode = reloaded.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "alphabetical-index" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(idxNode);
            Assert.Equal("Alphabetical Index", idxNode.GetAttribute("name", OdfNamespaces.Text));
        }

        [Fact]
        public void F1_Bibliography_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddBibliography();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var bibNode = reloaded.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "bibliography" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(bibNode);
            Assert.Equal("Bibliography", bibNode.GetAttribute("name", OdfNamespaces.Text));
        }

        [Fact]
        public void F1_TableIndex_HappyPath()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddTableIndex();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var idxNode = reloaded.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "table-index" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(idxNode);
            Assert.Equal("Index of Tables", idxNode.GetAttribute("name", OdfNamespaces.Text));
        }

        [Fact]
        public void F1_TOC_IndexBodyStructure()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new TextDocument(package);
            doc.AddTableOfContents();
            doc.Save();

            var reloaded = RoundTrip(doc, p => new TextDocument(p));
            var tocNode = reloaded.BodyTextRoot.Children.FirstOrDefault(c => c.LocalName == "table-of-content" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(tocNode);
            var bodyNode = tocNode.Children.FirstOrDefault(c => c.LocalName == "index-body" && c.NamespaceUri == OdfNamespaces.Text);
            Assert.NotNull(bodyNode);
        }
        #endregion
    }
}
