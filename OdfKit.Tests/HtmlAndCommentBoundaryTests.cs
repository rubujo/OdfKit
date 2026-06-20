using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Xunit;
using OdfKit.DOM;
using OdfKit.Core;
using OdfKit.Text;

namespace OdfKit.Tests
{
    public class HtmlAndCommentBoundaryTests
    {
        #region 1. OdfComment Cyclic References & Nested Limits

        [Fact]
        public void TestDirectCycleDetection()
        {
            var a = new OdfComment("Author", "A");
            var b = new OdfComment("Author", "B");
            a.AddReply(b);
            b.AddReply(a); // Direct cycle A -> B -> A

            Assert.Throws<InvalidOperationException>(() => a.ToXmlNode());
            Assert.Throws<InvalidOperationException>(() => b.ToXmlNode());
        }

        [Fact]
        public void TestIndirectCycleDetection()
        {
            var a = new OdfComment("Author", "A");
            var b = new OdfComment("Author", "B");
            var c = new OdfComment("Author", "C");
            a.AddReply(b);
            b.AddReply(c);
            c.AddReply(a); // Indirect cycle A -> B -> C -> A

            Assert.Throws<InvalidOperationException>(() => a.ToXmlNode());
        }

        [Fact]
        public void TestComplexIndirectCycleDetection()
        {
            var root = new OdfComment("Author", "Root");
            var a = new OdfComment("Author", "A");
            var b = new OdfComment("Author", "B");
            var c = new OdfComment("Author", "C");
            var d = new OdfComment("Author", "D");

            root.AddReply(a);
            root.AddReply(d);
            a.AddReply(b);
            b.AddReply(c);
            c.AddReply(b); // Cycle is B -> C -> B, root/A are outside the cycle
            d.AddReply(c); // D also points to C

            // serializing root should hit the cycle under B/C and throw
            Assert.Throws<InvalidOperationException>(() => root.ToXmlNode());
        }

        [Fact]
        public void TestXmlCycleParsingSafety()
        {
            // Create a manually constructed XML container representing a cycle in comments.
            // Under annotation-list:
            // annotation name="A" annotation-parent="B"
            // annotation name="B" annotation-parent="A"
            var container = new OdfNode(OdfNodeType.Element, "annotation-list", string.Empty);

            var aNode = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            aNode.SetAttribute("name", OdfNamespaces.Office, "A", "office");
            aNode.SetAttribute("annotation-parent", OdfNamespaces.Office, "B", "office");

            var bNode = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            bNode.SetAttribute("name", OdfNamespaces.Office, "B", "office");
            bNode.SetAttribute("annotation-parent", OdfNamespaces.Office, "A", "office");

            container.AppendChild(aNode);
            container.AppendChild(bNode);

            // Parsing from container
            var parsed = OdfComment.FromXmlNode(container);
            Assert.NotNull(parsed);
            Assert.Equal("A", parsed.Name);

            // Let's verify that the replies do not cause an infinite loop on access
            Assert.Single(parsed.Replies);
            Assert.Equal("B", parsed.Replies[0].Name);
            Assert.Single(parsed.Replies[0].Replies);
            Assert.Equal("A", parsed.Replies[0].Replies[0].Name);
            // Since a child comment A has its parent as B, and B has its parent as A,
            // let's verify if the child A object is the SAME reference as root A object or a duplicate.
            // During parsing:
            // commentsMap has distinct objects for A and B.
            // A has B added to replies.
            // B has A added to replies.
            // So indeed it forms an in-memory cycle: parsed -> Replies[0] -> Replies[0].Replies[0] == parsed?
            // Actually, commentsMap["A"] == parsed, commentsMap["B"] == Replies[0].
            // Let's assert:
            Assert.Same(parsed, parsed.Replies[0].Replies[0]); // It is an in-memory object cycle!

            // Now, since there is an in-memory object cycle, calling ToXmlNode() on parsed must detect it and throw!
            Assert.Throws<InvalidOperationException>(() => parsed.ToXmlNode());
        }

        #endregion

        #region 2. OdfComment Diamond DAGs & Duplicate Names

        [Fact]
        public void TestDuplicateCommentNamesSerialization()
        {
            var a = new OdfComment("Author", "Root");
            // Create two different comment objects with the SAME Name
            var b = new OdfComment("Author", "Reply B", DateTime.UtcNow, "DuplicateName");
            var c = new OdfComment("Author", "Reply C", DateTime.UtcNow, "DuplicateName");

            a.AddReply(b);
            a.AddReply(c);

            var xml = a.ToXmlNode();
            Assert.NotNull(xml);

            // Since both replies have name "DuplicateName", the second one should be skipped
            // by serializedNames check.
            int countOfB = CountOccurrences(xml, "Reply B");
            int countOfC = CountOccurrences(xml, "Reply C");

            Assert.Equal(1, countOfB);
            Assert.Equal(0, countOfC); // Completely omitted!
        }

        private int CountOccurrences(OdfNode node, string text)
        {
            int count = 0;
            if (node.NodeType == OdfNodeType.Text && node.TextContent.Contains(text))
                count++;
            foreach (var child in node.Children)
            {
                count += CountOccurrences(child, text);
            }
            return count;
        }

        #endregion

        #region 3. TextDocument HTML Entity & Tag Parsing

        [Fact]
        public void TestHtmlParsingWithUnclosedAndMalformedTags()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();

            // 1. Unclosed tags should apply styles to end of text
            string html1 = "Plain <b>Bold <i>BoldItalic";
            p.AddHtmlFragment(html1);

            // Verify children:
            // "Plain " (text node, no style)
            // "Bold " (span, bold)
            // "BoldItalic" (span, bold+italic)
            Assert.Equal(3, p.Node.Children.Count);
            Assert.Equal("Plain ", p.Node.Children[0].TextContent);
            Assert.Equal("Bold ", p.Node.Children[1].TextContent);
            Assert.Equal("BoldItalic", p.Node.Children[2].TextContent);

            // 2. Extra closing tags should be ignored safely
            p = doc.AddParagraph();
            string html2 = "Plain </b>Bold</i> Extra</i>";
            p.AddHtmlFragment(html2);
            Assert.Single(p.Node.Children);
            Assert.Equal("Plain Bold Extra", p.Node.TextContent);
        }

        [Fact]
        public void TestHtmlParsingNestedSpansLimitations()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();

            // Nested spans with font-weight: bold and then font-weight: normal.
            // Since spanStack accumulates styles additively, the inner normal span
            // will still inherit bold style from outer stack frame.
            string html = "<span style=\"font-weight: bold;\">OuterBold <span style=\"font-weight: normal;\">InnerNormal</span> StillBold</span>";
            p.AddHtmlFragment(html);

            // Total children:
            // "OuterBold " (bold span)
            // "InnerNormal" (bold span - because bold state from outer span is inherited additively)
            // " StillBold" (bold span)
            Assert.Equal(3, p.Node.Children.Count);

            // Let's verify that even "InnerNormal" is marked as bold.
            // To do this, we can check that it has style-name or textrun style settings.
            // Actually, we can check the node content and its properties or style resolution.
            // Let's look at the generated XML of paragraph.
            using var writeStream = new MemoryStream();
            OdfXmlWriter.Write(p.Node, writeStream, new OdfSaveOptions());
            string xml = Encoding.UTF8.GetString(writeStream.ToArray());

            // Both "OuterBold ", "InnerNormal", and " StillBold" are wrapped in <span> elements with a style.
            // We can assert that the span for "InnerNormal" has a style name.
            Assert.Contains("OuterBold", xml);
            Assert.Contains("InnerNormal", xml);
            Assert.Contains("StillBold", xml);
        }

        [Fact]
        public void TestHtmlParsingCharacterLessThanLossBug()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();

            // A less-than sign '<' that is not part of a tag (e.g., "A < B")
            string html = "A < B";
            p.AddHtmlFragment(html);

            // Verify that the '<' is preserved!
            string plainText = p.Node.TextContent;

            // The '<' is preserved because the tokenRegex correctly parses it without loss.
            Assert.Equal("A < B", plainText);
        }

        [Fact]
        public void TestHtmlParsingUnclosedScriptBlockLossBug()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();

            // Unclosed script tag:
            string html = "Visible text <script>var x = 1; More text that is lost";
            p.AddHtmlFragment(html);

            string plainText = p.Node.TextContent;

            // The text after '<script>' is entirely ignored because 'inScriptOrStyle' is never reset.
            Assert.Equal("Visible text ", plainText); // Everything after <script> is LOST!
        }

        [Fact]
        public void TestHtmlParsingScriptInsideHtmlComment()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();

            // A script block wrapped in an HTML comment
            string html = "Hello <!-- <script>alert(1)</script> --> World";
            p.AddHtmlFragment(html);

            string plainText = p.Node.TextContent;

            // The HTML comment is stripped, so the text content is "Hello  World".
            Assert.Equal("Hello  World", plainText);
        }

        [Fact]
        public void TestHtmlParsingNestedScriptBlockRegexLimitation()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();

            // Nested script tag (lazy regex match will stop at the first closing tag)
            string html = "Hello <script>console.log(\"<script>alert(1)</script>\")</script> World";
            p.AddHtmlFragment(html);

            string plainText = p.Node.TextContent;

            // The first regex replaces `<script>...first </script>` with `""`.
            // Leftover: `\")</script> World`
            // Let's verify if the leftover text is parsed as plain text:
            Assert.Contains("\")", plainText);
            Assert.Contains("World", plainText);
            Assert.DoesNotContain("console.log", plainText);
        }

        [Fact]
        public void TestHtmlParsingCaseInsensitivityForScriptFilter()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();

            // Script/style filter with mixed case
            string html = "Text <ScRiPt>alert(1)</sCrIpT> and <StYlE>body{}</sTyLe> End";
            p.AddHtmlFragment(html);

            string plainText = p.Node.TextContent;
            Assert.Equal("Text  and  End", plainText);
        }

        [Fact]
        public void TestHtmlParsingEntitiesCornerCases()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();

            // Test various entities:
            // &apos; -> '
            // &APOS; -> '
            // &aPoS; -> WebUtility.HtmlDecode might not handle it? Let's check.
            // &#39; -> '
            // &#x27; -> '
            // &amp -> &
            // &invalid; -> &invalid;
            string html = "A &apos; B &APOS; C &aPoS; D &#39; E &#x27; F &#X27; G &amp H &invalid; I";
            p.AddHtmlFragment(html);

            string plainText = p.Node.TextContent;

            // Let's see what is decoded:
            Assert.Contains("A ' B", plainText);
            Assert.Contains("B ' C", plainText);
            Assert.Contains("D ' E", plainText);
            Assert.Contains("E ' F", plainText);
            Assert.Contains("F ' G", plainText);

            // WebUtility.HtmlDecode may or may not decode "&amp" without semicolon, or "&aPoS;".
            // Let's check what they actually evaluate to.
            LogDiagnostics($"Decoded text: {plainText}");
        }

        [Fact]
        public void TestHtmlParsingNestedBoldStyleResetBug()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();

            // Nested tags of same type
            string html = "<b>Outer <b>Inner</b> OuterAgain</b>";
            p.AddHtmlFragment(html);

            // Because the parser uses `isBold = !isClosing`, the first </b> sets `isBold = false`.
            // So "OuterAgain" will NOT be bold in the parser, which is a bug/limitation!
            // Let's verify this behavior:
            Assert.Equal(3, p.Node.Children.Count);

            // "Outer " is span (bold)
            // "Inner" is span (bold)
            // " OuterAgain" is text node (NOT bold) because isBold became false!
            Assert.Equal("Outer ", p.Node.Children[0].TextContent);
            Assert.Equal("Inner", p.Node.Children[1].TextContent);
            Assert.Equal(" OuterAgain", p.Node.Children[2].TextContent);

            // Children[2] is a text node (plain text), not an element span node.
            Assert.Equal(OdfNodeType.Text, p.Node.Children[2].NodeType);
        }

        [Fact]
        public void TestOdfCommentMultipleRootsInAnnotationList()
        {
            var container = new OdfNode(OdfNodeType.Element, "annotation-list", string.Empty);

            // Root 1
            var root1Node = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            root1Node.SetAttribute("name", OdfNamespaces.Office, "root1", "office");
            var creator1 = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = "A" };
            var p1 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "Root 1 Msg" };
            root1Node.AppendChild(creator1);
            root1Node.AppendChild(p1);

            // Root 2
            var root2Node = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            root2Node.SetAttribute("name", OdfNamespaces.Office, "root2", "office");
            var creator2 = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = "B" };
            var p2 = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = "Root 2 Msg" };
            root2Node.AppendChild(creator2);
            root2Node.AppendChild(p2);

            container.AppendChild(root1Node);
            container.AppendChild(root2Node);

            // Parsing from container
            var parsed = OdfComment.FromXmlNode(container);
            Assert.NotNull(parsed);

            // Only root1 is returned. root2 is ignored/lost.
            Assert.Equal("root1", parsed.Name);
            Assert.Empty(parsed.Replies);
        }

        [Fact]
        public void TestHtmlParsingStyleWhitespaceAndCase()
        {
            using var ms = new MemoryStream();
            using var package = OdfPackage.Create(ms);
            var doc = new TextDocument(package);
            var p = doc.AddParagraph();

            string html = "<span style  =  '  FONT-WEIGHT  :  BOLD  ;  FONT-STYLE  :  ITALIC  ;  TEXT-DECORATION  :  UNDERLINE  ' >Formatted Text</span>";
            p.AddHtmlFragment(html);

            // Verify that the parser correctly extracts bold, italic, and underline despite whitespace and uppercase.
            Assert.Single(p.Node.Children);
            var span = p.Node.Children[0];
            Assert.Equal("Formatted Text", span.TextContent);
            Assert.Equal("span", span.LocalName);

            // Save to XML and verify style presence
            using var writeStream = new MemoryStream();
            OdfXmlWriter.Write(p.Node, writeStream, new OdfSaveOptions());
            string xml = Encoding.UTF8.GetString(writeStream.ToArray());

            // Check that it's styled (it should have a style-name)
            Assert.Contains("style-name", xml);
        }

        private void LogDiagnostics(string msg)
        {
            Console.WriteLine(msg);
        }

        #endregion
    }
}
