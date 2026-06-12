using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;
using Xunit;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Presentation;
using OdfKit.Styles;
using OdfKit.Chart;
using OdfKit.Formula;

namespace OdfKit.Tests
{
    public class PresentationBoundaryTests
    {
        #region 1. Slide Layouts & Placeholders Boundary Tests

        [Fact]
        public void TestPlaceholderTypesEnumParsing()
        {
            // Verify that all possible values of OdfPlaceholderType roundtrip correctly
            foreach (OdfPlaceholderType type in Enum.GetValues(typeof(OdfPlaceholderType)))
            {
                string kebab = OdfPlaceholderTemplate.TypeToKebab(type);
                Assert.NotNull(kebab);
                Assert.NotEmpty(kebab);

                OdfPlaceholderType parsed = OdfPlaceholderTemplate.KebabToType(kebab);
                Assert.Equal(type, parsed);
            }

            // Verify fallback for unrecognized kebab strings
            OdfPlaceholderType fallback = OdfPlaceholderTemplate.KebabToType("non-existent-placeholder-class");
            Assert.Equal(OdfPlaceholderType.Text, fallback);
        }

        [Fact]
        public void TestOdfPlaceholderTemplateNullAndBoundaryCoordinates()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                var layout = doc.CreatePresentationPageLayout("BoundaryLayout");
                
                // Add a placeholder with normal coordinates
                var ph = layout.AddPlaceholder(OdfPlaceholderType.Subtitle, OdfLength.Parse("2cm"), OdfLength.Parse("3cm"), OdfLength.Parse("12cm"), OdfLength.Parse("4cm"));
                Assert.Equal("2cm", ph.X?.ToString());
                Assert.Equal("3cm", ph.Y?.ToString());

                // Set X and Y to null - check behaviour
                ph.X = null;
                ph.Y = null;
                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new PresentationDocument(package);
                var layout = doc.GetPresentationPageLayout("BoundaryLayout");
                Assert.NotNull(layout);
                Assert.Single(layout.Placeholders);

                var ph = layout.Placeholders[0];
                
                // CRITICAL OBSERVATION: If setting X = null sets it to string.Empty instead of removing it,
                // then parsing it back returns OdfUnit.Unspecified (value=0) instead of null.
                // We verify if it returns null or OdfUnit.Unspecified.
                if (ph.X != null)
                {
                    Assert.Equal(OdfUnit.Unspecified, ph.X.Value.Unit);
                    Assert.Equal(0, ph.X.Value.Value);
                }
                else
                {
                    Assert.Null(ph.X);
                }
            }
        }

        [Fact]
        public void TestDuplicateAndNonexistentPlaceholders()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                var layout = doc.CreatePresentationPageLayout("DuplicateLayout");

                // Add multiple placeholders of the same type
                layout.AddPlaceholder(OdfPlaceholderType.Header, OdfLength.Parse("1cm"), OdfLength.Parse("1cm"), OdfLength.Parse("5cm"), OdfLength.Parse("1cm"));
                layout.AddPlaceholder(OdfPlaceholderType.Header, OdfLength.Parse("10cm"), OdfLength.Parse("1cm"), OdfLength.Parse("5cm"), OdfLength.Parse("1cm"));

                Assert.Equal(2, layout.Placeholders.Count);

                // Remove non-existent placeholder type (should be a no-op)
                layout.RemovePlaceholder(OdfPlaceholderType.Footer);
                Assert.Equal(2, layout.Placeholders.Count);

                // Remove the duplicate placeholder type (should remove both)
                layout.RemovePlaceholder(OdfPlaceholderType.Header);
                Assert.Empty(layout.Placeholders);

                doc.Save();
            }
        }

        #endregion

        #region 2. Slide Notes and Handouts Boundary Tests

        [Fact]
        public void TestSlideNotesBoundaryCases()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                var slide = doc.AddSlide("Slide 1");
                var notes = slide.SpeakerNotesPage;

                // Test Null and Empty string note values
                notes.SpeakerNotesText = "";
                Assert.Equal(string.Empty, notes.SpeakerNotesText);

                notes.SpeakerNotesText = null!;
                Assert.Equal(string.Empty, notes.SpeakerNotesText);

                // Test very large speaker notes (stress)
                string hugeNotes = new string('A', 100000);
                notes.SpeakerNotesText = hugeNotes;
                Assert.Equal(hugeNotes, notes.SpeakerNotesText);

                // Add thumbnail with negative and zero coordinates
                notes.AddSlideThumbnail(OdfLength.Parse("-5cm"), OdfLength.Parse("0cm"), OdfLength.Parse("10cm"), OdfLength.Parse("8cm"));
                
                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new PresentationDocument(package);
                var slide = doc.Slides[0];
                var notes = slide.SpeakerNotesPage;
                Assert.Equal(100000, notes.SpeakerNotesText.Length);
            }
        }

        [Fact]
        public void TestHandoutPageBoundaryCases()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                var handout = doc.HandoutPage;

                // Set Name and MasterPageName to null (removing attributes)
                handout.Name = null;
                handout.MasterPageName = null;

                // Add text box with large content
                string longText = new string('H', 10000);
                handout.AddTextBox(OdfLength.Parse("1cm"), OdfLength.Parse("1cm"), OdfLength.Parse("20cm"), OdfLength.Parse("5cm"), longText);

                // Add multiple thumbnail placeholders
                handout.AddSlideThumbnailPlaceholder(OdfLength.Parse("1cm"), OdfLength.Parse("7cm"), OdfLength.Parse("8cm"), OdfLength.Parse("6cm"));
                handout.AddSlideThumbnailPlaceholder(OdfLength.Parse("10cm"), OdfLength.Parse("7cm"), OdfLength.Parse("8cm"), OdfLength.Parse("6cm"));

                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new PresentationDocument(package);
                var handout = doc.HandoutPage;
                Assert.Null(handout.Name);
                Assert.Null(handout.MasterPageName);
                Assert.Single(handout.Shapes); // Text box frame is listed as shape, but thumbnail isn't under Shapes list
            }
        }

        #endregion

        #region 3. Transitions & SMIL animations Timing Tests

        [Fact]
        public void TestSmilAnimationsBoundaryCases()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                var slide = doc.AddSlide("Slide 1");
                var rootSeq = slide.AnimationRoot;

                // Add sequence and parallel with null and empty begin attributes
                var seq1 = rootSeq.AddSequence(null);
                var seq2 = rootSeq.AddSequence(string.Empty);
                var par1 = seq1.AddParallel(null);

                // Add effects with zero and negative durations/delays
                // Timing is represented via OdfLength (mapped to inches)
                par1.AddEffect(OdfAnimationType.ZoomIn, "target1", OdfLength.FromInches(0), OdfLength.FromInches(-1));
                par1.AddEffect(OdfAnimationType.WipeRight, "target2", OdfLength.FromInches(9999), OdfLength.FromInches(0.5));

                doc.Save();
            }

            ms.Position = 0;
            using (var package = OdfPackage.Open(ms))
            {
                var doc = new PresentationDocument(package);
                var slide = doc.Slides[0];
                var rootSeq = slide.AnimationRoot;

                Assert.Equal(2, rootSeq.Children.Count);
                var seq1 = rootSeq.Children[0];
                Assert.Null(seq1.Begin);

                var seq2 = rootSeq.Children[1];
                Assert.Equal(string.Empty, seq2.Begin);

                var par1 = seq1.Children[0];
                Assert.Null(par1.Begin);

                Assert.Equal(2, par1.Children.Count);
                
                var effect1 = par1.Children[0];
                Assert.Equal("target1", effect1.TargetElement);
                Assert.Equal("0.00s", effect1.Dur);
                Assert.Equal("-1.00s", effect1.Begin);

                var effect2 = par1.Children[1];
                Assert.Equal("target2", effect2.TargetElement);
                Assert.Equal("9999.00s", effect2.Dur);
                Assert.Equal("0.50s", effect2.Begin);
            }
        }

        #endregion

        #region 4. Embedded Objects & Zip Slip Security Boundary Tests

        [Fact]
        public void TestEmbeddedDocumentsGetNonexistent()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);

            // Accessing non-existent embedded document does NOT throw FileNotFoundException;
            // instead, it constructs a default document structure in memory.
            // Let's verify it gets a non-null document and the package does not contain it in the manifest yet.
            var nonexistentDoc = doc.GetEmbeddedDocument<OdfChartDocument>("NonexistentObject");
            Assert.NotNull(nonexistentDoc);
            Assert.False(package.Manifest.ContainsKey("NonexistentObject/"));
        }

        [Fact]
        public void TestEmbeddedDocumentsDirectoryTraversal()
        {
            using var package = OdfPackage.Create(new MemoryStream());
            var doc = new PresentationDocument(package);

            // Attempt to create embedded document with parent directory traversal paths
            // It will throw SecurityException directly as WriteEntry is called before reflection-based instantiation
            var ex1 = Assert.Throws<SecurityException>(() => doc.CreateEmbeddedDocument<OdfChartDocument>("../EvilChart"));

            var ex2 = Assert.Throws<SecurityException>(() => doc.CreateEmbeddedDocument<OdfFormulaDocument>("SubFolder/../../EvilFormula"));

            var ex3 = Assert.Throws<SecurityException>(() => doc.CreateEmbeddedDocument<OdfChartDocument>("C:/Absolute/Path"));

            var ex4 = Assert.Throws<SecurityException>(() => doc.CreateEmbeddedDocument<OdfFormulaDocument>(@"\\UNC\Path"));

            // Attempt to get embedded document with traversal paths
            // It will throw TargetInvocationException wrapping SecurityException since constructors are invoked via reflection
            var ex5 = Assert.Throws<TargetInvocationException>(() => doc.GetEmbeddedDocument<OdfChartDocument>("../EvilChart"));
            Assert.IsType<SecurityException>(ex5.InnerException);

            var ex6 = Assert.Throws<TargetInvocationException>(() => doc.GetEmbeddedDocument<OdfFormulaDocument>("SubFolder/../../EvilFormula"));
            Assert.IsType<SecurityException>(ex6.InnerException);
        }

        #endregion

        #region 5. Slide Manipulation, Animations & Shape Boundary Tests

        [Fact]
        public void TestSlideManipulationBoundaryCases()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                Assert.Empty(doc.Slides);

                // Clone, delete, move on empty document should throw
                Assert.Throws<ArgumentOutOfRangeException>(() => doc.CloneSlide(0));
                Assert.Throws<ArgumentOutOfRangeException>(() => doc.DeleteSlide(0));
                Assert.Throws<ArgumentOutOfRangeException>(() => doc.MoveSlide(0, 0));

                var slide1 = doc.AddSlide("First");
                var slide2 = doc.AddSlide("Second");

                Assert.Equal(2, doc.Slides.Count);

                // Invalid index boundary checks
                Assert.Throws<ArgumentOutOfRangeException>(() => doc.CloneSlide(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => doc.CloneSlide(2));
                Assert.Throws<ArgumentOutOfRangeException>(() => doc.DeleteSlide(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => doc.DeleteSlide(2));
                Assert.Throws<ArgumentOutOfRangeException>(() => doc.MoveSlide(-1, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => doc.MoveSlide(0, -1));
                Assert.Throws<ArgumentOutOfRangeException>(() => doc.MoveSlide(0, 2));

                // Move slide to the same index (should be a no-op)
                doc.MoveSlide(0, 0);
                Assert.Equal("First", doc.Slides[0].Name);

                // Move slide to different index
                doc.MoveSlide(0, 1);
                Assert.Equal("Second", doc.Slides[0].Name);
                Assert.Equal("First", doc.Slides[1].Name);

                // Clone slide
                var cloned = doc.CloneSlide(1); // clone "First"
                Assert.Equal("First_Clone", cloned.Name);
                Assert.Equal(3, doc.Slides.Count);

                // Delete slide
                doc.DeleteSlide(2);
                Assert.Equal(2, doc.Slides.Count);
                Assert.Equal("Second", doc.Slides[0].Name);
                Assert.Equal("First", doc.Slides[1].Name);

                doc.Save();
            }
        }

        [Fact]
        public void TestSlideLayoutAndMasterPageExceptions()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);

                // Add master page boundary invalid values
                Assert.Throws<ArgumentException>(() => doc.AddMasterPage(null!, "PM1"));
                Assert.Throws<ArgumentException>(() => doc.AddMasterPage("", "PM1"));
                Assert.Throws<ArgumentException>(() => doc.AddMasterPage("Master1", null!));
                Assert.Throws<ArgumentException>(() => doc.AddMasterPage("Master1", ""));

                // Valid Master Page
                doc.AddMasterPage("Master1", "PM1");

                // Slide orientation boundary updates
                doc.SetSlideSize(OdfLength.Parse("10cm"), OdfLength.Parse("20cm")); // Portrait initially (W < H)
                doc.SetSlideOrientation(OdfPageOrientation.Landscape);
                doc.Save();
            }
        }

        [Fact]
        public void TestShapeAnimationAndExceptions()
        {
            using var ms = new MemoryStream();
            using (var package = OdfPackage.Create(ms, leaveOpen: true))
            {
                var doc = new PresentationDocument(package);
                var slide = doc.AddSlide("Slide1");
                
                // Add a text box
                var textBox = slide.AddTextBox(OdfLength.Parse("1cm"), OdfLength.Parse("2cm"), OdfLength.Parse("10cm"), OdfLength.Parse("5cm"), "Sample text");
                Assert.NotNull(textBox);
                Assert.StartsWith("frm_", textBox.Id); // Verify shape ID is auto-generated and valid (starts with frm_ since it's a frame)

                // Test animation on shape
                textBox.Animate(OdfAnimationType.FadeIn, OdfLength.FromInches(2), OdfLength.FromInches(1));

                // Verify Animation XML structure was created
                var animRoot = slide.AnimationRoot;
                Assert.NotNull(animRoot);

                // Create a standalone shape without slide
                var standaloneShapeNode = new OdfNode(OdfNodeType.Element, "rect", OdfNamespaces.Draw, "draw");
                var standaloneShape = new OdfShape(standaloneShapeNode, doc);
                
                // Animating a shape without slide should throw InvalidOperationException
                Assert.Throws<InvalidOperationException>(() => standaloneShape.Animate(OdfAnimationType.FadeIn, OdfLength.FromInches(1), OdfLength.FromInches(0)));

                // Add polyline with normal and empty point list
                var points = new[] { new PointF(0, 0), new PointF(10, 20), new PointF(30, 40) };
                var polyline = slide.AddPolyline(points, OdfLength.Parse("0cm"), OdfLength.Parse("0cm"), OdfLength.Parse("5cm"), OdfLength.Parse("5cm"));
                Assert.NotNull(polyline);

                var emptyPolyline = slide.AddPolyline(Array.Empty<PointF>(), OdfLength.Parse("0cm"), OdfLength.Parse("0cm"), OdfLength.Parse("0cm"), OdfLength.Parse("0cm"));
                Assert.NotNull(emptyPolyline);

                // Test Shape properties
                textBox.FillColor = "#FF0000";
                textBox.StrokeColor = "#0000FF";
                Assert.Equal("#FF0000", textBox.FillColor);
                Assert.Equal("#0000FF", textBox.StrokeColor);

                doc.Save();
            }
        }

        #endregion
    }
}
