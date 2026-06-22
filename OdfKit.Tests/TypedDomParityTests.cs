using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 typed DOM 與 ODFDOM 對標線的基本覆蓋能力。
/// </summary>
public class TypedDomParityTests
{
    /// <summary>
    /// 驗證 factory 會建立 generated 與手寫 typed wrapper，未知元素則回退為通用元素。
    /// </summary>
    [Fact]
    public void NodeFactoryCreatesGeneratedHandWrittenAndFallbackElements()
    {
        OdfNode generated = OdfNodeFactory.CreateElement(
            "animate",
            "urn:oasis:names:tc:opendocument:xmlns:animation:1.0",
            "anim");
        OdfNode handWritten = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        OdfNode fallback = OdfNodeFactory.CreateElement("custom-node", "urn:example:custom", "x");

        Assert.IsType<AnimationAnimateElement>(generated);
        Assert.IsType<TextPElement>(handWritten);
        Assert.IsType<OdfElement>(fallback);
        Assert.Equal("animate", generated.LocalName);
        Assert.Equal("p", handWritten.LocalName);
        Assert.Equal("custom-node", fallback.LocalName);
    }

    /// <summary>
    /// 驗證 typed DOM 可用 ODFDOM 風格建立、插入、列舉並 round-trip 子元素。
    /// </summary>
    [Fact]
    public void TypedDomChildFacadeSupportsOdfDomStyleUserStory()
    {
        OfficeDocumentContentElement document = new("office");
        document.SetOdfVersionAttributeValue("version", OdfNamespaces.Office, OdfVersion.Odf14, "office");
        OfficeBodyElement body = document.AppendElement(new OfficeBodyElement("office"));
        OfficeTextElement text = body.AppendElement(new OfficeTextElement("office"));
        TextPElement paragraph = text.AppendElement(new TextPElement("text"));
        TextSpanElement emphasis = paragraph.AppendElement(new TextSpanElement("text"));
        AnimationAnimateElement animation = paragraph.AppendElement(new AnimationAnimateElement("anim"));
        TextSpanElement prefix = paragraph.InsertElementBefore(new TextSpanElement("text"), emphasis);
        TextSpanElement suffix = paragraph.InsertElementAfter(new TextSpanElement("text"), emphasis);

        prefix.TextContent = "Hello ";
        emphasis.StyleName = "Emphasis";
        emphasis.TextContent = "typed DOM";
        suffix.TextContent = "!";
        animation.Formula = "width * 2";

        Assert.Same(body, document.ChildElements<OfficeBodyElement>().Single());
        Assert.Same(paragraph, document.DescendantElements<TextPElement>().Single());
        Assert.Equal([prefix, emphasis, suffix], paragraph.ChildElements<TextSpanElement>().ToArray());
        Assert.Same(animation, paragraph.ChildElements<AnimationAnimateElement>().Single());
        Assert.Equal("Hello typed DOM!", paragraph.TextContent);

        using MemoryStream stream = new();
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OdfNode parsed = OdfXmlReader.Parse(stream);
        OfficeDocumentContentElement parsedDocument = Assert.IsType<OfficeDocumentContentElement>(parsed);
        TextPElement parsedParagraph = parsedDocument.DescendantElements<TextPElement>().Single();
        TextSpanElement parsedEmphasis = parsedParagraph.ChildElements<TextSpanElement>().ElementAt(1);
        AnimationAnimateElement parsedAnimation = parsedParagraph.ChildElements<AnimationAnimateElement>().Single();

        Assert.Equal("typed DOM", parsedEmphasis.TextContent);
        Assert.Equal("Emphasis", parsedEmphasis.StyleName);
        Assert.Equal("width * 2", parsedAnimation.Formula);
        Assert.Equal("Hello typed DOM!", parsedParagraph.TextContent);
    }

    /// <summary>
    /// 驗證 schema-specific child collection 可支援 ODFDOM 風格的 typed traversal。
    /// </summary>
    [Fact]
    public void SchemaSpecificChildCollectionsSupportOdfDomSampleTraversal()
    {
        OfficeDocumentContentElement document = new("office");
        OfficeBodyElement body = document.AppendElement(new OfficeBodyElement("office"));
        OfficeTextElement text = body.AppendElement(new OfficeTextElement("office"));
        TextPElement firstParagraph = text.AppendElement(new TextPElement("text"));
        TextPElement secondParagraph = text.AppendElement(new TextPElement("text"));
        OfficeSpreadsheetElement spreadsheet = body.AppendElement(new OfficeSpreadsheetElement("office"));
        TableTableElement table = spreadsheet.AppendElement(new TableTableElement("table"));
        TableTableRowElement row = table.AppendElement(new TableTableRowElement("table"));
        TableTableCellElement cell = row.AppendElement(new TableTableCellElement("table"));
        TextPElement cellParagraph = cell.AppendElement(new TextPElement("text"));

        firstParagraph.TextContent = "ODFDOM typed traversal";
        secondParagraph.TextContent = "Schema child collection";
        table.Name = "Sheet1";
        cell.ValueType = "string";
        cellParagraph.TextContent = "A1";

        Assert.Equal([text], body.OfficeTextChildElements.ToArray());
        Assert.Equal([spreadsheet], body.OfficeSpreadsheetChildElements.ToArray());
        Assert.Equal([firstParagraph, secondParagraph], text.TextPChildElements.ToArray());
        Assert.Equal([table], spreadsheet.TableTableChildElements.ToArray());
        Assert.Equal([row], table.TableTableRowChildElements.ToArray());
        Assert.Equal([cell], row.TableTableCellChildElements.ToArray());
        Assert.Equal([cellParagraph], cell.TextPChildElements.ToArray());

        using MemoryStream stream = new();
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeDocumentContentElement parsedDocument = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream));
        OfficeBodyElement parsedBody = parsedDocument.OfficeBodyChildElements.Single();
        OfficeTextElement parsedText = parsedBody.OfficeTextChildElements.Single();
        TableTableElement parsedTable = parsedBody.OfficeSpreadsheetChildElements
            .Single()
            .TableTableChildElements
            .Single();
        TableTableCellElement parsedCell = parsedTable.TableTableRowChildElements
            .Single()
            .TableTableCellChildElements
            .Single();

        Assert.Equal("ODFDOM typed traversal", parsedText.TextPChildElements.First().TextContent);
        Assert.Equal("Schema child collection", parsedText.TextPChildElements.ElementAt(1).TextContent);
        Assert.Equal("Sheet1", parsedTable.Name);
        Assert.Equal("string", parsedCell.ValueType);
        Assert.Equal("A1", parsedCell.TextPChildElements.Single().TextContent);
    }

    /// <summary>
    /// 驗證 typed DOM 可用 ODFDOM 風格走訪圖文混排 frame、image 與 SVG 替代文字。
    /// </summary>
    [Fact]
    public void TypedDomSupportsOdfDomStyleImageFrameTraversal()
    {
        OfficeDocumentContentElement document = new("office");
        document.SetOdfVersionAttributeValue("version", OdfNamespaces.Office, OdfVersion.Odf14, "office");
        OfficeBodyElement body = document.AppendElement(new OfficeBodyElement("office"));
        OfficeTextElement text = body.AppendElement(new OfficeTextElement("office"));
        TextPElement paragraph = text.AppendElement(new TextPElement("text"));
        DrawFrameElement frame = paragraph.AppendElement(new DrawFrameElement("draw"));
        SvgTitleElement title = frame.AppendElement(new SvgTitleElement("svg"));
        SvgDescElement description = frame.AppendElement(new SvgDescElement("svg"));
        DrawImageElement image = frame.AppendElement(new DrawImageElement("draw"));
        TextPElement caption = image.AppendElement(new TextPElement("text"));

        frame.SetLengthAttributeValue("width", OdfNamespaces.Svg, OdfLength.FromCentimeters(4), "svg");
        frame.SetLengthAttributeValue("height", OdfNamespaces.Svg, OdfLength.FromCentimeters(3), "svg");
        title.TextContent = "產品截圖";
        description.TextContent = "含有替代文字的圖片 frame。";
        image.SetIriReferenceAttributeValue("href", OdfNamespaces.XLink, new OdfIriReference("Pictures/image.png"), "xlink");
        image.SetXLinkTypeAttributeValue("type", OdfNamespaces.XLink, OdfXLinkType.Simple, "xlink");
        image.SetXLinkShowAttributeValue("show", OdfNamespaces.XLink, OdfXLinkShow.Embed, "xlink");
        image.SetXLinkActuateAttributeValue("actuate", OdfNamespaces.XLink, OdfXLinkActuate.OnLoad, "xlink");
        caption.TextContent = "Figure 1";

        Assert.Same(frame, paragraph.ChildElements<DrawFrameElement>().Single());
        Assert.Equal([title], frame.SvgTitleChildElements.ToArray());
        Assert.Equal([description], frame.SvgDescChildElements.ToArray());
        Assert.Equal([image], frame.DrawImageChildElements.ToArray());
        Assert.Equal([caption], image.TextPChildElements.ToArray());
        Assert.Equal(OdfLength.FromCentimeters(4), frame.GetLengthAttributeValue("width", OdfNamespaces.Svg));
        Assert.Equal(OdfXLinkType.Simple, image.GetXLinkTypeAttributeValue("type", OdfNamespaces.XLink));
        Assert.Equal(OdfXLinkShow.Embed, image.GetXLinkShowAttributeValue("show", OdfNamespaces.XLink));
        Assert.Equal(OdfXLinkActuate.OnLoad, image.GetXLinkActuateAttributeValue("actuate", OdfNamespaces.XLink));

        using MemoryStream stream = new();
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeDocumentContentElement parsedDocument = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream));
        DrawFrameElement parsedFrame = parsedDocument.DescendantElements<DrawFrameElement>().Single();
        DrawImageElement parsedImage = parsedFrame.DrawImageChildElements.Single();

        Assert.Equal("產品截圖", parsedFrame.SvgTitleChildElements.Single().TextContent);
        Assert.Equal("含有替代文字的圖片 frame。", parsedFrame.SvgDescChildElements.Single().TextContent);
        Assert.Equal(OdfLength.FromCentimeters(3), parsedFrame.GetLengthAttributeValue("height", OdfNamespaces.Svg));
        Assert.Equal(new OdfIriReference("Pictures/image.png"), parsedImage.GetIriReferenceAttributeValue("href", OdfNamespaces.XLink));
        Assert.Equal(OdfXLinkShow.Embed, parsedImage.GetXLinkShowAttributeValue("show", OdfNamespaces.XLink));
        Assert.Equal("Figure 1", parsedImage.TextPChildElements.Single().TextContent);
    }

    /// <summary>
    /// 驗證 typed DOM 可用 ODFDOM 風格建立巢狀清單與註腳內容。
    /// </summary>
    [Fact]
    public void TypedDomSupportsOdfDomStyleListAndFootnoteTraversal()
    {
        OfficeDocumentContentElement document = new("office");
        document.SetOdfVersionAttributeValue("version", OdfNamespaces.Office, OdfVersion.Odf14, "office");
        OfficeBodyElement body = document.AppendElement(new OfficeBodyElement("office"));
        OfficeTextElement text = body.AppendElement(new OfficeTextElement("office"));
        TextHElement heading = text.AppendElement(new TextHElement("text"));
        TextListElement list = text.AppendElement(new TextListElement("text"));
        TextListItemElement firstItem = list.AppendElement(new TextListItemElement("text"));
        TextPElement firstParagraph = firstItem.AppendElement(new TextPElement("text"));
        firstParagraph.TextContent = "第一項";
        TextNoteElement note = firstParagraph.AppendElement(new TextNoteElement("text"));
        TextNoteCitationElement citation = note.AppendElement(new TextNoteCitationElement("text"));
        TextNoteBodyElement noteBody = note.AppendElement(new TextNoteBodyElement("text"));
        TextPElement noteParagraph = noteBody.AppendElement(new TextPElement("text"));
        TextListElement nestedList = firstItem.AppendElement(new TextListElement("text"));
        TextListItemElement nestedItem = nestedList.AppendElement(new TextListItemElement("text"));
        TextPElement nestedParagraph = nestedItem.AppendElement(new TextPElement("text"));
        TextListItemElement secondItem = list.AppendElement(new TextListItemElement("text"));
        TextPElement secondParagraph = secondItem.AppendElement(new TextPElement("text"));

        heading.OutlineLevel = 2;
        heading.TextContent = "工作清單";
        list.StyleName = "List_20_1";
        note.SetTextNoteClassAttributeValue("note-class", OdfNamespaces.Text, OdfTextNoteClass.Footnote, "text");
        citation.Label = "1";
        citation.TextContent = "1";
        noteParagraph.TextContent = "註腳說明";
        nestedParagraph.TextContent = "巢狀項目";
        secondParagraph.TextContent = "第二項";

        Assert.Equal([heading], text.TextHChildElements.ToArray());
        Assert.Equal([list], text.TextListChildElements.ToArray());
        Assert.Equal([firstItem, secondItem], list.TextListItemChildElements.ToArray());
        Assert.Equal([firstParagraph], firstItem.TextPChildElements.ToArray());
        Assert.Equal([nestedList], firstItem.TextListChildElements.ToArray());
        Assert.Equal([nestedItem], nestedList.TextListItemChildElements.ToArray());
        Assert.Equal([note], firstParagraph.ChildElements<TextNoteElement>().ToArray());
        Assert.Equal([citation], note.TextNoteCitationChildElements.ToArray());
        Assert.Equal([noteBody], note.TextNoteBodyChildElements.ToArray());
        Assert.Equal([noteParagraph], noteBody.ChildElements<TextPElement>().ToArray());
        Assert.Equal(OdfTextNoteClass.Footnote, note.GetTextNoteClassAttributeValue("note-class", OdfNamespaces.Text));
        Assert.Equal(2, heading.OutlineLevel);

        using MemoryStream stream = new();
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeDocumentContentElement parsedDocument = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream));
        OfficeTextElement parsedText = parsedDocument.OfficeBodyChildElements.Single().OfficeTextChildElements.Single();
        TextListElement parsedList = parsedText.TextListChildElements.Single();
        TextListItemElement parsedFirstItem = parsedList.TextListItemChildElements.First();
        TextNoteElement parsedNote = parsedFirstItem.TextPChildElements.Single().ChildElements<TextNoteElement>().Single();

        Assert.Equal("工作清單", parsedText.TextHChildElements.Single().TextContent);
        Assert.Equal("List_20_1", parsedList.StyleName);
        Assert.Equal("第一項1註腳說明", parsedFirstItem.TextPChildElements.Single().TextContent);
        Assert.Equal("巢狀項目", parsedFirstItem.TextListChildElements.Single().TextListItemChildElements.Single().TextPChildElements.Single().TextContent);
        Assert.Equal("第二項", parsedList.TextListItemChildElements.ElementAt(1).TextPChildElements.Single().TextContent);
        Assert.Equal("1", parsedNote.TextNoteCitationChildElements.Single().Label);
        Assert.Equal("註腳說明", parsedNote.TextNoteBodyChildElements.Single().ChildElements<TextPElement>().Single().TextContent);
        Assert.Equal(OdfTextNoteClass.Footnote, parsedNote.GetTextNoteClassAttributeValue("note-class", OdfNamespaces.Text));
    }

    /// <summary>
    /// 驗證 typed DOM 可用 ODFDOM 風格建立簡報頁面、文字方塊、圖形與備忘稿。
    /// </summary>
    [Fact]
    public void TypedDomSupportsOdfDomStylePresentationPageTraversal()
    {
        OfficeDocumentContentElement document = new("office");
        document.SetOdfVersionAttributeValue("version", OdfNamespaces.Office, OdfVersion.Odf14, "office");
        OfficeBodyElement body = document.AppendElement(new OfficeBodyElement("office"));
        OfficePresentationElement presentation = body.AppendElement(new OfficePresentationElement("office"));
        DrawPageElement page = presentation.AppendElement(new DrawPageElement("draw"));
        DrawFrameElement titleFrame = page.AppendElement(new DrawFrameElement("draw"));
        SvgTitleElement frameTitle = titleFrame.AppendElement(new SvgTitleElement("svg"));
        DrawTextBoxElement titleBox = titleFrame.AppendElement(new DrawTextBoxElement("draw"));
        TextPElement titleParagraph = titleBox.AppendElement(new TextPElement("text"));
        DrawRectElement accentShape = page.AppendElement(new DrawRectElement("draw"));
        PresentationNotesElement notes = page.AppendElement(new PresentationNotesElement("presentation"));
        DrawFrameElement notesFrame = notes.AppendElement(new DrawFrameElement("draw"));
        DrawTextBoxElement notesBox = notesFrame.AppendElement(new DrawTextBoxElement("draw"));
        TextPElement notesParagraph = notesBox.AppendElement(new TextPElement("text"));

        page.SetAttribute("name", OdfNamespaces.Draw, "Slide1", "draw");
        page.SetPresentationTransitionTypeAttributeValue(
            "transition-type",
            OdfNamespaces.Presentation,
            OdfPresentationTransitionType.Automatic,
            "presentation");
        page.SetPresentationSpeedAttributeValue(
            "transition-speed",
            OdfNamespaces.Presentation,
            OdfPresentationSpeed.Fast,
            "presentation");
        titleFrame.SetLengthAttributeValue("x", OdfNamespaces.Svg, OdfLength.FromCentimeters(1), "svg");
        titleFrame.SetLengthAttributeValue("y", OdfNamespaces.Svg, OdfLength.FromCentimeters(1), "svg");
        titleFrame.SetLengthAttributeValue("width", OdfNamespaces.Svg, OdfLength.FromCentimeters(18), "svg");
        titleFrame.SetLengthAttributeValue("height", OdfNamespaces.Svg, OdfLength.FromCentimeters(2), "svg");
        frameTitle.TextContent = "首頁標題";
        titleParagraph.TextContent = "ODFDOM 簡報 traversal";
        accentShape.SetAttribute("name", OdfNamespaces.Draw, "Accent", "draw");
        accentShape.SetLengthAttributeValue("x", OdfNamespaces.Svg, OdfLength.FromCentimeters(1), "svg");
        accentShape.SetLengthAttributeValue("y", OdfNamespaces.Svg, OdfLength.FromCentimeters(4), "svg");
        accentShape.SetLengthAttributeValue("width", OdfNamespaces.Svg, OdfLength.FromCentimeters(6), "svg");
        accentShape.SetLengthAttributeValue("height", OdfNamespaces.Svg, OdfLength.FromCentimeters(1), "svg");
        notesParagraph.TextContent = "講者備忘稿";

        Assert.Equal([page], presentation.DrawPageChildElements.ToArray());
        Assert.Equal([titleFrame], page.DrawFrameChildElements.ToArray());
        Assert.Equal([accentShape], page.DrawRectChildElements.ToArray());
        Assert.Equal([notes], page.PresentationNotesChildElements.ToArray());
        Assert.Equal([frameTitle], titleFrame.SvgTitleChildElements.ToArray());
        Assert.Equal([titleBox], titleFrame.DrawTextBoxChildElements.ToArray());
        Assert.Equal([titleParagraph], titleBox.TextPChildElements.ToArray());
        Assert.Equal([notesFrame], notes.DrawFrameChildElements.ToArray());
        Assert.Equal([notesBox], notesFrame.DrawTextBoxChildElements.ToArray());
        Assert.Equal(OdfPresentationTransitionType.Automatic, page.GetPresentationTransitionTypeAttributeValue("transition-type", OdfNamespaces.Presentation));
        Assert.Equal(OdfPresentationSpeed.Fast, page.GetPresentationSpeedAttributeValue("transition-speed", OdfNamespaces.Presentation));
        Assert.Equal(OdfLength.FromCentimeters(18), titleFrame.GetLengthAttributeValue("width", OdfNamespaces.Svg));

        using MemoryStream stream = new();
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeDocumentContentElement parsedDocument = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream));
        OfficePresentationElement parsedPresentation = parsedDocument.OfficeBodyChildElements.Single().OfficePresentationChildElements.Single();
        DrawPageElement parsedPage = parsedPresentation.DrawPageChildElements.Single();
        DrawFrameElement parsedTitleFrame = parsedPage.DrawFrameChildElements.Single();
        PresentationNotesElement parsedNotes = parsedPage.PresentationNotesChildElements.Single();

        Assert.Equal("Slide1", parsedPage.GetAttribute("name", OdfNamespaces.Draw));
        Assert.Equal("首頁標題", parsedTitleFrame.SvgTitleChildElements.Single().TextContent);
        Assert.Equal("ODFDOM 簡報 traversal", parsedTitleFrame.DrawTextBoxChildElements.Single().TextPChildElements.Single().TextContent);
        Assert.Equal("Accent", parsedPage.DrawRectChildElements.Single().GetAttribute("name", OdfNamespaces.Draw));
        Assert.Equal("講者備忘稿", parsedNotes.DrawFrameChildElements.Single().DrawTextBoxChildElements.Single().TextPChildElements.Single().TextContent);
        Assert.Equal(OdfPresentationTransitionType.Automatic, parsedPage.GetPresentationTransitionTypeAttributeValue("transition-type", OdfNamespaces.Presentation));
        Assert.Equal(OdfPresentationSpeed.Fast, parsedPage.GetPresentationSpeedAttributeValue("transition-speed", OdfNamespaces.Presentation));
        Assert.Equal(OdfLength.FromCentimeters(1), parsedTitleFrame.GetLengthAttributeValue("x", OdfNamespaces.Svg));
        Assert.Equal(OdfLength.FromCentimeters(1), parsedPage.DrawRectChildElements.Single().GetLengthAttributeValue("height", OdfNamespaces.Svg));
    }

    /// <summary>
    /// 驗證 typed DOM 可用 ODFDOM 風格建立與走訪內嵌 MathML 公式物件。
    /// </summary>
    [Fact]
    public void TypedDomSupportsOdfDomStyleMathMlFormulaObjectTraversal()
    {
        const string mathNamespace = "http://www.w3.org/1998/Math/MathML";
        OfficeDocumentContentElement document = new("office");
        document.SetOdfVersionAttributeValue("version", OdfNamespaces.Office, OdfVersion.Odf14, "office");
        OfficeBodyElement body = document.AppendElement(new OfficeBodyElement("office"));
        OfficeTextElement text = body.AppendElement(new OfficeTextElement("office"));
        TextPElement paragraph = text.AppendElement(new TextPElement("text"));
        DrawObjectElement formulaObject = paragraph.AppendElement(new DrawObjectElement("draw"));
        MathMLMathElement math = formulaObject.AppendElement(new MathMLMathElement("math"));
        OdfElement row = math.AppendElement(new OdfElement("mrow", mathNamespace, "math"));
        OdfElement identifier = row.AppendElement(new OdfElement("mi", mathNamespace, "math"));
        OdfElement operatorElement = row.AppendElement(new OdfElement("mo", mathNamespace, "math"));
        OdfElement number = row.AppendElement(new OdfElement("mn", mathNamespace, "math"));

        formulaObject.SetIriReferenceAttributeValue("href", OdfNamespaces.XLink, new OdfIriReference("./Object 1"), "xlink");
        formulaObject.SetXLinkTypeAttributeValue("type", OdfNamespaces.XLink, OdfXLinkType.Simple, "xlink");
        formulaObject.SetXLinkShowAttributeValue("show", OdfNamespaces.XLink, OdfXLinkShow.Embed, "xlink");
        formulaObject.SetXLinkActuateAttributeValue("actuate", OdfNamespaces.XLink, OdfXLinkActuate.OnLoad, "xlink");
        identifier.TextContent = "x";
        operatorElement.TextContent = "=";
        number.TextContent = "1";

        Assert.Equal([formulaObject], paragraph.ChildElements<DrawObjectElement>().ToArray());
        Assert.Equal([math], formulaObject.MathMLMathChildElements.ToArray());
        Assert.Equal("x=1", math.TextContent);
        Assert.Equal(new OdfIriReference("./Object 1"), formulaObject.GetIriReferenceAttributeValue("href", OdfNamespaces.XLink));
        Assert.Equal(OdfXLinkShow.Embed, formulaObject.GetXLinkShowAttributeValue("show", OdfNamespaces.XLink));

        using MemoryStream stream = new();
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeDocumentContentElement parsedDocument = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream));
        DrawObjectElement parsedObject = parsedDocument.DescendantElements<DrawObjectElement>().Single();
        MathMLMathElement parsedMath = parsedObject.MathMLMathChildElements.Single();

        Assert.Equal("x=1", parsedMath.TextContent);
        Assert.Equal("mrow", parsedMath.Children.Single().LocalName);
        Assert.Equal(mathNamespace, parsedMath.Children.Single().NamespaceUri);
        Assert.Equal(new OdfIriReference("./Object 1"), parsedObject.GetIriReferenceAttributeValue("href", OdfNamespaces.XLink));
        Assert.Equal(OdfXLinkType.Simple, parsedObject.GetXLinkTypeAttributeValue("type", OdfNamespaces.XLink));
        Assert.Equal(OdfXLinkActuate.OnLoad, parsedObject.GetXLinkActuateAttributeValue("actuate", OdfNamespaces.XLink));
    }

    /// <summary>
    /// 驗證 <c>office:text</c> content model facade 可依語意順序建立與列舉區塊內容。
    /// </summary>
    [Fact]
    public void OfficeTextContentModelFacadeSupportsBlockContentAppendAndEnumeration()
    {
        OfficeTextElement text = new("office");
        TextPElement paragraph = text.AppendParagraph("段落");
        TextHElement heading = text.AppendHeading("標題", 2);
        TextListElement list = text.AppendList("List_20_1");
        TextListItemElement item = list.AppendElement(new TextListItemElement("text"));
        item.AppendElement(new TextPElement("text")).TextContent = "清單項";
        TableTableElement table = text.AppendTable("內嵌表格");
        table.AppendRow().AppendElement(new TableTableCellElement("table"))
            .AppendElement(new TextPElement("text")).TextContent = "A1";

        OdfElement[] blockContent = text.BlockContentChildElements.ToArray();
        Assert.Equal([paragraph, heading, list, table], blockContent);
        Assert.Equal("段落", paragraph.TextContent);
        Assert.Equal(2, heading.OutlineLevel);
        Assert.Equal("清單項", list.TextListItemChildElements.Single().TextPChildElements.Single().TextContent);
        Assert.Equal("A1", table.TableTableRowChildElements.Single().TableTableCellChildElements.Single().TextPChildElements.Single().TextContent);
    }

    /// <summary>
    /// 驗證 <c>table:table</c> content model facade 可分隔欄位與列結構並 round-trip。
    /// </summary>
    [Fact]
    public void TableContentModelFacadeSupportsColumnAndRowStructure()
    {
        TableTableElement table = new("table");
        table.Name = "Sheet1";
        TableTableColumnElement column = table.AppendColumn();
        TableTableRowElement row = table.AppendRow();
        TableTableCellElement cell = row.AppendElement(new TableTableCellElement("table"));
        cell.AppendElement(new TextPElement("text")).TextContent = "資料";

        Assert.Single(table.ColumnStructureChildElements);
        Assert.Single(table.RowStructureChildElements);
        Assert.Same(column, table.TableTableColumnsChildElements.Single().TableTableColumnChildElements.Single());
        Assert.Same(row, table.TableTableRowChildElements.Single());

        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office"))
            .AppendElement(new OfficeSpreadsheetElement("office"))
            .AppendElement(table);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        TableTableElement parsed = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<TableTableElement>()
            .Single();
        Assert.Equal("Sheet1", parsed.Name);
        Assert.Equal("資料", parsed.TableTableRowChildElements.Single().TableTableCellChildElements.Single().TextContent);
    }

    /// <summary>
    /// 驗證 <c>draw:page</c> content model facade 可建立形狀與備忘稿並 round-trip。
    /// </summary>
    [Fact]
    public void DrawPageContentModelFacadeSupportsShapeAndNotesAppend()
    {
        DrawPageElement page = new("draw");
        page.SetAttribute("name", OdfNamespaces.Draw, "Slide1", "draw");
        DrawFrameElement frame = page.AppendFrame("Title");
        DrawRectElement rectangle = page.AppendRectangle("Accent");
        PresentationNotesElement notes = page.AppendNotes();
        notes.AppendElement(new DrawFrameElement("draw"))
            .AppendElement(new DrawTextBoxElement("draw"))
            .AppendElement(new TextPElement("text"))
            .TextContent = "備忘稿";

        OdfElement[] shapes = page.ShapeContentChildElements.ToArray();
        Assert.Equal([frame, rectangle], shapes);
        Assert.Single(page.PageAnnotationChildElements);
        Assert.Equal("Title", frame.GetAttribute("name", OdfNamespaces.Draw));

        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office"))
            .AppendElement(new OfficePresentationElement("office"))
            .AppendElement(page);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        DrawPageElement parsed = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<DrawPageElement>()
            .Single();
        Assert.Equal("Slide1", parsed.GetAttribute("name", OdfNamespaces.Draw));
        Assert.Equal("Accent", parsed.DrawRectChildElements.Single().GetAttribute("name", OdfNamespaces.Draw));
        Assert.Equal("備忘稿", parsed.PresentationNotesChildElements.Single().DrawFrameChildElements.Single().DrawTextBoxChildElements.Single().TextPChildElements.Single().TextContent);
    }

    /// <summary>
    /// 驗證 <c>office:presentation</c> content model facade 可建立投影片頁面並 round-trip。
    /// </summary>
    [Fact]
    public void OfficePresentationContentModelFacadeSupportsPageAppendAndEnumeration()
    {
        OfficePresentationElement presentation = new("office");
        DrawPageElement slide1 = presentation.AppendPage("Slide1");
        slide1.AppendRectangle("Title");
        DrawPageElement slide2 = presentation.AppendPage("Slide2");

        OdfElement[] pages = presentation.PresentationPageChildElements.ToArray();
        Assert.Equal(2, pages.Length);
        Assert.Same(slide1, pages[0]);
        Assert.Same(slide2, pages[1]);

        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office")).AppendElement(presentation);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficePresentationElement parsed = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<OfficePresentationElement>()
            .Single();
        string[] slideNames = parsed.PresentationPageChildElements
            .Select(page => page.GetAttribute("name", OdfNamespaces.Draw) ?? string.Empty)
            .ToArray();
        Assert.Equal(["Slide1", "Slide2"], slideNames);
        Assert.Equal("Title", parsed.DrawPageChildElements.First().DrawRectChildElements.Single().GetAttribute("name", OdfNamespaces.Draw));
    }

    /// <summary>
    /// 驗證 <c>office:drawing</c> content model facade 可建立繪圖頁面並 round-trip。
    /// </summary>
    [Fact]
    public void OfficeDrawingContentModelFacadeSupportsPageAppendAndEnumeration()
    {
        OfficeDrawingElement drawing = new("office");
        DrawPageElement page1 = drawing.AppendPage("Page1");
        page1.AppendFrame("Logo");
        DrawPageElement page2 = drawing.AppendPage("Page2");

        OdfElement[] pages = drawing.DrawingPageChildElements.ToArray();
        Assert.Equal(2, pages.Length);
        Assert.Same(page1, pages[0]);
        Assert.Same(page2, pages[1]);

        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office")).AppendElement(drawing);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeDrawingElement parsed = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<OfficeDrawingElement>()
            .Single();
        string[] pageNames = parsed.DrawingPageChildElements
            .Select(page => page.GetAttribute("name", OdfNamespaces.Draw) ?? string.Empty)
            .ToArray();
        Assert.Equal(["Page1", "Page2"], pageNames);
        Assert.Equal("Logo", parsed.DrawPageChildElements.First().DrawFrameChildElements.Single().GetAttribute("name", OdfNamespaces.Draw));
    }

    /// <summary>
    /// 驗證 <c>office:chart</c> content model facade 可建立圖表並 round-trip。
    /// </summary>
    [Fact]
    public void OfficeChartContentModelFacadeSupportsChartAppendAndEnumeration()
    {
        OfficeChartElement chartBody = new("office");
        ChartChartElement chart = chartBody.AppendChart();
        chart.ChartClass = "chart:bar";

        Assert.Single(chartBody.ChartMainChildElements);
        Assert.Same(chart, chartBody.ChartChartChildElements.Single());

        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office")).AppendElement(chartBody);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeChartElement parsed = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<OfficeChartElement>()
            .Single();
        Assert.Equal("chart:bar", parsed.ChartChartChildElements.Single().ChartClass);
    }

    /// <summary>
    /// 驗證 <c>office:image</c> content model facade 可建立影像框架並 round-trip。
    /// </summary>
    [Fact]
    public void OfficeImageContentModelFacadeSupportsFrameAppendAndEnumeration()
    {
        OfficeImageElement imageBody = new("office");
        DrawFrameElement frame = imageBody.AppendImageFrame("PrimaryFrame");

        Assert.Single(imageBody.ImageFrameChildElements);
        Assert.Equal("PrimaryFrame", frame.GetAttribute("name", OdfNamespaces.Draw));

        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office")).AppendElement(imageBody);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeImageElement parsed = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<OfficeImageElement>()
            .Single();
        Assert.Equal("PrimaryFrame", parsed.DrawFrameChildElements.Single().GetAttribute("name", OdfNamespaces.Draw));
    }

    /// <summary>
    /// 驗證 <c>office:database</c> content model facade 可建立元件容器並 round-trip。
    /// </summary>
    [Fact]
    public void OfficeDatabaseContentModelFacadeSupportsComponentContainers()
    {
        OfficeDatabaseElement databaseBody = new("office");
        databaseBody.EnsureDataSource();
        databaseBody.EnsureForms();
        databaseBody.EnsureQueries();

        OdfElement[] components = databaseBody.DatabaseComponentChildElements.ToArray();
        Assert.Equal(3, components.Length);
        Assert.IsType<DatabaseDataSourceElement>(components[0]);
        Assert.IsType<DatabaseFormsElement>(components[1]);
        Assert.IsType<DatabaseQueriesElement>(components[2]);

        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office")).AppendElement(databaseBody);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeDatabaseElement parsed = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<OfficeDatabaseElement>()
            .Single();
        Assert.Single(parsed.DatabaseDataSourceChildElements);
        Assert.Single(parsed.DatabaseFormsChildElements);
        Assert.Single(parsed.DatabaseQueriesChildElements);
    }

    /// <summary>
    /// 驗證 <c>office:database</c> content model facade 的 <c>EnsureReports</c>／
    /// <c>EnsureTableRepresentations</c> 即使在「逆序呼叫」（先建立順序較後的元件）時，
    /// 仍會將子元素插入在符合 ODF 規格 <c>db:data-source, db:forms, db:reports, db:queries,
    /// db:table-representations, db:schema</c> 正確順序的位置，並可正確 round-trip。
    /// </summary>
    [Fact]
    public void OfficeDatabaseContentModelFacadeEnsureReportsAndTableRepresentationsPreserveCanonicalOrder()
    {
        OfficeDatabaseElement databaseBody = new("office");

        // 刻意以反向順序呼叫：先建立順序較後的 table-representations，再建立 queries，
        // 最後才建立 reports，藉此驗證插入邏輯是否仍能維持規格要求的正確子元素順序。
        databaseBody.EnsureTableRepresentations();
        databaseBody.EnsureQueries();
        databaseBody.EnsureReports();

        OdfElement[] components = databaseBody.DatabaseComponentChildElements.ToArray();
        Assert.Equal(3, components.Length);
        Assert.IsType<DatabaseReportsElement>(components[0]);
        Assert.IsType<DatabaseQueriesElement>(components[1]);
        Assert.IsType<DatabaseTableRepresentationsElement>(components[2]);

        // 重複呼叫須回傳既有元素而非重複新增。
        Assert.Same(components[0], databaseBody.EnsureReports());
        Assert.Same(components[2], databaseBody.EnsureTableRepresentations());
        Assert.Equal(3, databaseBody.DatabaseComponentChildElements.Count());

        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office")).AppendElement(databaseBody);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeDatabaseElement parsed = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<OfficeDatabaseElement>()
            .Single();
        OdfElement[] parsedComponents = parsed.DatabaseComponentChildElements.ToArray();
        Assert.Equal(3, parsedComponents.Length);
        Assert.IsType<DatabaseReportsElement>(parsedComponents[0]);
        Assert.IsType<DatabaseQueriesElement>(parsedComponents[1]);
        Assert.IsType<DatabaseTableRepresentationsElement>(parsedComponents[2]);
    }

    /// <summary>
    /// 驗證 <c>office:database</c> content model facade 在「先建立 <c>db:reports</c>，再建立
    /// <c>db:queries</c>」這種兩兩相鄰元件的最小反例情境下，仍會將 <c>db:queries</c> 插入在
    /// <c>db:reports</c> 之後，而非插入之前。<c>InsertDatabaseComponent</c> 內部以
    /// 「尋找第一個屬於 reports／queries／table-representations／schema 任一類型的子元素」為插入
    /// 基準點，並未依個別呼叫的目標元件類型在規格序列中的相對位置調整搜尋範圍；若不限定範圍，
    /// 在僅有兩種元件、呼叫順序恰好是「先建立後面的，再建立前面的」以外的情境（例如本測試先建立
    /// reports 再建立 queries），插入邏輯仍只搜尋同一份清單，可能導致誤判插入點。
    /// </summary>
    [Fact]
    public void OfficeDatabaseContentModelFacadeEnsureQueriesAfterReportsPreservesOrder()
    {
        OfficeDatabaseElement databaseBody = new("office");

        databaseBody.EnsureReports();
        databaseBody.EnsureQueries();

        OdfElement[] components = databaseBody.DatabaseComponentChildElements.ToArray();
        Assert.Equal(2, components.Length);
        Assert.IsType<DatabaseReportsElement>(components[0]);
        Assert.IsType<DatabaseQueriesElement>(components[1]);
    }

    /// <summary>
    /// 驗證 <c>office:database</c> content model facade 的全部五個 <c>Ensure*</c> 方法
    /// （資料來源、表單、報表、查詢、資料表描述），無論呼叫端以何種順序呼叫，最終子元素順序
    /// 必定符合 ODF 規格規定的標準順序。逐一以完全反向（最後一個元件最先建立）的順序呼叫，
    /// 這是最容易暴露插入點誤判的情境。
    /// </summary>
    [Fact]
    public void OfficeDatabaseContentModelFacadeAllEnsureMethodsPreserveCanonicalOrderRegardlessOfCallOrder()
    {
        OfficeDatabaseElement databaseBody = new("office");

        // 完全反向呼叫：先建立規格順序最後面的元件，再依序往前建立。
        databaseBody.EnsureTableRepresentations();
        databaseBody.EnsureQueries();
        databaseBody.EnsureReports();
        databaseBody.EnsureForms();
        databaseBody.EnsureDataSource();

        OdfElement[] components = databaseBody.DatabaseComponentChildElements.ToArray();
        Assert.Equal(5, components.Length);
        Assert.IsType<DatabaseDataSourceElement>(components[0]);
        Assert.IsType<DatabaseFormsElement>(components[1]);
        Assert.IsType<DatabaseReportsElement>(components[2]);
        Assert.IsType<DatabaseQueriesElement>(components[3]);
        Assert.IsType<DatabaseTableRepresentationsElement>(components[4]);
    }

    /// <summary>
    /// 驗證 <c>office:text</c> content model facade 的 <c>AppendSection</c> 可正確建立具名章節，
    /// 章節內可巢狀段落，並能完整 round-trip。
    /// </summary>
    [Fact]
    public void OfficeTextContentModelFacadeAppendSectionCreatesNamedSectionAndRoundTrips()
    {
        OfficeTextElement textBody = new("office");
        textBody.AppendParagraph("先導段落");
        TextSectionElement section = textBody.AppendSection("Section1");
        section.AppendElement(new TextPElement("text")).TextContent = "章節內文";

        Assert.Equal("Section1", section.Name);
        OdfElement[] blocks = textBody.BlockContentChildElements.ToArray();
        Assert.Equal(2, blocks.Length);
        Assert.IsType<TextSectionElement>(blocks[1]);

        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office")).AppendElement(textBody);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeTextElement parsed = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<OfficeTextElement>()
            .Single();
        TextSectionElement parsedSection = parsed.DescendantElements<TextSectionElement>().Single();
        Assert.Equal("Section1", parsedSection.Name);
        Assert.Equal("章節內文", parsedSection.TextPChildElements.Single().TextContent);
    }

    /// <summary>
    /// 驗證 <c>table:table</c> content model facade 的 <c>AppendHeaderRows</c>／
    /// <c>EnsureTableColumns</c>／<c>InsertColumnStructure</c>：即使先新增資料列，
    /// 後續才補建表頭列容器與欄位結構，仍會插入在符合 ODF 規格「欄位結構先於列結構」
    /// 的正確位置，而非單純附加於末尾，並可完整 round-trip。
    /// </summary>
    [Fact]
    public void TableTableContentModelFacadeAppendHeaderRowsAndEnsureTableColumnsInsertBeforeExistingRows()
    {
        TableTableElement table = new("table");

        // 刻意先新增一般資料列，再回頭補建欄位結構與表頭列容器，
        // 驗證 InsertColumnStructure 是否仍會插入在所有列結構之前。
        TableTableRowElement dataRow = table.AppendRow();
        dataRow.AppendElement(new TableTableCellElement("table"));

        TableTableHeaderRowsElement headerRows = table.AppendHeaderRows();
        headerRows.AppendElement(new TableTableRowElement("table"))
            .AppendElement(new TableTableCellElement("table"));

        TableTableColumnsElement columns = table.EnsureTableColumns();
        columns.AppendElement(new TableTableColumnElement("table"));

        // 欄位結構必須出現在所有列結構（表頭列、資料列）之前。
        OdfElement[] columnStructures = table.ColumnStructureChildElements.ToArray();
        OdfElement[] rowStructures = table.RowStructureChildElements.ToArray();
        Assert.Single(columnStructures);
        Assert.Equal(2, rowStructures.Length);
        Assert.IsType<TableTableHeaderRowsElement>(rowStructures[0]);
        Assert.IsType<TableTableRowElement>(rowStructures[1]);
        Assert.True(table.Children.IndexOf(columns) < table.Children.IndexOf(headerRows));
        Assert.True(table.Children.IndexOf(headerRows) < table.Children.IndexOf(dataRow));

        // 再次呼叫 EnsureTableColumns 必須回傳既有容器，不可重複新增。
        Assert.Same(columns, table.EnsureTableColumns());

        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        OfficeSpreadsheetElement spreadsheet = document.AppendElement(new OfficeBodyElement("office"))
            .AppendElement(new OfficeSpreadsheetElement("office"));
        spreadsheet.AppendElement(table);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        TableTableElement parsed = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<TableTableElement>()
            .Single();
        Assert.Single(parsed.TableTableColumnsChildElements);
        Assert.Single(parsed.TableTableHeaderRowsChildElements);
        Assert.Single(parsed.TableTableRowChildElements);
    }

    /// <summary>
    /// 驗證 <c>office:spreadsheet</c> content model facade 可建立工作表並 round-trip。
    /// </summary>
    [Fact]
    public void OfficeSpreadsheetContentModelFacadeSupportsTableAppendAndEnumeration()
    {
        OfficeSpreadsheetElement spreadsheet = new("office");
        TableTableElement sheet = spreadsheet.AppendTable("Sheet1");
        sheet.AppendRow().AppendElement(new TableTableCellElement("table"))
            .AppendElement(new TextPElement("text")).TextContent = "A1";

        Assert.Single(spreadsheet.SpreadsheetTableChildElements);
        Assert.Equal("Sheet1", sheet.Name);

        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office")).AppendElement(spreadsheet);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        OfficeSpreadsheetElement parsed = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<OfficeSpreadsheetElement>()
            .Single();
        Assert.Equal("Sheet1", parsed.TableTableChildElements.Single().Name);
        Assert.Equal("A1", parsed.TableTableChildElements.Single().TableTableRowChildElements.Single().TableTableCellChildElements.Single().TextContent);
    }

    /// <summary>
    /// 驗證 generated DOM wrapper 的 class、factory case 與屬性數量沒有意外退化。
    /// </summary>
    [Fact]
    public void GeneratedDomWrapperCoverageDoesNotRegressBelowParityFloor()
    {
        string repoRoot = FindRepositoryRoot();
        string generatedPath = Path.Combine(repoRoot, "OdfKit", "DOM", "Generated", "GeneratedDomWrappers.g.cs");
        string generated = File.ReadAllText(generatedPath);

        int classCount = Regex.Matches(generated, @"public partial class \w+Element").Count;
        int factoryCaseCount = Regex.Matches(generated, "case \".*\": return new .*Element\\(prefix\\);").Count;
        int childCollectionPropertyCount = Regex.Matches(generated, @"public IEnumerable<\w+Element> \w+ChildElements").Count;
        int stringPropertyCount = Regex.Matches(generated, @"public string\? \w+").Count;
        int intPropertyCount = Regex.Matches(generated, @"public int\? \w+").Count;
        int boolPropertyCount = Regex.Matches(generated, @"public bool\? \w+").Count;
        int decimalPropertyCount = Regex.Matches(generated, @"public decimal\? \w+").Count;
        int dateTimePropertyCount = Regex.Matches(generated, @"public DateTime\? \w+").Count;
        int timePropertyCount = Regex.Matches(generated, @"public OdfTime\? \w+").Count;
        int lengthPropertyCount = Regex.Matches(generated, @"public OdfLength\? \w+").Count;
        int borderWidthsPropertyCount = Regex.Matches(generated, @"public OdfBorderWidths\? \w+").Count;
        int durationPropertyCount = Regex.Matches(generated, @"public OdfDuration\? \w+").Count;
        int anglePropertyCount = Regex.Matches(generated, @"public OdfAngle\? \w+").Count;
        int styleNamePropertyCount = Regex.Matches(generated, @"public OdfStyleName\? \w+").Count;
        int styleNameListPropertyCount = Regex.Matches(generated, @"public OdfStyleNameList\? \w+").Count;
        int colorPropertyCount = Regex.Matches(generated, @"public OdfColor\? \w+").Count;
        int iriReferencePropertyCount = Regex.Matches(generated, @"public OdfIriReference\? \w+").Count;
        int percentPropertyCount = Regex.Matches(generated, @"public OdfPercent\? \w+").Count;
        int cellAddressPropertyCount = Regex.Matches(generated, @"public OdfCellAddressReference\? \w+").Count;
        int cellRangeAddressPropertyCount = Regex.Matches(generated, @"public OdfCellRangeAddress\? \w+").Count;
        int cellRangeAddressListPropertyCount = Regex.Matches(generated, @"public OdfCellRangeAddressList\? \w+").Count;
        int vector3DPropertyCount = Regex.Matches(generated, @"public OdfVector3D\? \w+").Count;
        int point3DPropertyCount = Regex.Matches(generated, @"public OdfPoint3D\? \w+").Count;
        int pointListPropertyCount = Regex.Matches(generated, @"public OdfPointList\? \w+").Count;
        int languageCodePropertyCount = Regex.Matches(generated, @"public OdfLanguageCode\? \w+").Count;
        int countryCodePropertyCount = Regex.Matches(generated, @"public OdfCountryCode\? \w+").Count;
        int scriptCodePropertyCount = Regex.Matches(generated, @"public OdfScriptCode\? \w+").Count;
        int languageTagPropertyCount = Regex.Matches(generated, @"public OdfLanguageTag\? \w+").Count;
        int namespacedTokenPropertyCount = Regex.Matches(generated, @"public OdfNamespacedToken\? \w+").Count;
        int characterPropertyCount = Regex.Matches(generated, @"public OdfCharacter\? \w+").Count;
        int textEncodingPropertyCount = Regex.Matches(generated, @"public OdfTextEncoding\? \w+").Count;
        int targetFrameNamePropertyCount = Regex.Matches(generated, @"public OdfTargetFrameName\? \w+").Count;
        int xLinkTypePropertyCount = Regex.Matches(generated, @"public OdfXLinkType\? \w+").Count;
        int xLinkShowPropertyCount = Regex.Matches(generated, @"public OdfXLinkShow\? \w+").Count;
        int xLinkActuatePropertyCount = Regex.Matches(generated, @"public OdfXLinkActuate\? \w+").Count;
        int numberStylePropertyCount = Regex.Matches(generated, @"public OdfNumberStyle\? \w+").Count;
        int numberCalendarPropertyCount = Regex.Matches(generated, @"public OdfNumberCalendar\? \w+").Count;
        int tableOrderPropertyCount = Regex.Matches(generated, @"public OdfTableOrder\? \w+").Count;
        int tableTypePropertyCount = Regex.Matches(generated, @"public OdfTableType\? \w+").Count;
        int presentationEffectPropertyCount = Regex.Matches(generated, @"public OdfPresentationEffect\? \w+").Count;
        int presentationSpeedPropertyCount = Regex.Matches(generated, @"public OdfPresentationSpeed\? \w+").Count;
        int presentationActionPropertyCount = Regex.Matches(generated, @"public OdfPresentationAction\? \w+").Count;
        int presentationTransitionTypePropertyCount = Regex.Matches(generated, @"public OdfPresentationTransitionType\? \w+").Count;
        int presentationTransitionStylePropertyCount = Regex.Matches(generated, @"public OdfPresentationTransitionStyle\? \w+").Count;
        int foTextTransformPropertyCount = Regex.Matches(generated, @"public OdfFoTextTransform\? \w+").Count;
        int foTextAlignPropertyCount = Regex.Matches(generated, @"public OdfFoTextAlign\? \w+").Count;
        int styleTextRotationScalePropertyCount = Regex.Matches(generated, @"public OdfStyleTextRotationScale\? \w+").Count;
        int styleTextCombinePropertyCount = Regex.Matches(generated, @"public OdfStyleTextCombine\? \w+").Count;
        int drawFillPropertyCount = Regex.Matches(generated, @"public OdfDrawFill\? \w+").Count;
        int drawFillImageRefPointPropertyCount = Regex.Matches(generated, @"public OdfDrawFillImageRefPoint\? \w+").Count;
        int drawColorModePropertyCount = Regex.Matches(generated, @"public OdfDrawColorMode\? \w+").Count;
        int styleVerticalAlignPropertyCount = Regex.Matches(generated, @"public OdfStyleVerticalAlign\? \w+").Count;
        int styleVerticalPosPropertyCount = Regex.Matches(generated, @"public OdfStyleVerticalPos\? \w+").Count;
        int styleVerticalRelPropertyCount = Regex.Matches(generated, @"public OdfStyleVerticalRel\? \w+").Count;
        int styleHorizontalPosPropertyCount = Regex.Matches(generated, @"public OdfStyleHorizontalPos\? \w+").Count;
        int styleHorizontalRelPropertyCount = Regex.Matches(generated, @"public OdfStyleHorizontalRel\? \w+").Count;
        int styleWrapPropertyCount = Regex.Matches(generated, @"public OdfStyleWrap\? \w+").Count;
        int styleRunThroughPropertyCount = Regex.Matches(generated, @"public OdfStyleRunThrough\? \w+").Count;
        int styleWrapContourModePropertyCount = Regex.Matches(generated, @"public OdfStyleWrapContourMode\? \w+").Count;
        int styleWritingModePropertyCount = Regex.Matches(generated, @"public OdfStyleWritingMode\? \w+").Count;
        int tableDisplayMemberModePropertyCount = Regex.Matches(generated, @"public OdfTableDisplayMemberMode\? \w+").Count;
        int tableLayoutModePropertyCount = Regex.Matches(generated, @"public OdfTableLayoutMode\? \w+").Count;
        int tableMemberTypePropertyCount = Regex.Matches(generated, @"public OdfTableMemberType\? \w+").Count;
        int tableGroupedByPropertyCount = Regex.Matches(generated, @"public OdfTableGroupedBy\? \w+").Count;
        int tableSortModePropertyCount = Regex.Matches(generated, @"public OdfTableSortMode\? \w+").Count;
        int tableConditionSourcePropertyCount = Regex.Matches(generated, @"public OdfTableConditionSource\? \w+").Count;
        int tableFunctionPropertyCount = Regex.Matches(generated, @"public OdfTableFunction\? \w+").Count;
        int databaseRulePropertyCount = Regex.Matches(generated, @"public OdfDatabaseRule\? \w+").Count;
        int databaseIsNullablePropertyCount = Regex.Matches(generated, @"public OdfDatabaseIsNullable\? \w+").Count;
        int databaseDataSourceSettingTypePropertyCount = Regex.Matches(generated, @"public OdfDatabaseDataSourceSettingType\? \w+").Count;
        int animationColorInterpolationPropertyCount = Regex.Matches(generated, @"public OdfAnimationColorInterpolation\? \w+").Count;
        int animationColorInterpolationDirectionPropertyCount = Regex.Matches(generated, @"public OdfAnimationColorInterpolationDirection\? \w+").Count;
        int drawNoHrefPropertyCount = Regex.Matches(generated, @"public OdfDrawNoHref\? \w+").Count;
        int presentationPresetClassPropertyCount = Regex.Matches(generated, @"public OdfPresentationPresetClass\? \w+").Count;
        int numberTransliterationStylePropertyCount = Regex.Matches(generated, @"public OdfNumberTransliterationStyle\? \w+").Count;
        int styleScriptTypePropertyCount = Regex.Matches(generated, @"public OdfStyleScriptType\? \w+").Count;
        int styleTextEmphasizePropertyCount = Regex.Matches(generated, @"public OdfStyleTextEmphasize\? \w+").Count;
        int drawStrokeLineJoinPropertyCount = Regex.Matches(generated, @"public OdfDrawStrokeLineJoin\? \w+").Count;
        int svgStrokeLineCapPropertyCount = Regex.Matches(generated, @"public OdfSvgStrokeLineCap\? \w+").Count;
        int foKeepTogetherPropertyCount = Regex.Matches(generated, @"public OdfFoKeepTogether\? \w+").Count;
        int foWrapOptionPropertyCount = Regex.Matches(generated, @"public OdfFoWrapOption\? \w+").Count;
        int dr3dProjectionPropertyCount = Regex.Matches(generated, @"public OdfDr3dProjection\? \w+").Count;
        int dr3dShadeModePropertyCount = Regex.Matches(generated, @"public OdfDr3dShadeMode\? \w+").Count;
        int svgFillRulePropertyCount = Regex.Matches(generated, @"public OdfSvgFillRule\? \w+").Count;
        int tableBorderModelPropertyCount = Regex.Matches(generated, @"public OdfTableBorderModel\? \w+").Count;
        int textLabelFollowedByPropertyCount = Regex.Matches(generated, @"public OdfTextLabelFollowedBy\? \w+").Count;
        int textListLevelPositionModePropertyCount = Regex.Matches(generated, @"public OdfTextListLevelPositionMode\? \w+").Count;
        int textIndexScopePropertyCount = Regex.Matches(generated, @"public OdfTextIndexScope\? \w+").Count;
        int textTableTypePropertyCount = Regex.Matches(generated, @"public OdfTextTableType\? \w+").Count;
        int textAnchorTypePropertyCount = Regex.Matches(generated, @"public OdfTextAnchorType\? \w+").Count;
        int textNoteClassPropertyCount = Regex.Matches(generated, @"public OdfTextNoteClass\? \w+").Count;
        int textSelectPagePropertyCount = Regex.Matches(generated, @"public OdfTextSelectPage\? \w+").Count;
        int textReferenceFormatPropertyCount = Regex.Matches(generated, @"public OdfTextReferenceFormat\? \w+").Count;
        int textStartNumberingAtPropertyCount = Regex.Matches(generated, @"public OdfTextStartNumberingAt\? \w+").Count;
        int textFootnotesPositionPropertyCount = Regex.Matches(generated, @"public OdfTextFootnotesPosition\? \w+").Count;
        int textCaptionSequenceFormatPropertyCount = Regex.Matches(generated, @"public OdfTextCaptionSequenceFormat\? \w+").Count;
        int textNumberPositionPropertyCount = Regex.Matches(generated, @"public OdfTextNumberPosition\? \w+").Count;
        int textPlaceholderTypePropertyCount = Regex.Matches(generated, @"public OdfTextPlaceholderType\? \w+").Count;
        int textAnimationPropertyCount = Regex.Matches(generated, @"public OdfTextAnimation\? \w+").Count;
        int textAnimationDirectionPropertyCount = Regex.Matches(generated, @"public OdfTextAnimationDirection\? \w+").Count;
        int textKindPropertyCount = Regex.Matches(generated, @"public OdfTextKind\? \w+").Count;
        int lineStylePropertyCount = Regex.Matches(generated, @"public OdfLineStyle\? \w+").Count;
        int lineTypePropertyCount = Regex.Matches(generated, @"public OdfLineType\? \w+").Count;
        int lineWidthPropertyCount = Regex.Matches(generated, @"public OdfLineWidth\? \w+").Count;
        int lineModePropertyCount = Regex.Matches(generated, @"public OdfLineMode\? \w+").Count;
        int fontStylePropertyCount = Regex.Matches(generated, @"public OdfFontStyle\? \w+").Count;
        int fontVariantPropertyCount = Regex.Matches(generated, @"public OdfFontVariant\? \w+").Count;
        int fontWeightPropertyCount = Regex.Matches(generated, @"public OdfFontWeight\? \w+").Count;
        int fontFamilyGenericPropertyCount = Regex.Matches(generated, @"public OdfFontFamilyGeneric\? \w+").Count;
        int fontPitchPropertyCount = Regex.Matches(generated, @"public OdfFontPitch\? \w+").Count;
        int fontReliefPropertyCount = Regex.Matches(generated, @"public OdfFontRelief\? \w+").Count;
        int fontStretchPropertyCount = Regex.Matches(generated, @"public OdfFontStretch\? \w+").Count;
        int styleLineBreakPropertyCount = Regex.Matches(generated, @"public OdfStyleLineBreak\? \w+").Count;
        int styleRepeatPropertyCount = Regex.Matches(generated, @"public OdfStyleRepeat\? \w+").Count;
        int styleDirectionPropertyCount = Regex.Matches(generated, @"public OdfStyleDirection\? \w+").Count;
        int formOrientationPropertyCount = Regex.Matches(generated, @"public OdfFormOrientation\? \w+").Count;
        int tableDirectionPropertyCount = Regex.Matches(generated, @"public OdfTableDirection\? \w+").Count;
        int tableOrientationPropertyCount = Regex.Matches(generated, @"public OdfTableOrientation\? \w+").Count;
        int xmlNamePropertyCount = Regex.Matches(generated, @"public OdfXmlName\? \w+").Count;
        int styleFamilyPropertyCount = Regex.Matches(generated, @"public OdfStyleFamily\? \w+").Count;
        int odfVersionPropertyCount = Regex.Matches(generated, @"public OdfVersion\? \w+").Count;
        int mediaTypePropertyCount = Regex.Matches(generated, @"public OdfMediaType\? \w+").Count;
        int propertyCount = childCollectionPropertyCount + stringPropertyCount + intPropertyCount + boolPropertyCount + decimalPropertyCount + dateTimePropertyCount + timePropertyCount + lengthPropertyCount + borderWidthsPropertyCount + durationPropertyCount + anglePropertyCount + styleNamePropertyCount + styleNameListPropertyCount + colorPropertyCount + iriReferencePropertyCount + xLinkTypePropertyCount + xLinkShowPropertyCount + xLinkActuatePropertyCount + numberStylePropertyCount + numberCalendarPropertyCount + tableOrderPropertyCount + tableTypePropertyCount + presentationEffectPropertyCount + presentationSpeedPropertyCount + presentationActionPropertyCount + presentationTransitionTypePropertyCount + presentationTransitionStylePropertyCount + foTextTransformPropertyCount + foTextAlignPropertyCount + styleTextRotationScalePropertyCount + styleTextCombinePropertyCount + drawFillPropertyCount + drawFillImageRefPointPropertyCount + drawColorModePropertyCount + styleVerticalAlignPropertyCount + styleVerticalPosPropertyCount + styleVerticalRelPropertyCount + styleHorizontalPosPropertyCount + styleHorizontalRelPropertyCount + styleWrapPropertyCount + styleRunThroughPropertyCount + styleWrapContourModePropertyCount + styleWritingModePropertyCount + tableDisplayMemberModePropertyCount + tableLayoutModePropertyCount + tableMemberTypePropertyCount + tableGroupedByPropertyCount + tableSortModePropertyCount + tableConditionSourcePropertyCount + tableFunctionPropertyCount + databaseRulePropertyCount + databaseIsNullablePropertyCount + databaseDataSourceSettingTypePropertyCount + animationColorInterpolationPropertyCount + animationColorInterpolationDirectionPropertyCount + drawNoHrefPropertyCount + presentationPresetClassPropertyCount + numberTransliterationStylePropertyCount + styleScriptTypePropertyCount + styleTextEmphasizePropertyCount + drawStrokeLineJoinPropertyCount + svgStrokeLineCapPropertyCount + foKeepTogetherPropertyCount + foWrapOptionPropertyCount + dr3dProjectionPropertyCount + dr3dShadeModePropertyCount + svgFillRulePropertyCount + tableBorderModelPropertyCount + textLabelFollowedByPropertyCount + textListLevelPositionModePropertyCount + textIndexScopePropertyCount + textTableTypePropertyCount + textAnchorTypePropertyCount + textNoteClassPropertyCount + textSelectPagePropertyCount + textReferenceFormatPropertyCount + textStartNumberingAtPropertyCount + textFootnotesPositionPropertyCount + textCaptionSequenceFormatPropertyCount + textNumberPositionPropertyCount + textPlaceholderTypePropertyCount + textAnimationPropertyCount + textAnimationDirectionPropertyCount + textKindPropertyCount + percentPropertyCount + cellAddressPropertyCount + cellRangeAddressPropertyCount + cellRangeAddressListPropertyCount + vector3DPropertyCount + point3DPropertyCount + pointListPropertyCount + languageCodePropertyCount + countryCodePropertyCount + scriptCodePropertyCount + languageTagPropertyCount + namespacedTokenPropertyCount + characterPropertyCount + textEncodingPropertyCount + targetFrameNamePropertyCount + lineStylePropertyCount + lineTypePropertyCount + lineWidthPropertyCount + lineModePropertyCount + fontStylePropertyCount + fontVariantPropertyCount + fontWeightPropertyCount + fontFamilyGenericPropertyCount + fontPitchPropertyCount + fontReliefPropertyCount + fontStretchPropertyCount + styleLineBreakPropertyCount + styleRepeatPropertyCount + styleDirectionPropertyCount + formOrientationPropertyCount + tableDirectionPropertyCount + tableOrientationPropertyCount + xmlNamePropertyCount + styleFamilyPropertyCount + odfVersionPropertyCount + mediaTypePropertyCount;

        Assert.True(classCount >= 550, "generated typed element class count regressed: " + classCount);
        Assert.True(factoryCaseCount >= 590, "generated factory case count regressed: " + factoryCaseCount);
        Assert.True(propertyCount >= 100000, "generated attribute property count regressed: " + propertyCount);
        Assert.True(childCollectionPropertyCount >= 2000, "generated child collection property count regressed: " + childCollectionPropertyCount);
        Assert.True(intPropertyCount >= 1000, "generated integer attribute property count regressed: " + intPropertyCount);
        Assert.True(boolPropertyCount >= 10000, "generated boolean attribute property count regressed: " + boolPropertyCount);
        Assert.True(decimalPropertyCount >= 100, "generated decimal attribute property count regressed: " + decimalPropertyCount);
        Assert.True(dateTimePropertyCount >= 100, "generated date/time attribute property count regressed: " + dateTimePropertyCount);
        Assert.True(timePropertyCount >= 6, "generated time attribute property count regressed: " + timePropertyCount);
        Assert.True(lengthPropertyCount >= 10000, "generated length attribute property count regressed: " + lengthPropertyCount);
        Assert.True(borderWidthsPropertyCount >= 723, "generated border widths attribute property count regressed: " + borderWidthsPropertyCount);
        Assert.True(durationPropertyCount >= 1000, "generated duration attribute property count regressed: " + durationPropertyCount);
        Assert.True(anglePropertyCount >= 1000, "generated angle attribute property count regressed: " + anglePropertyCount);
        Assert.True(styleNamePropertyCount >= 1000, "generated style name attribute property count regressed: " + styleNamePropertyCount);
        Assert.True(styleNameListPropertyCount >= 300, "generated style name list attribute property count regressed: " + styleNameListPropertyCount);
        Assert.True(colorPropertyCount >= 1000, "generated color attribute property count regressed: " + colorPropertyCount);
        Assert.True(iriReferencePropertyCount >= 400, "generated IRI reference attribute property count regressed: " + iriReferencePropertyCount);
        Assert.True(percentPropertyCount >= 1000, "generated percent attribute property count regressed: " + percentPropertyCount);
        Assert.True(cellAddressPropertyCount >= 400, "generated cell address attribute property count regressed: " + cellAddressPropertyCount);
        Assert.True(cellRangeAddressPropertyCount >= 400, "generated cell range address attribute property count regressed: " + cellRangeAddressPropertyCount);
        Assert.True(cellRangeAddressListPropertyCount >= 800, "generated cell range address list attribute property count regressed: " + cellRangeAddressListPropertyCount);
        Assert.True(vector3DPropertyCount >= 1000, "generated vector3D attribute property count regressed: " + vector3DPropertyCount);
        Assert.True(point3DPropertyCount >= 90, "generated point3D attribute property count regressed: " + point3DPropertyCount);
        Assert.True(pointListPropertyCount >= 90, "generated point list attribute property count regressed: " + pointListPropertyCount);
        Assert.True(languageCodePropertyCount >= 100, "generated language code attribute property count regressed: " + languageCodePropertyCount);
        Assert.True(countryCodePropertyCount >= 100, "generated country code attribute property count regressed: " + countryCodePropertyCount);
        Assert.True(scriptCodePropertyCount >= 100, "generated script code attribute property count regressed: " + scriptCodePropertyCount);
        Assert.True(languageTagPropertyCount >= 100, "generated language tag attribute property count regressed: " + languageTagPropertyCount);
        Assert.True(namespacedTokenPropertyCount >= 100, "generated namespaced token attribute property count regressed: " + namespacedTokenPropertyCount);
        Assert.True(characterPropertyCount >= 100, "generated character attribute property count regressed: " + characterPropertyCount);
        Assert.True(textEncodingPropertyCount >= 438, "generated text encoding attribute property count regressed: " + textEncodingPropertyCount);
        Assert.True(targetFrameNamePropertyCount >= 205, "generated target frame name attribute property count regressed: " + targetFrameNamePropertyCount);
        Assert.True(xLinkTypePropertyCount >= 172, "generated XLink type attribute property count regressed: " + xLinkTypePropertyCount);
        Assert.True(xLinkShowPropertyCount >= 160, "generated XLink show attribute property count regressed: " + xLinkShowPropertyCount);
        Assert.True(xLinkActuatePropertyCount >= 167, "generated XLink actuate attribute property count regressed: " + xLinkActuatePropertyCount);
        Assert.True(numberStylePropertyCount >= 109, "generated number style attribute property count regressed: " + numberStylePropertyCount);
        Assert.True(numberCalendarPropertyCount >= 106, "generated number calendar attribute property count regressed: " + numberCalendarPropertyCount);
        Assert.True(tableOrderPropertyCount >= 108, "generated table order attribute property count regressed: " + tableOrderPropertyCount);
        Assert.True(tableTypePropertyCount >= 102, "generated table type attribute property count regressed: " + tableTypePropertyCount);
        Assert.True(presentationEffectPropertyCount >= 131, "generated presentation effect attribute property count regressed: " + presentationEffectPropertyCount);
        Assert.True(presentationSpeedPropertyCount >= 231, "generated presentation speed attribute property count regressed: " + presentationSpeedPropertyCount);
        Assert.True(presentationActionPropertyCount >= 125, "generated presentation action attribute property count regressed: " + presentationActionPropertyCount);
        Assert.True(presentationTransitionTypePropertyCount >= 99, "generated presentation transition type attribute property count regressed: " + presentationTransitionTypePropertyCount);
        Assert.True(presentationTransitionStylePropertyCount >= 99, "generated presentation transition style attribute property count regressed: " + presentationTransitionStylePropertyCount);
        Assert.True(foTextTransformPropertyCount >= 111, "generated FO text transform attribute property count regressed: " + foTextTransformPropertyCount);
        Assert.True(foTextAlignPropertyCount >= 106, "generated FO text align attribute property count regressed: " + foTextAlignPropertyCount);
        Assert.True(styleTextRotationScalePropertyCount >= 111, "generated style text rotation scale attribute property count regressed: " + styleTextRotationScalePropertyCount);
        Assert.True(styleTextCombinePropertyCount >= 111, "generated style text combine attribute property count regressed: " + styleTextCombinePropertyCount);
        Assert.True(drawFillPropertyCount >= 109, "generated draw fill attribute property count regressed: " + drawFillPropertyCount);
        Assert.True(drawFillImageRefPointPropertyCount >= 109, "generated draw fill image ref point attribute property count regressed: " + drawFillImageRefPointPropertyCount);
        Assert.True(drawColorModePropertyCount >= 99, "generated draw color mode attribute property count regressed: " + drawColorModePropertyCount);
        Assert.True(styleVerticalAlignPropertyCount >= 105, "generated style vertical align attribute property count regressed: " + styleVerticalAlignPropertyCount);
        Assert.True(styleVerticalPosPropertyCount >= 106, "generated style vertical pos attribute property count regressed: " + styleVerticalPosPropertyCount);
        Assert.True(styleVerticalRelPropertyCount >= 106, "generated style vertical rel attribute property count regressed: " + styleVerticalRelPropertyCount);
        Assert.True(styleHorizontalPosPropertyCount >= 99, "generated style horizontal pos attribute property count regressed: " + styleHorizontalPosPropertyCount);
        Assert.True(styleHorizontalRelPropertyCount >= 99, "generated style horizontal rel attribute property count regressed: " + styleHorizontalRelPropertyCount);
        Assert.True(styleWrapPropertyCount >= 99, "generated style wrap attribute property count regressed: " + styleWrapPropertyCount);
        Assert.True(styleRunThroughPropertyCount >= 99, "generated style run-through attribute property count regressed: " + styleRunThroughPropertyCount);
        Assert.True(styleWrapContourModePropertyCount >= 99, "generated style wrap contour mode attribute property count regressed: " + styleWrapContourModePropertyCount);
        Assert.True(styleWritingModePropertyCount >= 104, "generated style writing mode attribute property count regressed: " + styleWritingModePropertyCount);
        Assert.True(tableDisplayMemberModePropertyCount >= 103, "generated table display member mode attribute property count regressed: " + tableDisplayMemberModePropertyCount);
        Assert.True(tableLayoutModePropertyCount >= 103, "generated table layout mode attribute property count regressed: " + tableLayoutModePropertyCount);
        Assert.True(tableMemberTypePropertyCount >= 102, "generated table member type attribute property count regressed: " + tableMemberTypePropertyCount);
        Assert.True(tableGroupedByPropertyCount >= 102, "generated table grouped-by attribute property count regressed: " + tableGroupedByPropertyCount);
        Assert.True(tableSortModePropertyCount >= 103, "generated table sort mode attribute property count regressed: " + tableSortModePropertyCount);
        Assert.True(tableConditionSourcePropertyCount >= 103, "generated table condition source attribute property count regressed: " + tableConditionSourcePropertyCount);
        Assert.True(tableFunctionPropertyCount >= 109, "generated table function attribute property count regressed: " + tableFunctionPropertyCount);
        Assert.True(databaseRulePropertyCount >= 206, "generated database rule attribute property count regressed: " + databaseRulePropertyCount);
        Assert.True(databaseIsNullablePropertyCount >= 103, "generated database is-nullable attribute property count regressed: " + databaseIsNullablePropertyCount);
        Assert.True(databaseDataSourceSettingTypePropertyCount >= 102, "generated database data source setting type attribute property count regressed: " + databaseDataSourceSettingTypePropertyCount);
        Assert.True(animationColorInterpolationPropertyCount >= 102, "generated animation color interpolation attribute property count regressed: " + animationColorInterpolationPropertyCount);
        Assert.True(animationColorInterpolationDirectionPropertyCount >= 102, "generated animation color interpolation direction attribute property count regressed: " + animationColorInterpolationDirectionPropertyCount);
        Assert.True(drawNoHrefPropertyCount >= 102, "generated draw nohref attribute property count regressed: " + drawNoHrefPropertyCount);
        Assert.True(presentationPresetClassPropertyCount >= 103, "generated presentation preset class attribute property count regressed: " + presentationPresetClassPropertyCount);
        Assert.True(numberTransliterationStylePropertyCount >= 105, "generated number transliteration style attribute property count regressed: " + numberTransliterationStylePropertyCount);
        Assert.True(styleScriptTypePropertyCount >= 111, "generated style script type attribute property count regressed: " + styleScriptTypePropertyCount);
        Assert.True(styleTextEmphasizePropertyCount >= 111, "generated style text emphasize attribute property count regressed: " + styleTextEmphasizePropertyCount);
        Assert.True(drawStrokeLineJoinPropertyCount >= 99, "generated draw stroke linejoin attribute property count regressed: " + drawStrokeLineJoinPropertyCount);
        Assert.True(svgStrokeLineCapPropertyCount >= 99, "generated SVG stroke linecap attribute property count regressed: " + svgStrokeLineCapPropertyCount);
        Assert.True(foKeepTogetherPropertyCount >= 99, "generated FO keep-together attribute property count regressed: " + foKeepTogetherPropertyCount);
        Assert.True(foWrapOptionPropertyCount >= 100, "generated FO wrap option attribute property count regressed: " + foWrapOptionPropertyCount);
        Assert.True(dr3dProjectionPropertyCount >= 100, "generated 3D projection attribute property count regressed: " + dr3dProjectionPropertyCount);
        Assert.True(dr3dShadeModePropertyCount >= 100, "generated 3D shade mode attribute property count regressed: " + dr3dShadeModePropertyCount);
        Assert.True(svgFillRulePropertyCount >= 109, "generated SVG fill rule attribute property count regressed: " + svgFillRulePropertyCount);
        Assert.True(tableBorderModelPropertyCount >= 99, "generated table border model attribute property count regressed: " + tableBorderModelPropertyCount);
        Assert.True(textLabelFollowedByPropertyCount >= 107, "generated text label-followed-by attribute property count regressed: " + textLabelFollowedByPropertyCount);
        Assert.True(textListLevelPositionModePropertyCount >= 106, "generated text list level position mode attribute property count regressed: " + textListLevelPositionModePropertyCount);
        Assert.True(textIndexScopePropertyCount >= 104, "generated text index scope attribute property count regressed: " + textIndexScopePropertyCount);
        Assert.True(textTableTypePropertyCount >= 103, "generated text table type attribute property count regressed: " + textTableTypePropertyCount);
        Assert.True(textAnchorTypePropertyCount >= 102, "generated text anchor type attribute property count regressed: " + textAnchorTypePropertyCount);
        Assert.True(textNoteClassPropertyCount >= 101, "generated text note class attribute property count regressed: " + textNoteClassPropertyCount);
        Assert.True(textSelectPagePropertyCount >= 100, "generated text select page attribute property count regressed: " + textSelectPagePropertyCount);
        Assert.True(textReferenceFormatPropertyCount >= 100, "generated text reference format attribute property count regressed: " + textReferenceFormatPropertyCount);
        Assert.True(textStartNumberingAtPropertyCount >= 100, "generated text start numbering at attribute property count regressed: " + textStartNumberingAtPropertyCount);
        Assert.True(textFootnotesPositionPropertyCount >= 100, "generated text footnotes position attribute property count regressed: " + textFootnotesPositionPropertyCount);
        Assert.True(textCaptionSequenceFormatPropertyCount >= 100, "generated text caption sequence format attribute property count regressed: " + textCaptionSequenceFormatPropertyCount);
        Assert.True(textNumberPositionPropertyCount >= 99, "generated text number position attribute property count regressed: " + textNumberPositionPropertyCount);
        Assert.True(textPlaceholderTypePropertyCount >= 99, "generated text placeholder type attribute property count regressed: " + textPlaceholderTypePropertyCount);
        Assert.True(textAnimationPropertyCount >= 99, "generated text animation attribute property count regressed: " + textAnimationPropertyCount);
        Assert.True(textAnimationDirectionPropertyCount >= 99, "generated text animation direction attribute property count regressed: " + textAnimationDirectionPropertyCount);
        Assert.True(textKindPropertyCount >= 99, "generated text kind attribute property count regressed: " + textKindPropertyCount);
        Assert.True(lineStylePropertyCount >= 534, "generated line style attribute property count regressed: " + lineStylePropertyCount);
        Assert.True(lineTypePropertyCount >= 433, "generated line type attribute property count regressed: " + lineTypePropertyCount);
        Assert.True(lineWidthPropertyCount >= 433, "generated line width attribute property count regressed: " + lineWidthPropertyCount);
        Assert.True(lineModePropertyCount >= 333, "generated line mode attribute property count regressed: " + lineModePropertyCount);
        Assert.True(fontStylePropertyCount >= 433, "generated font style attribute property count regressed: " + fontStylePropertyCount);
        Assert.True(fontVariantPropertyCount >= 211, "generated font variant attribute property count regressed: " + fontVariantPropertyCount);
        Assert.True(fontWeightPropertyCount >= 433, "generated font weight attribute property count regressed: " + fontWeightPropertyCount);
        Assert.True(fontFamilyGenericPropertyCount >= 335, "generated font family generic attribute property count regressed: " + fontFamilyGenericPropertyCount);
        Assert.True(fontPitchPropertyCount >= 335, "generated font pitch attribute property count regressed: " + fontPitchPropertyCount);
        Assert.True(fontReliefPropertyCount >= 111, "generated font relief attribute property count regressed: " + fontReliefPropertyCount);
        Assert.True(fontStretchPropertyCount >= 100, "generated font stretch attribute property count regressed: " + fontStretchPropertyCount);
        Assert.True(styleLineBreakPropertyCount >= 98, "generated style line break attribute property count regressed: " + styleLineBreakPropertyCount);
        Assert.True(styleRepeatPropertyCount >= 111, "generated style repeat attribute property count regressed: " + styleRepeatPropertyCount);
        Assert.True(styleDirectionPropertyCount >= 99, "generated style direction attribute property count regressed: " + styleDirectionPropertyCount);
        Assert.True(formOrientationPropertyCount >= 99, "generated form orientation attribute property count regressed: " + formOrientationPropertyCount);
        Assert.True(tableDirectionPropertyCount >= 100, "generated table direction attribute property count regressed: " + tableDirectionPropertyCount);
        Assert.True(tableOrientationPropertyCount >= 104, "generated table orientation attribute property count regressed: " + tableOrientationPropertyCount);
        Assert.True(xmlNamePropertyCount >= 1000, "generated XML name attribute property count regressed: " + xmlNamePropertyCount);
        Assert.True(styleFamilyPropertyCount >= 50, "generated style family attribute property count regressed: " + styleFamilyPropertyCount);
        Assert.True(odfVersionPropertyCount >= 50, "generated ODF version attribute property count regressed: " + odfVersionPropertyCount);
        Assert.True(mediaTypePropertyCount >= 100, "generated media type attribute property count regressed: " + mediaTypePropertyCount);
    }

    /// <summary>
    /// 驗證 typed DOM coverage report 可輸出 schema-to-wrapper 對照。
    /// </summary>
    [Fact]
    public void TypedDomCoverageReportDeclaresSchemaWrapperMappings()
    {
        OdfTypedDomCoverageReport report = OdfTypedDomCoverage.Build();

        Assert.True(report.SchemaElementCount >= 550, "schema element count too low: " + report.SchemaElementCount);
        Assert.True(report.TypedElementCount >= 550, "typed element count too low: " + report.TypedElementCount);
        Assert.True(report.SchemaChildElementRelationCount >= 2000, "schema child element relation count too low: " + report.SchemaChildElementRelationCount);
        Assert.True(report.SchemaAttributeCount >= 100, "schema attribute count too low: " + report.SchemaAttributeCount);
        Assert.Contains(
            report.Elements,
            element => element.NamespaceUri == OdfNamespaces.Text &&
                element.LocalName == "p" &&
                element.HasTypedWrapper &&
                element.WrapperType.Contains("TextPElement", StringComparison.Ordinal));
        Assert.Contains(
            report.ChildElementRelations,
            relation => relation.ParentNamespaceUri == OdfNamespaces.Office &&
                relation.ParentLocalName == "body" &&
                relation.ChildNamespaceUri == OdfNamespaces.Office &&
                relation.ChildLocalName == "text");
        Assert.Contains(
            report.ChildElementRelations,
            relation => relation.ParentNamespaceUri == OdfNamespaces.Table &&
                relation.ParentLocalName == "table" &&
                relation.ChildNamespaceUri == OdfNamespaces.Table &&
                relation.ChildLocalName == "table-row");
        Assert.Contains(report.AttributeValueTypeCounts, pair => pair.Key.Length > 0 && pair.Value > 0);
        Assert.True(report.WrapperPropertyTypeCounts["int"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["childElementCollection"] >= 2000);
        Assert.True(report.WrapperPropertyTypeCounts["bool"] >= 10000);
        Assert.True(report.WrapperPropertyTypeCounts["decimal"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["dateTime"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["time"] >= 6);
        Assert.True(report.WrapperPropertyTypeCounts["length"] >= 10000);
        Assert.True(report.WrapperPropertyTypeCounts["borderWidths"] >= 723);
        Assert.True(report.WrapperPropertyTypeCounts["duration"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["angle"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["styleName"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["styleNameList"] >= 300);
        Assert.True(report.WrapperPropertyTypeCounts["color"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["iriReference"] >= 400);
        Assert.True(report.WrapperPropertyTypeCounts["percent"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["cellAddress"] >= 400);
        Assert.True(report.WrapperPropertyTypeCounts["cellRangeAddress"] >= 400);
        Assert.True(report.WrapperPropertyTypeCounts["cellRangeAddressList"] >= 800);
        Assert.True(report.WrapperPropertyTypeCounts["vector3D"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["point3D"] >= 90);
        Assert.True(report.WrapperPropertyTypeCounts["pointList"] >= 90);
        Assert.True(report.WrapperPropertyTypeCounts["languageCode"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["countryCode"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["scriptCode"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["languageTag"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["namespacedToken"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["character"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["textEncoding"] >= 438);
        Assert.True(report.WrapperPropertyTypeCounts["targetFrameName"] >= 205);
        Assert.True(report.WrapperPropertyTypeCounts["xLinkType"] >= 172);
        Assert.True(report.WrapperPropertyTypeCounts["xLinkShow"] >= 160);
        Assert.True(report.WrapperPropertyTypeCounts["xLinkActuate"] >= 167);
        Assert.True(report.WrapperPropertyTypeCounts["numberStyle"] >= 109);
        Assert.True(report.WrapperPropertyTypeCounts["numberCalendar"] >= 106);
        Assert.True(report.WrapperPropertyTypeCounts["tableOrder"] >= 108);
        Assert.True(report.WrapperPropertyTypeCounts["tableType"] >= 102);
        Assert.True(report.WrapperPropertyTypeCounts["presentationEffect"] >= 131);
        Assert.True(report.WrapperPropertyTypeCounts["presentationSpeed"] >= 231);
        Assert.True(report.WrapperPropertyTypeCounts["presentationAction"] >= 125);
        Assert.True(report.WrapperPropertyTypeCounts["presentationTransitionType"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["presentationTransitionStyle"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["foTextTransform"] >= 111);
        Assert.True(report.WrapperPropertyTypeCounts["foTextAlign"] >= 106);
        Assert.True(report.WrapperPropertyTypeCounts["styleTextRotationScale"] >= 111);
        Assert.True(report.WrapperPropertyTypeCounts["styleTextCombine"] >= 111);
        Assert.True(report.WrapperPropertyTypeCounts["drawFill"] >= 109);
        Assert.True(report.WrapperPropertyTypeCounts["drawFillImageRefPoint"] >= 109);
        Assert.True(report.WrapperPropertyTypeCounts["drawColorMode"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["styleVerticalAlign"] >= 105);
        Assert.True(report.WrapperPropertyTypeCounts["styleVerticalPos"] >= 106);
        Assert.True(report.WrapperPropertyTypeCounts["styleVerticalRel"] >= 106);
        Assert.True(report.WrapperPropertyTypeCounts["styleHorizontalPos"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["styleHorizontalRel"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["styleWrap"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["styleRunThrough"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["styleWrapContourMode"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["styleWritingMode"] >= 104);
        Assert.True(report.WrapperPropertyTypeCounts["tableDisplayMemberMode"] >= 103);
        Assert.True(report.WrapperPropertyTypeCounts["tableLayoutMode"] >= 103);
        Assert.True(report.WrapperPropertyTypeCounts["tableMemberType"] >= 102);
        Assert.True(report.WrapperPropertyTypeCounts["tableGroupedBy"] >= 102);
        Assert.True(report.WrapperPropertyTypeCounts["tableSortMode"] >= 103);
        Assert.True(report.WrapperPropertyTypeCounts["tableConditionSource"] >= 103);
        Assert.True(report.WrapperPropertyTypeCounts["tableFunction"] >= 109);
        Assert.True(report.WrapperPropertyTypeCounts["databaseRule"] >= 206);
        Assert.True(report.WrapperPropertyTypeCounts["databaseIsNullable"] >= 103);
        Assert.True(report.WrapperPropertyTypeCounts["databaseDataSourceSettingType"] >= 102);
        Assert.True(report.WrapperPropertyTypeCounts["animationColorInterpolation"] >= 102);
        Assert.True(report.WrapperPropertyTypeCounts["animationColorInterpolationDirection"] >= 102);
        Assert.True(report.WrapperPropertyTypeCounts["drawNoHref"] >= 102);
        Assert.True(report.WrapperPropertyTypeCounts["presentationPresetClass"] >= 103);
        Assert.True(report.WrapperPropertyTypeCounts["numberTransliterationStyle"] >= 105);
        Assert.True(report.WrapperPropertyTypeCounts["styleScriptType"] >= 111);
        Assert.True(report.WrapperPropertyTypeCounts["styleTextEmphasize"] >= 111);
        Assert.True(report.WrapperPropertyTypeCounts["drawStrokeLineJoin"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["svgStrokeLineCap"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["foKeepTogether"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["foWrapOption"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["dr3dProjection"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["dr3dShadeMode"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["svgFillRule"] >= 109);
        Assert.True(report.WrapperPropertyTypeCounts["tableBorderModel"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["textLabelFollowedBy"] >= 107);
        Assert.True(report.WrapperPropertyTypeCounts["textListLevelPositionMode"] >= 106);
        Assert.True(report.WrapperPropertyTypeCounts["textIndexScope"] >= 104);
        Assert.True(report.WrapperPropertyTypeCounts["textTableType"] >= 103);
        Assert.True(report.WrapperPropertyTypeCounts["textAnchorType"] >= 102);
        Assert.True(report.WrapperPropertyTypeCounts["textNoteClass"] >= 101);
        Assert.True(report.WrapperPropertyTypeCounts["textSelectPage"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["textReferenceFormat"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["textStartNumberingAt"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["textFootnotesPosition"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["textCaptionSequenceFormat"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["textNumberPosition"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["textPlaceholderType"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["textAnimation"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["textAnimationDirection"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["textKind"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["lineStyle"] >= 534);
        Assert.True(report.WrapperPropertyTypeCounts["lineType"] >= 433);
        Assert.True(report.WrapperPropertyTypeCounts["lineWidth"] >= 433);
        Assert.True(report.WrapperPropertyTypeCounts["lineMode"] >= 333);
        Assert.True(report.WrapperPropertyTypeCounts["fontStyle"] >= 433);
        Assert.True(report.WrapperPropertyTypeCounts["fontVariant"] >= 211);
        Assert.True(report.WrapperPropertyTypeCounts["fontWeight"] >= 433);
        Assert.True(report.WrapperPropertyTypeCounts["fontFamilyGeneric"] >= 335);
        Assert.True(report.WrapperPropertyTypeCounts["fontPitch"] >= 335);
        Assert.True(report.WrapperPropertyTypeCounts["fontRelief"] >= 111);
        Assert.True(report.WrapperPropertyTypeCounts["fontStretch"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["styleLineBreak"] >= 98);
        Assert.True(report.WrapperPropertyTypeCounts["styleRepeat"] >= 111);
        Assert.True(report.WrapperPropertyTypeCounts["styleDirection"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["formOrientation"] >= 99);
        Assert.True(report.WrapperPropertyTypeCounts["tableDirection"] >= 100);
        Assert.True(report.WrapperPropertyTypeCounts["tableOrientation"] >= 104);
        Assert.True(report.WrapperPropertyTypeCounts["xmlName"] >= 1000);
        Assert.True(report.WrapperPropertyTypeCounts["styleFamily"] >= 50);
        Assert.True(report.WrapperPropertyTypeCounts["odfVersion"] >= 50);
        Assert.True(report.WrapperPropertyTypeCounts["mediaType"] >= 100);

        string json = JsonSerializer.Serialize(report.ToJsonModel(), OdfJsonSerializerOptions.Compact);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(report.SchemaElementCount, document.RootElement.GetProperty("summary").GetProperty("schemaElementCount").GetInt32());
        Assert.Equal(report.SchemaChildElementRelationCount, document.RootElement.GetProperty("summary").GetProperty("schemaChildElementRelationCount").GetInt32());
        Assert.True(document.RootElement.GetProperty("childElementRelations").GetArrayLength() >= 2000);
        Assert.Contains(
            document.RootElement.GetProperty("childElementRelations").EnumerateArray(),
            relation => relation.GetProperty("parentNamespaceUri").GetString() == OdfNamespaces.Office &&
                relation.GetProperty("parentLocalName").GetString() == "body" &&
                relation.GetProperty("childNamespaceUri").GetString() == OdfNamespaces.Office &&
                relation.GetProperty("childLocalName").GetString() == "text");
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("childElementCollection").GetInt32() >= 2000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("int").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("bool").GetInt32() >= 10000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("time").GetInt32() >= 6);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("length").GetInt32() >= 10000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("borderWidths").GetInt32() >= 723);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("duration").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("angle").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleName").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleNameList").GetInt32() >= 300);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("color").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("iriReference").GetInt32() >= 400);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("percent").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("cellAddress").GetInt32() >= 400);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("cellRangeAddress").GetInt32() >= 400);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("cellRangeAddressList").GetInt32() >= 800);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("vector3D").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("point3D").GetInt32() >= 90);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("pointList").GetInt32() >= 90);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("languageCode").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("countryCode").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("scriptCode").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("languageTag").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("namespacedToken").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("character").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textEncoding").GetInt32() >= 438);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("targetFrameName").GetInt32() >= 205);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("xLinkType").GetInt32() >= 172);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("xLinkShow").GetInt32() >= 160);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("xLinkActuate").GetInt32() >= 167);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("numberStyle").GetInt32() >= 109);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("numberCalendar").GetInt32() >= 106);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableOrder").GetInt32() >= 108);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableType").GetInt32() >= 102);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationEffect").GetInt32() >= 131);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationSpeed").GetInt32() >= 231);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationAction").GetInt32() >= 125);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationTransitionType").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationTransitionStyle").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("foTextTransform").GetInt32() >= 111);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("foTextAlign").GetInt32() >= 106);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleTextRotationScale").GetInt32() >= 111);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleTextCombine").GetInt32() >= 111);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("drawFill").GetInt32() >= 109);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("drawFillImageRefPoint").GetInt32() >= 109);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("drawColorMode").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleVerticalAlign").GetInt32() >= 105);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleVerticalPos").GetInt32() >= 106);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleVerticalRel").GetInt32() >= 106);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleHorizontalPos").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleHorizontalRel").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleWrap").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleRunThrough").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleWrapContourMode").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleWritingMode").GetInt32() >= 104);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableDisplayMemberMode").GetInt32() >= 103);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableLayoutMode").GetInt32() >= 103);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableMemberType").GetInt32() >= 102);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableGroupedBy").GetInt32() >= 102);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableSortMode").GetInt32() >= 103);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableConditionSource").GetInt32() >= 103);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableFunction").GetInt32() >= 109);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("databaseRule").GetInt32() >= 206);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("databaseIsNullable").GetInt32() >= 103);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("databaseDataSourceSettingType").GetInt32() >= 102);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("animationColorInterpolation").GetInt32() >= 102);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("animationColorInterpolationDirection").GetInt32() >= 102);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("drawNoHref").GetInt32() >= 102);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("presentationPresetClass").GetInt32() >= 103);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("numberTransliterationStyle").GetInt32() >= 105);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleScriptType").GetInt32() >= 111);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleTextEmphasize").GetInt32() >= 111);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("drawStrokeLineJoin").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("svgStrokeLineCap").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("foKeepTogether").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("foWrapOption").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("dr3dProjection").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("dr3dShadeMode").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("svgFillRule").GetInt32() >= 109);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableBorderModel").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textLabelFollowedBy").GetInt32() >= 107);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textListLevelPositionMode").GetInt32() >= 106);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textIndexScope").GetInt32() >= 104);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textTableType").GetInt32() >= 103);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textAnchorType").GetInt32() >= 102);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textNoteClass").GetInt32() >= 101);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textSelectPage").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textReferenceFormat").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textStartNumberingAt").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textFootnotesPosition").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textCaptionSequenceFormat").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textNumberPosition").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textPlaceholderType").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textAnimation").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textAnimationDirection").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("textKind").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("lineStyle").GetInt32() >= 534);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("lineType").GetInt32() >= 433);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("lineWidth").GetInt32() >= 433);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("lineMode").GetInt32() >= 333);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontStyle").GetInt32() >= 433);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontVariant").GetInt32() >= 211);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontWeight").GetInt32() >= 433);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontFamilyGeneric").GetInt32() >= 335);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontPitch").GetInt32() >= 335);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontRelief").GetInt32() >= 111);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("fontStretch").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleLineBreak").GetInt32() >= 98);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleRepeat").GetInt32() >= 111);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleDirection").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("formOrientation").GetInt32() >= 99);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableDirection").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("tableOrientation").GetInt32() >= 104);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("xmlName").GetInt32() >= 1000);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("styleFamily").GetInt32() >= 50);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("odfVersion").GetInt32() >= 50);
        Assert.True(document.RootElement.GetProperty("wrapperPropertyTypeCounts").GetProperty("mediaType").GetInt32() >= 100);
        Assert.True(document.RootElement.GetProperty("elements").GetArrayLength() >= report.SchemaElementCount);
    }

    /// <summary>
    /// 驗證 typed DOM 屬性 helper 可用強型別讀寫常用 ODF datatype。
    /// </summary>
    [Fact]
    public void TypedDomAttributeHelpersReadAndWriteCommonDatatypes()
    {
        TableTableCellElement cell = new("table");
        const string formNamespace = "urn:oasis:names:tc:opendocument:xmlns:form:1.0";
        const string dr3dNamespace = "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0";
        const string smilNamespace = "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0";
        const string databaseNamespace = "urn:oasis:names:tc:opendocument:xmlns:database:1.0";
        const string animationNamespace = "urn:oasis:names:tc:opendocument:xmlns:animation:1.0";
        DateTime utc = new(2026, 6, 13, 9, 30, 0, DateTimeKind.Utc);
        DateTime local = new(2026, 6, 13, 17, 30, 0, DateTimeKind.Unspecified);

        cell.NumberColumnsRepeated = 3;
        cell.SetDecimalAttributeValue("value", OdfNamespaces.Office, 12.50m, OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        cell.SetBooleanAttributeValue("boolean-value", OdfNamespaces.Office, true, OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        cell.SetDateTimeAttributeValue("date-value", OdfNamespaces.Office, utc, OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        cell.SetTimeAttributeValue("time-value", OdfNamespaces.Office, new OdfTime(new TimeSpan(12, 30, 45), TimeSpan.Zero), OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        cell.SetLengthAttributeValue("width", OdfNamespaces.Style, OdfLength.FromCentimeters(2.5), OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetBorderWidthsAttributeValue("border-line-width", OdfNamespaces.Style, new OdfBorderWidths(OdfLength.FromPoints(0.5), OdfLength.FromPoints(1), OdfLength.FromPoints(0.5)), OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetDurationAttributeValue("duration", OdfNamespaces.Presentation, new OdfDuration("PT1H30M"), OdfNamespaces.GetPrefix(OdfNamespaces.Presentation));
        cell.SetAngleAttributeValue("rotation-angle", OdfNamespaces.Style, OdfAngle.FromDegrees(45.5m), OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleNameAttributeValue("style-name", OdfNamespaces.Table, new OdfStyleName("CellStyle1"), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetStyleNameListAttributeValue("style-names", OdfNamespaces.Table, new OdfStyleNameList([new OdfStyleName("CellStyle1"), new OdfStyleName("Accent2")]), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetColorAttributeValue("fill-color", OdfNamespaces.Draw, OdfColor.FromRgb(255, 204, 0), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetIriReferenceAttributeValue("href", OdfNamespaces.XLink, new OdfIriReference("../Pictures/logo.svg#main"), OdfNamespaces.GetPrefix(OdfNamespaces.XLink));
        cell.SetPercentAttributeValue("opacity", OdfNamespaces.Draw, new OdfPercent("87.5%"), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetSignedPercentAttributeValue("shadow-offset", OdfNamespaces.Draw, new OdfPercent("-12.5%"), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetCellAddressAttributeValue("base-cell-address", OdfNamespaces.Table, new OdfCellAddressReference("'My Sheet'.$A$1"), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetCellRangeAddressAttributeValue("cell-range-address", OdfNamespaces.Table, new OdfCellRangeAddress("'My Sheet'.$A$1:'My Sheet'.$C$3"), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetCellRangeAddressListAttributeValue(
            "cell-range-address-list",
            OdfNamespaces.Table,
            new OdfCellRangeAddressList("'My Sheet'.$A$1:'My Sheet'.$C$3 .D4:.E5"),
            OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetVector3DAttributeValue("extrusion-direction", OdfNamespaces.Draw, new OdfVector3D(1m, 0m, -0.5m), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetPoint3DAttributeValue(
            "extrusion-viewpoint",
            OdfNamespaces.Draw,
            new OdfPoint3D(OdfLength.FromCentimeters(1), OdfLength.FromMillimeters(0), OdfLength.FromInches(-0.5)),
            OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetPointListAttributeValue("points", OdfNamespaces.Draw, new OdfPointList([new OdfPoint2D(0, 0), new OdfPoint2D(10, -20)]), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetXmlNameAttributeValue("shape-id", OdfNamespaces.Draw, new OdfXmlName("Shape1"), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetLanguageCodeAttributeValue("language", OdfNamespaces.Fo, new OdfLanguageCode("zh"), OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetCountryCodeAttributeValue("country", OdfNamespaces.Fo, new OdfCountryCode("TW"), OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetScriptCodeAttributeValue("script", OdfNamespaces.Fo, new OdfScriptCode("Hant"), OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetLanguageTagAttributeValue("rfc-language-tag", OdfNamespaces.Table, new OdfLanguageTag("zh-Hant-TW"), OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetNamespacedTokenAttributeValue("type-name", OdfNamespaces.Draw, new OdfNamespacedToken("draw:shape"), OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetCharacterAttributeValue("decimal-replacement", OdfNamespaces.Number, new OdfCharacter("*"), OdfNamespaces.GetPrefix(OdfNamespaces.Number));
        cell.SetTextEncodingAttributeValue("encoding", OdfNamespaces.Text, new OdfTextEncoding("UTF-8"), OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTargetFrameNameAttributeValue("target-frame-name", OdfNamespaces.Office, new OdfTargetFrameName("_blank"), OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        cell.SetXLinkTypeAttributeValue("type", OdfNamespaces.XLink, OdfXLinkType.Simple, OdfNamespaces.GetPrefix(OdfNamespaces.XLink));
        cell.SetXLinkShowAttributeValue("show", OdfNamespaces.XLink, OdfXLinkShow.Embed, OdfNamespaces.GetPrefix(OdfNamespaces.XLink));
        cell.SetXLinkActuateAttributeValue("actuate", OdfNamespaces.XLink, OdfXLinkActuate.OnLoad, OdfNamespaces.GetPrefix(OdfNamespaces.XLink));
        cell.SetNumberStyleAttributeValue("style", OdfNamespaces.Number, OdfNumberStyle.Long, OdfNamespaces.GetPrefix(OdfNamespaces.Number));
        cell.SetNumberCalendarAttributeValue("calendar", OdfNamespaces.Number, OdfNumberCalendar.HanjaYoil, OdfNamespaces.GetPrefix(OdfNamespaces.Number));
        cell.SetTableOrderAttributeValue("order", OdfNamespaces.Table, OdfTableOrder.Descending, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetTableTypeAttributeValue("type", OdfNamespaces.Table, OdfTableType.RunningTotal, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetPresentationEffectAttributeValue("effect", OdfNamespaces.Presentation, OdfPresentationEffect.MoveShort, OdfNamespaces.GetPrefix(OdfNamespaces.Presentation));
        cell.SetPresentationSpeedAttributeValue("speed", OdfNamespaces.Presentation, OdfPresentationSpeed.Fast, OdfNamespaces.GetPrefix(OdfNamespaces.Presentation));
        cell.SetPresentationActionAttributeValue("action", OdfNamespaces.Presentation, OdfPresentationAction.LastVisitedPage, OdfNamespaces.GetPrefix(OdfNamespaces.Presentation));
        cell.SetPresentationTransitionTypeAttributeValue("transition-type", OdfNamespaces.Presentation, OdfPresentationTransitionType.SemiAutomatic, OdfNamespaces.GetPrefix(OdfNamespaces.Presentation));
        cell.SetPresentationTransitionStyleAttributeValue("transition-style", OdfNamespaces.Presentation, OdfPresentationTransitionStyle.InterlockingHorizontalRight, OdfNamespaces.GetPrefix(OdfNamespaces.Presentation));
        cell.SetFoTextTransformAttributeValue("text-transform", OdfNamespaces.Fo, OdfFoTextTransform.Uppercase, OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetFoTextAlignAttributeValue("text-align", OdfNamespaces.Fo, OdfFoTextAlign.Justify, OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetStyleTextRotationScaleAttributeValue("text-rotation-scale", OdfNamespaces.Style, OdfStyleTextRotationScale.LineHeight, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleTextCombineAttributeValue("text-combine", OdfNamespaces.Style, OdfStyleTextCombine.Letters, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetDrawFillAttributeValue("fill", OdfNamespaces.Draw, OdfDrawFill.Gradient, OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetSmilFillAttributeValue("fill", smilNamespace, OdfSmilFill.Freeze, "smil");
        cell.SetDrawFillImageRefPointAttributeValue("fill-image-ref-point", OdfNamespaces.Draw, OdfDrawFillImageRefPoint.BottomRight, OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetDrawColorModeAttributeValue("color-mode", OdfNamespaces.Draw, OdfDrawColorMode.Greyscale, OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetStyleVerticalAlignAttributeValue("vertical-align", OdfNamespaces.Style, OdfStyleVerticalAlign.Middle, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleVerticalPosAttributeValue("vertical-pos", OdfNamespaces.Style, OdfStyleVerticalPos.FromTop, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleVerticalRelAttributeValue("vertical-rel", OdfNamespaces.Style, OdfStyleVerticalRel.PageContentBottom, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleHorizontalPosAttributeValue("horizontal-pos", OdfNamespaces.Style, OdfStyleHorizontalPos.FromInside, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleHorizontalRelAttributeValue("horizontal-rel", OdfNamespaces.Style, OdfStyleHorizontalRel.ParagraphStartMargin, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleWrapAttributeValue("wrap", OdfNamespaces.Style, OdfStyleWrap.RunThrough, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleRunThroughAttributeValue("run-through", OdfNamespaces.Style, OdfStyleRunThrough.Foreground, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleWrapContourModeAttributeValue("wrap-contour-mode", OdfNamespaces.Style, OdfStyleWrapContourMode.Outside, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleWritingModeAttributeValue("writing-mode", OdfNamespaces.Style, OdfStyleWritingMode.SidewaysRl, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetTableDisplayMemberModeAttributeValue("display-member-mode", OdfNamespaces.Table, OdfTableDisplayMemberMode.FromBottom, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetTableLayoutModeAttributeValue("layout-mode", OdfNamespaces.Table, OdfTableLayoutMode.TabularLayout, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetTableMemberTypeAttributeValue("member-type", OdfNamespaces.Table, OdfTableMemberType.Previous, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetTableGroupedByAttributeValue("grouped-by", OdfNamespaces.Table, OdfTableGroupedBy.Quarters, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetTableSortModeAttributeValue("sort-mode", OdfNamespaces.Table, OdfTableSortMode.Manual, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetTableConditionSourceAttributeValue("condition-source", OdfNamespaces.Table, OdfTableConditionSource.CellRange, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetTableFunctionAttributeValue("function", OdfNamespaces.Table, OdfTableFunction.Stdevp, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetDatabaseRuleAttributeValue("delete-rule", databaseNamespace, OdfDatabaseRule.SetNull, "db");
        cell.SetDatabaseIsNullableAttributeValue("is-nullable", databaseNamespace, OdfDatabaseIsNullable.NoNulls, "db");
        cell.SetDatabaseDataSourceSettingTypeAttributeValue("data-source-setting-type", databaseNamespace, OdfDatabaseDataSourceSettingType.Boolean, "db");
        cell.SetAnimationColorInterpolationAttributeValue("color-interpolation", animationNamespace, OdfAnimationColorInterpolation.Hsl, "anim");
        cell.SetAnimationColorInterpolationDirectionAttributeValue("color-interpolation-direction", animationNamespace, OdfAnimationColorInterpolationDirection.CounterClockwise, "anim");
        cell.SetDrawNoHrefAttributeValue("nohref", OdfNamespaces.Draw, OdfDrawNoHref.Nohref, OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetPresentationPresetClassAttributeValue("preset-class", OdfNamespaces.Presentation, OdfPresentationPresetClass.MotionPath, OdfNamespaces.GetPrefix(OdfNamespaces.Presentation));
        cell.SetNumberTransliterationStyleAttributeValue("transliteration-style", OdfNamespaces.Number, OdfNumberTransliterationStyle.Medium, OdfNamespaces.GetPrefix(OdfNamespaces.Number));
        cell.SetStyleScriptTypeAttributeValue("script-type", OdfNamespaces.Style, OdfStyleScriptType.Complex, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleTextEmphasizeAttributeValue("text-emphasize", OdfNamespaces.Style, OdfStyleTextEmphasize.Circle, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetDrawStrokeLineJoinAttributeValue("stroke-linejoin", OdfNamespaces.Draw, OdfDrawStrokeLineJoin.Miter, OdfNamespaces.GetPrefix(OdfNamespaces.Draw));
        cell.SetSvgStrokeLineCapAttributeValue("stroke-linecap", OdfNamespaces.Svg, OdfSvgStrokeLineCap.Square, OdfNamespaces.GetPrefix(OdfNamespaces.Svg));
        cell.SetFoKeepTogetherAttributeValue("keep-together", OdfNamespaces.Fo, OdfFoKeepTogether.Always, OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetFoWrapOptionAttributeValue("wrap-option", OdfNamespaces.Fo, OdfFoWrapOption.NoWrap, OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetDr3dProjectionAttributeValue("projection", dr3dNamespace, OdfDr3dProjection.Perspective, "dr3d");
        cell.SetDr3dShadeModeAttributeValue("shade-mode", dr3dNamespace, OdfDr3dShadeMode.Phong, "dr3d");
        cell.SetSvgFillRuleAttributeValue("fill-rule", OdfNamespaces.Svg, OdfSvgFillRule.EvenOdd, OdfNamespaces.GetPrefix(OdfNamespaces.Svg));
        cell.SetTableBorderModelAttributeValue("border-model", OdfNamespaces.Table, OdfTableBorderModel.Collapsing, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetTextLabelFollowedByAttributeValue("label-followed-by", OdfNamespaces.Text, OdfTextLabelFollowedBy.ListTab, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextListLevelPositionModeAttributeValue("list-level-position-and-space-mode", OdfNamespaces.Text, OdfTextListLevelPositionMode.LabelAlignment, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextIndexScopeAttributeValue("index-scope", OdfNamespaces.Text, OdfTextIndexScope.Document, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextTableTypeAttributeValue("table-type", OdfNamespaces.Text, OdfTextTableType.Query, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextAnchorTypeAttributeValue("anchor-type", OdfNamespaces.Text, OdfTextAnchorType.AsChar, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextNoteClassAttributeValue("note-class", OdfNamespaces.Text, OdfTextNoteClass.Footnote, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextSelectPageAttributeValue("select-page", OdfNamespaces.Text, OdfTextSelectPage.Previous, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextReferenceFormatAttributeValue("reference-format", OdfNamespaces.Text, OdfTextReferenceFormat.NumberAllSuperior, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextStartNumberingAtAttributeValue("start-numbering-at", OdfNamespaces.Text, OdfTextStartNumberingAt.Page, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextFootnotesPositionAttributeValue("footnotes-position", OdfNamespaces.Text, OdfTextFootnotesPosition.Section, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextCaptionSequenceFormatAttributeValue("caption-sequence-format", OdfNamespaces.Text, OdfTextCaptionSequenceFormat.CategoryAndValue, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextNumberPositionAttributeValue("number-position", OdfNamespaces.Text, OdfTextNumberPosition.Outer, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextPlaceholderTypeAttributeValue("placeholder-type", OdfNamespaces.Text, OdfTextPlaceholderType.TextBox, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextAnimationAttributeValue("animation", OdfNamespaces.Text, OdfTextAnimation.Scroll, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextAnimationDirectionAttributeValue("animation-direction", OdfNamespaces.Text, OdfTextAnimationDirection.Up, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetTextKindAttributeValue("kind", OdfNamespaces.Text, OdfTextKind.Unit, OdfNamespaces.GetPrefix(OdfNamespaces.Text));
        cell.SetLineStyleAttributeValue("text-underline-style", OdfNamespaces.Style, OdfLineStyle.LongDash, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetLineTypeAttributeValue("text-underline-type", OdfNamespaces.Style, OdfLineType.Double, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetLineWidthAttributeValue("text-underline-width", OdfNamespaces.Style, new OdfLineWidth("150%"), OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetLineModeAttributeValue("text-underline-mode", OdfNamespaces.Style, OdfLineMode.SkipWhiteSpace, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetFontStyleAttributeValue("font-style", OdfNamespaces.Fo, OdfFontStyle.Italic, OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetFontVariantAttributeValue("font-variant", OdfNamespaces.Fo, OdfFontVariant.SmallCaps, OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetFontWeightAttributeValue("font-weight", OdfNamespaces.Fo, OdfFontWeight.Weight700, OdfNamespaces.GetPrefix(OdfNamespaces.Fo));
        cell.SetFontFamilyGenericAttributeValue("font-family-generic", OdfNamespaces.Style, OdfFontFamilyGeneric.Swiss, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetFontPitchAttributeValue("font-pitch", OdfNamespaces.Style, OdfFontPitch.Fixed, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetFontReliefAttributeValue("font-relief", OdfNamespaces.Style, OdfFontRelief.Embossed, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetFontStretchAttributeValue("font-stretch", OdfNamespaces.Svg, OdfFontStretch.SemiExpanded, OdfNamespaces.GetPrefix(OdfNamespaces.Svg));
        cell.SetStyleLineBreakAttributeValue("line-break", OdfNamespaces.Style, OdfStyleLineBreak.Strict, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleRepeatAttributeValue("repeat", OdfNamespaces.Style, OdfStyleRepeat.Stretch, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetStyleDirectionAttributeValue("direction", OdfNamespaces.Style, OdfStyleDirection.TopToBottom, OdfNamespaces.GetPrefix(OdfNamespaces.Style));
        cell.SetFormOrientationAttributeValue("orientation", formNamespace, OdfFormOrientation.Vertical, "form");
        cell.SetTableDirectionAttributeValue("direction", OdfNamespaces.Table, OdfTableDirection.FromSameTable, OdfNamespaces.GetPrefix(OdfNamespaces.Table));
        cell.SetTableOrientationAttributeValue("orientation", OdfNamespaces.Table, OdfTableOrientation.Page, OdfNamespaces.GetPrefix(OdfNamespaces.Table));

        Assert.Equal(3, cell.NumberColumnsRepeated);
        Assert.Equal(12.50m, cell.GetDecimalAttributeValue("value", OdfNamespaces.Office));
        Assert.True(cell.GetBooleanAttributeValue("boolean-value", OdfNamespaces.Office));
        Assert.Equal(utc, cell.GetDateTimeAttributeValue("date-value", OdfNamespaces.Office));
        Assert.Equal("2026-06-13T09:30:00Z", cell.GetAttribute("date-value", OdfNamespaces.Office));
        Assert.Equal(new OdfTime(new TimeSpan(12, 30, 45), TimeSpan.Zero), cell.GetTimeAttributeValue("time-value", OdfNamespaces.Office));
        Assert.Equal("12:30:45Z", cell.GetAttribute("time-value", OdfNamespaces.Office));
        Assert.Equal(OdfLength.FromCentimeters(2.5), cell.GetLengthAttributeValue("width", OdfNamespaces.Style));
        Assert.Equal("2.5cm", cell.GetAttribute("width", OdfNamespaces.Style));
        OdfBorderWidths? borderWidths = cell.GetBorderWidthsAttributeValue("border-line-width", OdfNamespaces.Style);
        Assert.Equal(new OdfBorderWidths("0.5pt 1pt 0.5pt"), borderWidths);
        Assert.Equal(OdfLength.FromPoints(1), borderWidths!.Value.Spacing);
        Assert.Equal(new OdfDuration("PT1H30M"), cell.GetDurationAttributeValue("duration", OdfNamespaces.Presentation));
        Assert.Equal("PT1H30M", cell.GetAttribute("duration", OdfNamespaces.Presentation));
        OdfAngle? angle = cell.GetAngleAttributeValue("rotation-angle", OdfNamespaces.Style);
        Assert.Equal(new OdfAngle("45.5"), angle);
        Assert.True(angle!.Value.TryGetDegrees(out decimal degrees));
        Assert.Equal(45.5m, degrees);
        Assert.Equal(new OdfStyleName("CellStyle1"), cell.GetStyleNameAttributeValue("style-name", OdfNamespaces.Table));
        Assert.Equal("CellStyle1", cell.GetAttribute("style-name", OdfNamespaces.Table));
        OdfStyleNameList? styleNameList = cell.GetStyleNameListAttributeValue("style-names", OdfNamespaces.Table);
        Assert.Equal(2, styleNameList!.Value.StyleNames.Count);
        Assert.Equal("Accent2", styleNameList.Value.StyleNames[1].Value);
        Assert.Equal(new OdfColor("#ffcc00"), cell.GetColorAttributeValue("fill-color", OdfNamespaces.Draw));
        Assert.Equal("#ffcc00", cell.GetAttribute("fill-color", OdfNamespaces.Draw));
        Assert.Equal(new OdfIriReference("../Pictures/logo.svg#main"), cell.GetIriReferenceAttributeValue("href", OdfNamespaces.XLink));
        Assert.Equal("../Pictures/logo.svg#main", cell.GetAttribute("href", OdfNamespaces.XLink));
        Assert.Equal(new OdfPercent("87.5%"), cell.GetPercentAttributeValue("opacity", OdfNamespaces.Draw));
        Assert.Equal(87.5m, cell.GetPercentAttributeValue("opacity", OdfNamespaces.Draw)!.Value.Percent);
        Assert.Equal(new OdfPercent("-12.5%"), cell.GetSignedPercentAttributeValue("shadow-offset", OdfNamespaces.Draw));
        Assert.Equal("-12.5%", cell.GetAttribute("shadow-offset", OdfNamespaces.Draw));
        Assert.Equal(new OdfCellAddressReference("'My Sheet'.$A$1"), cell.GetCellAddressAttributeValue("base-cell-address", OdfNamespaces.Table));
        Assert.Equal(new OdfCellRangeAddress("'My Sheet'.$A$1:'My Sheet'.$C$3"), cell.GetCellRangeAddressAttributeValue("cell-range-address", OdfNamespaces.Table));
        OdfCellRangeAddressList? cellRangeAddressList = cell.GetCellRangeAddressListAttributeValue("cell-range-address-list", OdfNamespaces.Table);
        Assert.Equal(2, cellRangeAddressList!.Value.Ranges.Count);
        Assert.Equal("'My Sheet'.$A$1:'My Sheet'.$C$3", cellRangeAddressList.Value.Ranges[0].Value);
        Assert.Equal(new OdfVector3D("(1 0 -0.5)"), cell.GetVector3DAttributeValue("extrusion-direction", OdfNamespaces.Draw));
        Assert.Equal(-0.5m, cell.GetVector3DAttributeValue("extrusion-direction", OdfNamespaces.Draw)!.Value.Z);
        OdfPoint3D? point = cell.GetPoint3DAttributeValue("extrusion-viewpoint", OdfNamespaces.Draw);
        Assert.Equal(new OdfPoint3D("(1cm 0mm -0.5in)"), point);
        Assert.Equal(OdfUnit.Inches, point!.Value.Z.Unit);
        OdfPointList? pointList = cell.GetPointListAttributeValue("points", OdfNamespaces.Draw);
        Assert.Equal(2, pointList!.Value.Points.Count);
        Assert.Equal(-20, pointList.Value.Points[1].Y);
        Assert.Equal(new OdfXmlName("Shape1"), cell.GetXmlNameAttributeValue("shape-id", OdfNamespaces.Draw));
        Assert.Equal(new OdfLanguageCode("zh"), cell.GetLanguageCodeAttributeValue("language", OdfNamespaces.Fo));
        Assert.Equal(new OdfCountryCode("TW"), cell.GetCountryCodeAttributeValue("country", OdfNamespaces.Fo));
        Assert.Equal(new OdfScriptCode("Hant"), cell.GetScriptCodeAttributeValue("script", OdfNamespaces.Fo));
        Assert.Equal(new OdfLanguageTag("zh-Hant-TW"), cell.GetLanguageTagAttributeValue("rfc-language-tag", OdfNamespaces.Table));
        OdfNamespacedToken? namespacedToken = cell.GetNamespacedTokenAttributeValue("type-name", OdfNamespaces.Draw);
        Assert.Equal(new OdfNamespacedToken("draw:shape"), namespacedToken);
        Assert.Equal("draw", namespacedToken!.Value.Prefix);
        Assert.Equal(new OdfCharacter("*"), cell.GetCharacterAttributeValue("decimal-replacement", OdfNamespaces.Number));
        Assert.Equal(new OdfTextEncoding("UTF-8"), cell.GetTextEncodingAttributeValue("encoding", OdfNamespaces.Text));
        OdfTargetFrameName? targetFrameName = cell.GetTargetFrameNameAttributeValue("target-frame-name", OdfNamespaces.Office);
        Assert.Equal(new OdfTargetFrameName("_blank"), targetFrameName);
        Assert.True(targetFrameName!.Value.IsReservedTarget);
        Assert.Equal(OdfXLinkType.Simple, cell.GetXLinkTypeAttributeValue("type", OdfNamespaces.XLink));
        Assert.Equal("simple", cell.GetAttribute("type", OdfNamespaces.XLink));
        Assert.Equal(OdfXLinkShow.Embed, cell.GetXLinkShowAttributeValue("show", OdfNamespaces.XLink));
        Assert.Equal("embed", cell.GetAttribute("show", OdfNamespaces.XLink));
        Assert.Equal(OdfXLinkActuate.OnLoad, cell.GetXLinkActuateAttributeValue("actuate", OdfNamespaces.XLink));
        Assert.Equal("onLoad", cell.GetAttribute("actuate", OdfNamespaces.XLink));
        Assert.Equal(OdfNumberStyle.Long, cell.GetNumberStyleAttributeValue("style", OdfNamespaces.Number));
        Assert.Equal("long", cell.GetAttribute("style", OdfNamespaces.Number));
        Assert.Equal(OdfNumberCalendar.HanjaYoil, cell.GetNumberCalendarAttributeValue("calendar", OdfNamespaces.Number));
        Assert.Equal("hanja_yoil", cell.GetAttribute("calendar", OdfNamespaces.Number));
        cell.SetAttribute("calendar", OdfNamespaces.Number, "ROC");
        Assert.Equal(OdfNumberCalendar.Roc, cell.GetNumberCalendarAttributeValue("calendar", OdfNamespaces.Number));
        Assert.Equal(OdfTableOrder.Descending, cell.GetTableOrderAttributeValue("order", OdfNamespaces.Table));
        Assert.Equal("descending", cell.GetAttribute("order", OdfNamespaces.Table));
        Assert.Equal(OdfTableType.RunningTotal, cell.GetTableTypeAttributeValue("type", OdfNamespaces.Table));
        Assert.Equal("running-total", cell.GetAttribute("type", OdfNamespaces.Table));
        Assert.Equal(OdfPresentationEffect.MoveShort, cell.GetPresentationEffectAttributeValue("effect", OdfNamespaces.Presentation));
        Assert.Equal("move-short", cell.GetAttribute("effect", OdfNamespaces.Presentation));
        Assert.Equal(OdfPresentationSpeed.Fast, cell.GetPresentationSpeedAttributeValue("speed", OdfNamespaces.Presentation));
        Assert.Equal("fast", cell.GetAttribute("speed", OdfNamespaces.Presentation));
        Assert.Equal(OdfPresentationAction.LastVisitedPage, cell.GetPresentationActionAttributeValue("action", OdfNamespaces.Presentation));
        Assert.Equal("last-visited-page", cell.GetAttribute("action", OdfNamespaces.Presentation));
        Assert.Equal(OdfPresentationTransitionType.SemiAutomatic, cell.GetPresentationTransitionTypeAttributeValue("transition-type", OdfNamespaces.Presentation));
        Assert.Equal("semi-automatic", cell.GetAttribute("transition-type", OdfNamespaces.Presentation));
        Assert.Equal(OdfPresentationTransitionStyle.InterlockingHorizontalRight, cell.GetPresentationTransitionStyleAttributeValue("transition-style", OdfNamespaces.Presentation));
        Assert.Equal("interlocking-horizontal-right", cell.GetAttribute("transition-style", OdfNamespaces.Presentation));
        Assert.Equal(OdfFoTextTransform.Uppercase, cell.GetFoTextTransformAttributeValue("text-transform", OdfNamespaces.Fo));
        Assert.Equal("uppercase", cell.GetAttribute("text-transform", OdfNamespaces.Fo));
        Assert.Equal(OdfFoTextAlign.Justify, cell.GetFoTextAlignAttributeValue("text-align", OdfNamespaces.Fo));
        Assert.Equal("justify", cell.GetAttribute("text-align", OdfNamespaces.Fo));
        Assert.Equal(OdfStyleTextRotationScale.LineHeight, cell.GetStyleTextRotationScaleAttributeValue("text-rotation-scale", OdfNamespaces.Style));
        Assert.Equal("line-height", cell.GetAttribute("text-rotation-scale", OdfNamespaces.Style));
        Assert.Equal(OdfStyleTextCombine.Letters, cell.GetStyleTextCombineAttributeValue("text-combine", OdfNamespaces.Style));
        Assert.Equal("letters", cell.GetAttribute("text-combine", OdfNamespaces.Style));
        Assert.Equal(OdfDrawFill.Gradient, cell.GetDrawFillAttributeValue("fill", OdfNamespaces.Draw));
        Assert.Equal("gradient", cell.GetAttribute("fill", OdfNamespaces.Draw));
        Assert.Equal(OdfSmilFill.Freeze, cell.GetSmilFillAttributeValue("fill", smilNamespace));
        Assert.Equal("freeze", cell.GetAttribute("fill", smilNamespace));
        Assert.Equal(OdfDrawFillImageRefPoint.BottomRight, cell.GetDrawFillImageRefPointAttributeValue("fill-image-ref-point", OdfNamespaces.Draw));
        Assert.Equal("bottom-right", cell.GetAttribute("fill-image-ref-point", OdfNamespaces.Draw));
        Assert.Equal(OdfDrawColorMode.Greyscale, cell.GetDrawColorModeAttributeValue("color-mode", OdfNamespaces.Draw));
        Assert.Equal("greyscale", cell.GetAttribute("color-mode", OdfNamespaces.Draw));
        Assert.Equal(OdfStyleVerticalAlign.Middle, cell.GetStyleVerticalAlignAttributeValue("vertical-align", OdfNamespaces.Style));
        Assert.Equal("middle", cell.GetAttribute("vertical-align", OdfNamespaces.Style));
        Assert.Equal(OdfStyleVerticalPos.FromTop, cell.GetStyleVerticalPosAttributeValue("vertical-pos", OdfNamespaces.Style));
        Assert.Equal("from-top", cell.GetAttribute("vertical-pos", OdfNamespaces.Style));
        Assert.Equal(OdfStyleVerticalRel.PageContentBottom, cell.GetStyleVerticalRelAttributeValue("vertical-rel", OdfNamespaces.Style));
        Assert.Equal("page-content-bottom", cell.GetAttribute("vertical-rel", OdfNamespaces.Style));
        Assert.Equal(OdfStyleHorizontalPos.FromInside, cell.GetStyleHorizontalPosAttributeValue("horizontal-pos", OdfNamespaces.Style));
        Assert.Equal("from-inside", cell.GetAttribute("horizontal-pos", OdfNamespaces.Style));
        Assert.Equal(OdfStyleHorizontalRel.ParagraphStartMargin, cell.GetStyleHorizontalRelAttributeValue("horizontal-rel", OdfNamespaces.Style));
        Assert.Equal("paragraph-start-margin", cell.GetAttribute("horizontal-rel", OdfNamespaces.Style));
        Assert.Equal(OdfStyleWrap.RunThrough, cell.GetStyleWrapAttributeValue("wrap", OdfNamespaces.Style));
        Assert.Equal("run-through", cell.GetAttribute("wrap", OdfNamespaces.Style));
        Assert.Equal(OdfStyleRunThrough.Foreground, cell.GetStyleRunThroughAttributeValue("run-through", OdfNamespaces.Style));
        Assert.Equal("foreground", cell.GetAttribute("run-through", OdfNamespaces.Style));
        Assert.Equal(OdfStyleWrapContourMode.Outside, cell.GetStyleWrapContourModeAttributeValue("wrap-contour-mode", OdfNamespaces.Style));
        Assert.Equal("outside", cell.GetAttribute("wrap-contour-mode", OdfNamespaces.Style));
        Assert.Equal(OdfStyleWritingMode.SidewaysRl, cell.GetStyleWritingModeAttributeValue("writing-mode", OdfNamespaces.Style));
        Assert.Equal("sideways-rl", cell.GetAttribute("writing-mode", OdfNamespaces.Style));
        Assert.Equal(OdfTableDisplayMemberMode.FromBottom, cell.GetTableDisplayMemberModeAttributeValue("display-member-mode", OdfNamespaces.Table));
        Assert.Equal("from-bottom", cell.GetAttribute("display-member-mode", OdfNamespaces.Table));
        Assert.Equal(OdfTableLayoutMode.TabularLayout, cell.GetTableLayoutModeAttributeValue("layout-mode", OdfNamespaces.Table));
        Assert.Equal("tabular-layout", cell.GetAttribute("layout-mode", OdfNamespaces.Table));
        Assert.Equal(OdfTableMemberType.Previous, cell.GetTableMemberTypeAttributeValue("member-type", OdfNamespaces.Table));
        Assert.Equal("previous", cell.GetAttribute("member-type", OdfNamespaces.Table));
        Assert.Equal(OdfTableGroupedBy.Quarters, cell.GetTableGroupedByAttributeValue("grouped-by", OdfNamespaces.Table));
        Assert.Equal("quarters", cell.GetAttribute("grouped-by", OdfNamespaces.Table));
        Assert.Equal(OdfTableSortMode.Manual, cell.GetTableSortModeAttributeValue("sort-mode", OdfNamespaces.Table));
        Assert.Equal("manual", cell.GetAttribute("sort-mode", OdfNamespaces.Table));
        Assert.Equal(OdfTableConditionSource.CellRange, cell.GetTableConditionSourceAttributeValue("condition-source", OdfNamespaces.Table));
        Assert.Equal("cell-range", cell.GetAttribute("condition-source", OdfNamespaces.Table));
        Assert.Equal(OdfTableFunction.Stdevp, cell.GetTableFunctionAttributeValue("function", OdfNamespaces.Table));
        Assert.Equal("stdevp", cell.GetAttribute("function", OdfNamespaces.Table));
        Assert.Equal(OdfDatabaseRule.SetNull, cell.GetDatabaseRuleAttributeValue("delete-rule", databaseNamespace));
        Assert.Equal("set-null", cell.GetAttribute("delete-rule", databaseNamespace));
        Assert.Equal(OdfDatabaseIsNullable.NoNulls, cell.GetDatabaseIsNullableAttributeValue("is-nullable", databaseNamespace));
        Assert.Equal("no-nulls", cell.GetAttribute("is-nullable", databaseNamespace));
        Assert.Equal(OdfDatabaseDataSourceSettingType.Boolean, cell.GetDatabaseDataSourceSettingTypeAttributeValue("data-source-setting-type", databaseNamespace));
        Assert.Equal("boolean", cell.GetAttribute("data-source-setting-type", databaseNamespace));
        Assert.Equal(OdfAnimationColorInterpolation.Hsl, cell.GetAnimationColorInterpolationAttributeValue("color-interpolation", animationNamespace));
        Assert.Equal("hsl", cell.GetAttribute("color-interpolation", animationNamespace));
        Assert.Equal(OdfAnimationColorInterpolationDirection.CounterClockwise, cell.GetAnimationColorInterpolationDirectionAttributeValue("color-interpolation-direction", animationNamespace));
        Assert.Equal("counter-clockwise", cell.GetAttribute("color-interpolation-direction", animationNamespace));
        Assert.Equal(OdfDrawNoHref.Nohref, cell.GetDrawNoHrefAttributeValue("nohref", OdfNamespaces.Draw));
        Assert.Equal("nohref", cell.GetAttribute("nohref", OdfNamespaces.Draw));
        Assert.Equal(OdfPresentationPresetClass.MotionPath, cell.GetPresentationPresetClassAttributeValue("preset-class", OdfNamespaces.Presentation));
        Assert.Equal("motion-path", cell.GetAttribute("preset-class", OdfNamespaces.Presentation));
        Assert.Equal(OdfNumberTransliterationStyle.Medium, cell.GetNumberTransliterationStyleAttributeValue("transliteration-style", OdfNamespaces.Number));
        Assert.Equal("medium", cell.GetAttribute("transliteration-style", OdfNamespaces.Number));
        Assert.Equal(OdfStyleScriptType.Complex, cell.GetStyleScriptTypeAttributeValue("script-type", OdfNamespaces.Style));
        Assert.Equal("complex", cell.GetAttribute("script-type", OdfNamespaces.Style));
        Assert.Equal(OdfStyleTextEmphasize.Circle, cell.GetStyleTextEmphasizeAttributeValue("text-emphasize", OdfNamespaces.Style));
        Assert.Equal("circle", cell.GetAttribute("text-emphasize", OdfNamespaces.Style));
        Assert.Equal(OdfDrawStrokeLineJoin.Miter, cell.GetDrawStrokeLineJoinAttributeValue("stroke-linejoin", OdfNamespaces.Draw));
        Assert.Equal("miter", cell.GetAttribute("stroke-linejoin", OdfNamespaces.Draw));
        Assert.Equal(OdfSvgStrokeLineCap.Square, cell.GetSvgStrokeLineCapAttributeValue("stroke-linecap", OdfNamespaces.Svg));
        Assert.Equal("square", cell.GetAttribute("stroke-linecap", OdfNamespaces.Svg));
        Assert.Equal(OdfFoKeepTogether.Always, cell.GetFoKeepTogetherAttributeValue("keep-together", OdfNamespaces.Fo));
        Assert.Equal("always", cell.GetAttribute("keep-together", OdfNamespaces.Fo));
        Assert.Equal(OdfFoWrapOption.NoWrap, cell.GetFoWrapOptionAttributeValue("wrap-option", OdfNamespaces.Fo));
        Assert.Equal("no-wrap", cell.GetAttribute("wrap-option", OdfNamespaces.Fo));
        Assert.Equal(OdfDr3dProjection.Perspective, cell.GetDr3dProjectionAttributeValue("projection", dr3dNamespace));
        Assert.Equal("perspective", cell.GetAttribute("projection", dr3dNamespace));
        Assert.Equal(OdfDr3dShadeMode.Phong, cell.GetDr3dShadeModeAttributeValue("shade-mode", dr3dNamespace));
        Assert.Equal("phong", cell.GetAttribute("shade-mode", dr3dNamespace));
        Assert.Equal(OdfSvgFillRule.EvenOdd, cell.GetSvgFillRuleAttributeValue("fill-rule", OdfNamespaces.Svg));
        Assert.Equal("evenodd", cell.GetAttribute("fill-rule", OdfNamespaces.Svg));
        Assert.Equal(OdfTableBorderModel.Collapsing, cell.GetTableBorderModelAttributeValue("border-model", OdfNamespaces.Table));
        Assert.Equal("collapsing", cell.GetAttribute("border-model", OdfNamespaces.Table));
        Assert.Equal(OdfTextLabelFollowedBy.ListTab, cell.GetTextLabelFollowedByAttributeValue("label-followed-by", OdfNamespaces.Text));
        Assert.Equal("listtab", cell.GetAttribute("label-followed-by", OdfNamespaces.Text));
        Assert.Equal(OdfTextListLevelPositionMode.LabelAlignment, cell.GetTextListLevelPositionModeAttributeValue("list-level-position-and-space-mode", OdfNamespaces.Text));
        Assert.Equal("label-alignment", cell.GetAttribute("list-level-position-and-space-mode", OdfNamespaces.Text));
        Assert.Equal(OdfTextIndexScope.Document, cell.GetTextIndexScopeAttributeValue("index-scope", OdfNamespaces.Text));
        Assert.Equal("document", cell.GetAttribute("index-scope", OdfNamespaces.Text));
        Assert.Equal(OdfTextTableType.Query, cell.GetTextTableTypeAttributeValue("table-type", OdfNamespaces.Text));
        Assert.Equal("query", cell.GetAttribute("table-type", OdfNamespaces.Text));
        Assert.Equal(OdfTextAnchorType.AsChar, cell.GetTextAnchorTypeAttributeValue("anchor-type", OdfNamespaces.Text));
        Assert.Equal("as-char", cell.GetAttribute("anchor-type", OdfNamespaces.Text));
        Assert.Equal(OdfTextNoteClass.Footnote, cell.GetTextNoteClassAttributeValue("note-class", OdfNamespaces.Text));
        Assert.Equal("footnote", cell.GetAttribute("note-class", OdfNamespaces.Text));
        Assert.Equal(OdfTextSelectPage.Previous, cell.GetTextSelectPageAttributeValue("select-page", OdfNamespaces.Text));
        Assert.Equal("previous", cell.GetAttribute("select-page", OdfNamespaces.Text));
        Assert.Equal(OdfTextReferenceFormat.NumberAllSuperior, cell.GetTextReferenceFormatAttributeValue("reference-format", OdfNamespaces.Text));
        Assert.Equal("number-all-superior", cell.GetAttribute("reference-format", OdfNamespaces.Text));
        Assert.Equal(OdfTextStartNumberingAt.Page, cell.GetTextStartNumberingAtAttributeValue("start-numbering-at", OdfNamespaces.Text));
        Assert.Equal("page", cell.GetAttribute("start-numbering-at", OdfNamespaces.Text));
        Assert.Equal(OdfTextFootnotesPosition.Section, cell.GetTextFootnotesPositionAttributeValue("footnotes-position", OdfNamespaces.Text));
        Assert.Equal("section", cell.GetAttribute("footnotes-position", OdfNamespaces.Text));
        Assert.Equal(OdfTextCaptionSequenceFormat.CategoryAndValue, cell.GetTextCaptionSequenceFormatAttributeValue("caption-sequence-format", OdfNamespaces.Text));
        Assert.Equal("category-and-value", cell.GetAttribute("caption-sequence-format", OdfNamespaces.Text));
        Assert.Equal(OdfTextNumberPosition.Outer, cell.GetTextNumberPositionAttributeValue("number-position", OdfNamespaces.Text));
        Assert.Equal("outer", cell.GetAttribute("number-position", OdfNamespaces.Text));
        Assert.Equal(OdfTextPlaceholderType.TextBox, cell.GetTextPlaceholderTypeAttributeValue("placeholder-type", OdfNamespaces.Text));
        Assert.Equal("text-box", cell.GetAttribute("placeholder-type", OdfNamespaces.Text));
        Assert.Equal(OdfTextAnimation.Scroll, cell.GetTextAnimationAttributeValue("animation", OdfNamespaces.Text));
        Assert.Equal("scroll", cell.GetAttribute("animation", OdfNamespaces.Text));
        Assert.Equal(OdfTextAnimationDirection.Up, cell.GetTextAnimationDirectionAttributeValue("animation-direction", OdfNamespaces.Text));
        Assert.Equal("up", cell.GetAttribute("animation-direction", OdfNamespaces.Text));
        Assert.Equal(OdfTextKind.Unit, cell.GetTextKindAttributeValue("kind", OdfNamespaces.Text));
        Assert.Equal("unit", cell.GetAttribute("kind", OdfNamespaces.Text));
        Assert.Equal(OdfLineStyle.LongDash, cell.GetLineStyleAttributeValue("text-underline-style", OdfNamespaces.Style));
        Assert.Equal("long-dash", cell.GetAttribute("text-underline-style", OdfNamespaces.Style));
        Assert.Equal(OdfLineType.Double, cell.GetLineTypeAttributeValue("text-underline-type", OdfNamespaces.Style));
        Assert.Equal("double", cell.GetAttribute("text-underline-type", OdfNamespaces.Style));
        OdfLineWidth? lineWidth = cell.GetLineWidthAttributeValue("text-underline-width", OdfNamespaces.Style);
        Assert.Equal(OdfLineWidthKind.Percent, lineWidth!.Value.Kind);
        Assert.Equal(150m, lineWidth.Value.Percent);
        Assert.Equal(OdfLineMode.SkipWhiteSpace, cell.GetLineModeAttributeValue("text-underline-mode", OdfNamespaces.Style));
        Assert.Equal("skip-white-space", cell.GetAttribute("text-underline-mode", OdfNamespaces.Style));
        Assert.Equal(OdfFontStyle.Italic, cell.GetFontStyleAttributeValue("font-style", OdfNamespaces.Fo));
        Assert.Equal("italic", cell.GetAttribute("font-style", OdfNamespaces.Fo));
        Assert.Equal(OdfFontVariant.SmallCaps, cell.GetFontVariantAttributeValue("font-variant", OdfNamespaces.Fo));
        Assert.Equal("small-caps", cell.GetAttribute("font-variant", OdfNamespaces.Fo));
        Assert.Equal(OdfFontWeight.Weight700, cell.GetFontWeightAttributeValue("font-weight", OdfNamespaces.Fo));
        Assert.Equal("700", cell.GetAttribute("font-weight", OdfNamespaces.Fo));
        Assert.Equal(OdfFontFamilyGeneric.Swiss, cell.GetFontFamilyGenericAttributeValue("font-family-generic", OdfNamespaces.Style));
        Assert.Equal("swiss", cell.GetAttribute("font-family-generic", OdfNamespaces.Style));
        Assert.Equal(OdfFontPitch.Fixed, cell.GetFontPitchAttributeValue("font-pitch", OdfNamespaces.Style));
        Assert.Equal("fixed", cell.GetAttribute("font-pitch", OdfNamespaces.Style));
        Assert.Equal(OdfFontRelief.Embossed, cell.GetFontReliefAttributeValue("font-relief", OdfNamespaces.Style));
        Assert.Equal("embossed", cell.GetAttribute("font-relief", OdfNamespaces.Style));
        Assert.Equal(OdfFontStretch.SemiExpanded, cell.GetFontStretchAttributeValue("font-stretch", OdfNamespaces.Svg));
        Assert.Equal("semi-expanded", cell.GetAttribute("font-stretch", OdfNamespaces.Svg));
        Assert.Equal(OdfStyleLineBreak.Strict, cell.GetStyleLineBreakAttributeValue("line-break", OdfNamespaces.Style));
        Assert.Equal("strict", cell.GetAttribute("line-break", OdfNamespaces.Style));
        Assert.Equal(OdfStyleRepeat.Stretch, cell.GetStyleRepeatAttributeValue("repeat", OdfNamespaces.Style));
        Assert.Equal("stretch", cell.GetAttribute("repeat", OdfNamespaces.Style));
        Assert.Equal(OdfStyleDirection.TopToBottom, cell.GetStyleDirectionAttributeValue("direction", OdfNamespaces.Style));
        Assert.Equal("ttb", cell.GetAttribute("direction", OdfNamespaces.Style));
        Assert.Equal(OdfFormOrientation.Vertical, cell.GetFormOrientationAttributeValue("orientation", formNamespace));
        Assert.Equal("vertical", cell.GetAttribute("orientation", formNamespace));
        Assert.Equal(OdfTableDirection.FromSameTable, cell.GetTableDirectionAttributeValue("direction", OdfNamespaces.Table));
        Assert.Equal("from-same-table", cell.GetAttribute("direction", OdfNamespaces.Table));
        Assert.Equal(OdfTableOrientation.Page, cell.GetTableOrientationAttributeValue("orientation", OdfNamespaces.Table));
        Assert.Equal("page", cell.GetAttribute("orientation", OdfNamespaces.Table));

        cell.SetDateTimeAttributeValue("date-value", OdfNamespaces.Office, local, OdfNamespaces.GetPrefix(OdfNamespaces.Office));
        cell.SetAttribute("time-value", OdfNamespaces.Office, "23:59:59.125+02:30");

        Assert.Equal(local, cell.GetDateTimeAttributeValue("date-value", OdfNamespaces.Office));
        Assert.Equal("2026-06-13T17:30:00", cell.GetAttribute("date-value", OdfNamespaces.Office));
        Assert.Equal(new OdfTime(new TimeSpan(0, 23, 59, 59, 125), new TimeSpan(2, 30, 0)), cell.GetTimeAttributeValue("time-value", OdfNamespaces.Office));
        cell.SetAttribute("time-value", OdfNamespaces.Office, "25:00:00");
        Assert.Null(cell.GetTimeAttributeValue("time-value", OdfNamespaces.Office));
        cell.SetAttribute("width", OdfNamespaces.Style, "invalid-length");
        Assert.Null(cell.GetLengthAttributeValue("width", OdfNamespaces.Style));
        cell.SetAttribute("border-line-width", OdfNamespaces.Style, "0.5pt 1pt");
        Assert.Null(cell.GetBorderWidthsAttributeValue("border-line-width", OdfNamespaces.Style));
        cell.SetAttribute("border-line-width", OdfNamespaces.Style, "0pt 1pt 0.5pt");
        Assert.Null(cell.GetBorderWidthsAttributeValue("border-line-width", OdfNamespaces.Style));
        cell.SetAttribute("duration", OdfNamespaces.Presentation, "not-duration");
        Assert.Null(cell.GetDurationAttributeValue("duration", OdfNamespaces.Presentation));
        cell.SetAttribute("rotation-angle", OdfNamespaces.Style, "\u0001");
        Assert.Null(cell.GetAngleAttributeValue("rotation-angle", OdfNamespaces.Style));
        cell.SetAttribute("style-name", OdfNamespaces.Table, "invalid style");
        Assert.Null(cell.GetStyleNameAttributeValue("style-name", OdfNamespaces.Table));
        cell.SetAttribute("style-names", OdfNamespaces.Table, "Valid invalid:name");
        Assert.Null(cell.GetStyleNameListAttributeValue("style-names", OdfNamespaces.Table));
        cell.SetAttribute("style-names", OdfNamespaces.Table, string.Empty);
        Assert.Empty(cell.GetStyleNameListAttributeValue("style-names", OdfNamespaces.Table)!.Value.StyleNames);
        cell.SetAttribute("fill-color", OdfNamespaces.Draw, "#ff0");
        Assert.Null(cell.GetColorAttributeValue("fill-color", OdfNamespaces.Draw));
        cell.SetAttribute("href", OdfNamespaces.XLink, "bad\u0001iri");
        Assert.Null(cell.GetIriReferenceAttributeValue("href", OdfNamespaces.XLink));
        cell.SetAttribute("opacity", OdfNamespaces.Draw, "-1%");
        Assert.Null(cell.GetPercentAttributeValue("opacity", OdfNamespaces.Draw));
        cell.SetAttribute("shadow-offset", OdfNamespaces.Draw, "-101%");
        Assert.Null(cell.GetSignedPercentAttributeValue("shadow-offset", OdfNamespaces.Draw));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            cell.SetPercentAttributeValue("opacity", OdfNamespaces.Draw, new OdfPercent("-1%"), OdfNamespaces.GetPrefix(OdfNamespaces.Draw)));
        cell.SetAttribute("base-cell-address", OdfNamespaces.Table, "A1");
        Assert.Null(cell.GetCellAddressAttributeValue("base-cell-address", OdfNamespaces.Table));
        cell.SetAttribute("cell-range-address", OdfNamespaces.Table, ".A1:B2");
        Assert.Null(cell.GetCellRangeAddressAttributeValue("cell-range-address", OdfNamespaces.Table));
        cell.SetAttribute("cell-range-address-list", OdfNamespaces.Table, "'My Sheet'.$A$1:'My Sheet'.$C$3 bad-range");
        Assert.Null(cell.GetCellRangeAddressListAttributeValue("cell-range-address-list", OdfNamespaces.Table));
        cell.SetAttribute("extrusion-direction", OdfNamespaces.Draw, "(1 2)");
        Assert.Null(cell.GetVector3DAttributeValue("extrusion-direction", OdfNamespaces.Draw));
        cell.SetAttribute("extrusion-viewpoint", OdfNamespaces.Draw, "(1px 2px 3px)");
        Assert.Null(cell.GetPoint3DAttributeValue("extrusion-viewpoint", OdfNamespaces.Draw));
        cell.SetAttribute("points", OdfNamespaces.Draw, "0,0 1.5,2");
        Assert.Null(cell.GetPointListAttributeValue("points", OdfNamespaces.Draw));
        cell.SetAttribute("shape-id", OdfNamespaces.Draw, "bad:name");
        Assert.Null(cell.GetXmlNameAttributeValue("shape-id", OdfNamespaces.Draw));
        cell.SetAttribute("language", OdfNamespaces.Fo, "zh-Hant");
        Assert.Null(cell.GetLanguageCodeAttributeValue("language", OdfNamespaces.Fo));
        cell.SetAttribute("country", OdfNamespaces.Fo, "too-long-code");
        Assert.Null(cell.GetCountryCodeAttributeValue("country", OdfNamespaces.Fo));
        cell.SetAttribute("script", OdfNamespaces.Fo, "Hant!");
        Assert.Null(cell.GetScriptCodeAttributeValue("script", OdfNamespaces.Fo));
        cell.SetAttribute("rfc-language-tag", OdfNamespaces.Table, "zh--TW");
        Assert.Null(cell.GetLanguageTagAttributeValue("rfc-language-tag", OdfNamespaces.Table));
        cell.SetAttribute("type-name", OdfNamespaces.Draw, "draw:bad:name");
        Assert.Null(cell.GetNamespacedTokenAttributeValue("type-name", OdfNamespaces.Draw));
        cell.SetAttribute("decimal-replacement", OdfNamespaces.Number, "ab");
        Assert.Null(cell.GetCharacterAttributeValue("decimal-replacement", OdfNamespaces.Number));
        cell.SetAttribute("encoding", OdfNamespaces.Text, "8bit");
        Assert.Null(cell.GetTextEncodingAttributeValue("encoding", OdfNamespaces.Text));
        cell.SetAttribute("target-frame-name", OdfNamespaces.Office, "ReportFrame");
        Assert.False(cell.GetTargetFrameNameAttributeValue("target-frame-name", OdfNamespaces.Office)!.Value.IsReservedTarget);
        cell.SetAttribute("target-frame-name", OdfNamespaces.Office, "\u0001");
        Assert.Null(cell.GetTargetFrameNameAttributeValue("target-frame-name", OdfNamespaces.Office));
        cell.SetAttribute("type", OdfNamespaces.XLink, "extended");
        Assert.Null(cell.GetXLinkTypeAttributeValue("type", OdfNamespaces.XLink));
        cell.SetAttribute("show", OdfNamespaces.XLink, "other");
        Assert.Null(cell.GetXLinkShowAttributeValue("show", OdfNamespaces.XLink));
        cell.SetAttribute("actuate", OdfNamespaces.XLink, "manual");
        Assert.Null(cell.GetXLinkActuateAttributeValue("actuate", OdfNamespaces.XLink));
        cell.SetAttribute("style", OdfNamespaces.Number, "medium");
        Assert.Null(cell.GetNumberStyleAttributeValue("style", OdfNamespaces.Number));
        cell.SetAttribute("calendar", OdfNamespaces.Number, "iso");
        Assert.Null(cell.GetNumberCalendarAttributeValue("calendar", OdfNamespaces.Number));
        cell.SetAttribute("order", OdfNamespaces.Table, "random");
        Assert.Null(cell.GetTableOrderAttributeValue("order", OdfNamespaces.Table));
        cell.SetAttribute("type", OdfNamespaces.Table, "custom");
        Assert.Null(cell.GetTableTypeAttributeValue("type", OdfNamespaces.Table));
        cell.SetAttribute("effect", OdfNamespaces.Presentation, "zoom");
        Assert.Null(cell.GetPresentationEffectAttributeValue("effect", OdfNamespaces.Presentation));
        cell.SetAttribute("speed", OdfNamespaces.Presentation, "instant");
        Assert.Null(cell.GetPresentationSpeedAttributeValue("speed", OdfNamespaces.Presentation));
        cell.SetAttribute("action", OdfNamespaces.Presentation, "rewind");
        Assert.Null(cell.GetPresentationActionAttributeValue("action", OdfNamespaces.Presentation));
        cell.SetAttribute("transition-type", OdfNamespaces.Presentation, "timed");
        Assert.Null(cell.GetPresentationTransitionTypeAttributeValue("transition-type", OdfNamespaces.Presentation));
        cell.SetAttribute("transition-style", OdfNamespaces.Presentation, "explode");
        Assert.Null(cell.GetPresentationTransitionStyleAttributeValue("transition-style", OdfNamespaces.Presentation));
        cell.SetAttribute("text-transform", OdfNamespaces.Fo, "titlecase");
        Assert.Null(cell.GetFoTextTransformAttributeValue("text-transform", OdfNamespaces.Fo));
        cell.SetAttribute("text-align", OdfNamespaces.Fo, "middle");
        Assert.Null(cell.GetFoTextAlignAttributeValue("text-align", OdfNamespaces.Fo));
        cell.SetAttribute("text-rotation-scale", OdfNamespaces.Style, "auto");
        Assert.Null(cell.GetStyleTextRotationScaleAttributeValue("text-rotation-scale", OdfNamespaces.Style));
        cell.SetAttribute("text-combine", OdfNamespaces.Style, "words");
        Assert.Null(cell.GetStyleTextCombineAttributeValue("text-combine", OdfNamespaces.Style));
        cell.SetAttribute("fill", OdfNamespaces.Draw, "texture");
        Assert.Null(cell.GetDrawFillAttributeValue("fill", OdfNamespaces.Draw));
        cell.SetAttribute("fill", smilNamespace, "keep");
        Assert.Null(cell.GetSmilFillAttributeValue("fill", smilNamespace));
        cell.SetAttribute("fill-image-ref-point", OdfNamespaces.Draw, "middle");
        Assert.Null(cell.GetDrawFillImageRefPointAttributeValue("fill-image-ref-point", OdfNamespaces.Draw));
        cell.SetAttribute("color-mode", OdfNamespaces.Draw, "sepia");
        Assert.Null(cell.GetDrawColorModeAttributeValue("color-mode", OdfNamespaces.Draw));
        cell.SetAttribute("vertical-align", OdfNamespaces.Style, "center");
        Assert.Null(cell.GetStyleVerticalAlignAttributeValue("vertical-align", OdfNamespaces.Style));
        cell.SetAttribute("vertical-pos", OdfNamespaces.Style, "center");
        Assert.Null(cell.GetStyleVerticalPosAttributeValue("vertical-pos", OdfNamespaces.Style));
        cell.SetAttribute("vertical-rel", OdfNamespaces.Style, "margin");
        Assert.Null(cell.GetStyleVerticalRelAttributeValue("vertical-rel", OdfNamespaces.Style));
        cell.SetAttribute("horizontal-pos", OdfNamespaces.Style, "middle");
        Assert.Null(cell.GetStyleHorizontalPosAttributeValue("horizontal-pos", OdfNamespaces.Style));
        cell.SetAttribute("horizontal-rel", OdfNamespaces.Style, "margin");
        Assert.Null(cell.GetStyleHorizontalRelAttributeValue("horizontal-rel", OdfNamespaces.Style));
        cell.SetAttribute("wrap", OdfNamespaces.Style, "around");
        Assert.Null(cell.GetStyleWrapAttributeValue("wrap", OdfNamespaces.Style));
        cell.SetAttribute("run-through", OdfNamespaces.Style, "middle");
        Assert.Null(cell.GetStyleRunThroughAttributeValue("run-through", OdfNamespaces.Style));
        cell.SetAttribute("wrap-contour-mode", OdfNamespaces.Style, "inner");
        Assert.Null(cell.GetStyleWrapContourModeAttributeValue("wrap-contour-mode", OdfNamespaces.Style));
        cell.SetAttribute("writing-mode", OdfNamespaces.Style, "rtl");
        Assert.Null(cell.GetStyleWritingModeAttributeValue("writing-mode", OdfNamespaces.Style));
        cell.SetAttribute("display-member-mode", OdfNamespaces.Table, "center");
        Assert.Null(cell.GetTableDisplayMemberModeAttributeValue("display-member-mode", OdfNamespaces.Table));
        cell.SetAttribute("layout-mode", OdfNamespaces.Table, "grid");
        Assert.Null(cell.GetTableLayoutModeAttributeValue("layout-mode", OdfNamespaces.Table));
        cell.SetAttribute("member-type", OdfNamespaces.Table, "current");
        Assert.Null(cell.GetTableMemberTypeAttributeValue("member-type", OdfNamespaces.Table));
        cell.SetAttribute("grouped-by", OdfNamespaces.Table, "weeks");
        Assert.Null(cell.GetTableGroupedByAttributeValue("grouped-by", OdfNamespaces.Table));
        cell.SetAttribute("sort-mode", OdfNamespaces.Table, "alpha");
        Assert.Null(cell.GetTableSortModeAttributeValue("sort-mode", OdfNamespaces.Table));
        cell.SetAttribute("condition-source", OdfNamespaces.Table, "formula");
        Assert.Null(cell.GetTableConditionSourceAttributeValue("condition-source", OdfNamespaces.Table));
        cell.SetAttribute("function", OdfNamespaces.Table, "median");
        Assert.Null(cell.GetTableFunctionAttributeValue("function", OdfNamespaces.Table));
        cell.SetAttribute("delete-rule", databaseNamespace, "delete");
        Assert.Null(cell.GetDatabaseRuleAttributeValue("delete-rule", databaseNamespace));
        cell.SetAttribute("is-nullable", databaseNamespace, "maybe");
        Assert.Null(cell.GetDatabaseIsNullableAttributeValue("is-nullable", databaseNamespace));
        cell.SetAttribute("data-source-setting-type", databaseNamespace, "decimal");
        Assert.Null(cell.GetDatabaseDataSourceSettingTypeAttributeValue("data-source-setting-type", databaseNamespace));
        cell.SetAttribute("color-interpolation", animationNamespace, "lab");
        Assert.Null(cell.GetAnimationColorInterpolationAttributeValue("color-interpolation", animationNamespace));
        cell.SetAttribute("color-interpolation-direction", animationNamespace, "reverse");
        Assert.Null(cell.GetAnimationColorInterpolationDirectionAttributeValue("color-interpolation-direction", animationNamespace));
        cell.SetAttribute("nohref", OdfNamespaces.Draw, "href");
        Assert.Null(cell.GetDrawNoHrefAttributeValue("nohref", OdfNamespaces.Draw));
        cell.SetAttribute("preset-class", OdfNamespaces.Presentation, "transition");
        Assert.Null(cell.GetPresentationPresetClassAttributeValue("preset-class", OdfNamespaces.Presentation));
        cell.SetAttribute("transliteration-style", OdfNamespaces.Number, "compact");
        Assert.Null(cell.GetNumberTransliterationStyleAttributeValue("transliteration-style", OdfNamespaces.Number));
        cell.SetAttribute("script-type", OdfNamespaces.Style, "symbol");
        Assert.Null(cell.GetStyleScriptTypeAttributeValue("script-type", OdfNamespaces.Style));
        cell.SetAttribute("text-emphasize", OdfNamespaces.Style, "square");
        Assert.Null(cell.GetStyleTextEmphasizeAttributeValue("text-emphasize", OdfNamespaces.Style));
        cell.SetAttribute("stroke-linejoin", OdfNamespaces.Draw, "arcs");
        Assert.Null(cell.GetDrawStrokeLineJoinAttributeValue("stroke-linejoin", OdfNamespaces.Draw));
        cell.SetAttribute("stroke-linecap", OdfNamespaces.Svg, "flat");
        Assert.Null(cell.GetSvgStrokeLineCapAttributeValue("stroke-linecap", OdfNamespaces.Svg));
        cell.SetAttribute("keep-together", OdfNamespaces.Fo, "never");
        Assert.Null(cell.GetFoKeepTogetherAttributeValue("keep-together", OdfNamespaces.Fo));
        cell.SetAttribute("wrap-option", OdfNamespaces.Fo, "balance");
        Assert.Null(cell.GetFoWrapOptionAttributeValue("wrap-option", OdfNamespaces.Fo));
        cell.SetAttribute("projection", dr3dNamespace, "orthographic");
        Assert.Null(cell.GetDr3dProjectionAttributeValue("projection", dr3dNamespace));
        cell.SetAttribute("shade-mode", dr3dNamespace, "toon");
        Assert.Null(cell.GetDr3dShadeModeAttributeValue("shade-mode", dr3dNamespace));
        cell.SetAttribute("fill-rule", OdfNamespaces.Svg, "winding");
        Assert.Null(cell.GetSvgFillRuleAttributeValue("fill-rule", OdfNamespaces.Svg));
        cell.SetAttribute("border-model", OdfNamespaces.Table, "merged");
        Assert.Null(cell.GetTableBorderModelAttributeValue("border-model", OdfNamespaces.Table));
        cell.SetAttribute("label-followed-by", OdfNamespaces.Text, "tab");
        Assert.Null(cell.GetTextLabelFollowedByAttributeValue("label-followed-by", OdfNamespaces.Text));
        cell.SetAttribute("list-level-position-and-space-mode", OdfNamespaces.Text, "manual");
        Assert.Null(cell.GetTextListLevelPositionModeAttributeValue("list-level-position-and-space-mode", OdfNamespaces.Text));
        cell.SetAttribute("index-scope", OdfNamespaces.Text, "book");
        Assert.Null(cell.GetTextIndexScopeAttributeValue("index-scope", OdfNamespaces.Text));
        cell.SetAttribute("table-type", OdfNamespaces.Text, "view");
        Assert.Null(cell.GetTextTableTypeAttributeValue("table-type", OdfNamespaces.Text));
        cell.SetAttribute("anchor-type", OdfNamespaces.Text, "section");
        Assert.Null(cell.GetTextAnchorTypeAttributeValue("anchor-type", OdfNamespaces.Text));
        cell.SetAttribute("note-class", OdfNamespaces.Text, "comment");
        Assert.Null(cell.GetTextNoteClassAttributeValue("note-class", OdfNamespaces.Text));
        cell.SetAttribute("select-page", OdfNamespaces.Text, "last");
        Assert.Null(cell.GetTextSelectPageAttributeValue("select-page", OdfNamespaces.Text));
        cell.SetAttribute("reference-format", OdfNamespaces.Text, "bookmark");
        Assert.Null(cell.GetTextReferenceFormatAttributeValue("reference-format", OdfNamespaces.Text));
        cell.SetAttribute("start-numbering-at", OdfNamespaces.Text, "section");
        Assert.Null(cell.GetTextStartNumberingAtAttributeValue("start-numbering-at", OdfNamespaces.Text));
        cell.SetAttribute("footnotes-position", OdfNamespaces.Text, "chapter");
        Assert.Null(cell.GetTextFootnotesPositionAttributeValue("footnotes-position", OdfNamespaces.Text));
        cell.SetAttribute("caption-sequence-format", OdfNamespaces.Text, "number");
        Assert.Null(cell.GetTextCaptionSequenceFormatAttributeValue("caption-sequence-format", OdfNamespaces.Text));
        cell.SetAttribute("number-position", OdfNamespaces.Text, "center");
        Assert.Null(cell.GetTextNumberPositionAttributeValue("number-position", OdfNamespaces.Text));
        cell.SetAttribute("placeholder-type", OdfNamespaces.Text, "chart");
        Assert.Null(cell.GetTextPlaceholderTypeAttributeValue("placeholder-type", OdfNamespaces.Text));
        cell.SetAttribute("animation", OdfNamespaces.Text, "blink");
        Assert.Null(cell.GetTextAnimationAttributeValue("animation", OdfNamespaces.Text));
        cell.SetAttribute("animation-direction", OdfNamespaces.Text, "diagonal");
        Assert.Null(cell.GetTextAnimationDirectionAttributeValue("animation-direction", OdfNamespaces.Text));
        cell.SetAttribute("kind", OdfNamespaces.Text, "range");
        Assert.Null(cell.GetTextKindAttributeValue("kind", OdfNamespaces.Text));
        cell.SetAttribute("text-underline-style", OdfNamespaces.Style, "unknown");
        Assert.Null(cell.GetLineStyleAttributeValue("text-underline-style", OdfNamespaces.Style));
        cell.SetAttribute("text-underline-type", OdfNamespaces.Style, "triple");
        Assert.Null(cell.GetLineTypeAttributeValue("text-underline-type", OdfNamespaces.Style));
        cell.SetAttribute("text-underline-width", OdfNamespaces.Style, "0pt");
        Assert.Null(cell.GetLineWidthAttributeValue("text-underline-width", OdfNamespaces.Style));
        cell.SetAttribute("text-underline-width", OdfNamespaces.Style, "bold");
        Assert.Equal(OdfLineWidthKind.Bold, cell.GetLineWidthAttributeValue("text-underline-width", OdfNamespaces.Style)!.Value.Kind);
        cell.SetAttribute("text-underline-mode", OdfNamespaces.Style, "sometimes");
        Assert.Null(cell.GetLineModeAttributeValue("text-underline-mode", OdfNamespaces.Style));
        cell.SetAttribute("font-style", OdfNamespaces.Fo, "slanted");
        Assert.Null(cell.GetFontStyleAttributeValue("font-style", OdfNamespaces.Fo));
        cell.SetAttribute("font-variant", OdfNamespaces.Fo, "caps");
        Assert.Null(cell.GetFontVariantAttributeValue("font-variant", OdfNamespaces.Fo));
        cell.SetAttribute("font-weight", OdfNamespaces.Fo, "950");
        Assert.Null(cell.GetFontWeightAttributeValue("font-weight", OdfNamespaces.Fo));
        cell.SetAttribute("font-family-generic", OdfNamespaces.Style, "humanist");
        Assert.Null(cell.GetFontFamilyGenericAttributeValue("font-family-generic", OdfNamespaces.Style));
        cell.SetAttribute("font-pitch", OdfNamespaces.Style, "mono");
        Assert.Null(cell.GetFontPitchAttributeValue("font-pitch", OdfNamespaces.Style));
        cell.SetAttribute("font-relief", OdfNamespaces.Style, "raised");
        Assert.Null(cell.GetFontReliefAttributeValue("font-relief", OdfNamespaces.Style));
        cell.SetAttribute("font-stretch", OdfNamespaces.Svg, "wider");
        Assert.Null(cell.GetFontStretchAttributeValue("font-stretch", OdfNamespaces.Svg));
        cell.SetAttribute("line-break", OdfNamespaces.Style, "loose");
        Assert.Null(cell.GetStyleLineBreakAttributeValue("line-break", OdfNamespaces.Style));
        cell.SetAttribute("repeat", OdfNamespaces.Style, "tile-x");
        Assert.Null(cell.GetStyleRepeatAttributeValue("repeat", OdfNamespaces.Style));
        cell.SetAttribute("direction", OdfNamespaces.Style, "rtl");
        Assert.Null(cell.GetStyleDirectionAttributeValue("direction", OdfNamespaces.Style));
        cell.SetAttribute("orientation", formNamespace, "diagonal");
        Assert.Null(cell.GetFormOrientationAttributeValue("orientation", formNamespace));
        cell.SetAttribute("direction", OdfNamespaces.Table, "sideways");
        Assert.Null(cell.GetTableDirectionAttributeValue("direction", OdfNamespaces.Table));
        cell.SetAttribute("orientation", OdfNamespaces.Table, "diagonal");
        Assert.Null(cell.GetTableOrientationAttributeValue("orientation", OdfNamespaces.Table));
        Assert.Equal(7, cell.GetInt32AttributeValue("missing", OdfNamespaces.Table, 7));
        Assert.Null(cell.GetBooleanAttributeValue("missing", OdfNamespaces.Table));

        OdfElement style = new("style", OdfNamespaces.Style, "style");
        style.SetStyleFamilyAttributeValue("family", OdfNamespaces.Style, OdfStyleFamily.TableColumn, OdfNamespaces.GetPrefix(OdfNamespaces.Style));

        Assert.Equal(OdfStyleFamily.TableColumn, style.GetStyleFamilyAttributeValue("family", OdfNamespaces.Style));
        Assert.Equal("table-column", style.GetAttribute("family", OdfNamespaces.Style));
        style.SetAttribute("family", OdfNamespaces.Style, "unknown-family");
        Assert.Null(style.GetStyleFamilyAttributeValue("family", OdfNamespaces.Style));

        OdfElement document = new("document-content", OdfNamespaces.Office, "office");
        document.SetOdfVersionAttributeValue("version", OdfNamespaces.Office, OdfVersion.Odf13, OdfNamespaces.GetPrefix(OdfNamespaces.Office));

        Assert.Equal(OdfVersion.Odf13, document.GetOdfVersionAttributeValue("version", OdfNamespaces.Office));
        Assert.Equal("1.3", document.GetAttribute("version", OdfNamespaces.Office));
        document.SetAttribute("version", OdfNamespaces.Office, "2.0");
        Assert.Null(document.GetOdfVersionAttributeValue("version", OdfNamespaces.Office));

        document.SetMediaTypeAttributeValue(
            "mimetype",
            OdfNamespaces.Office,
            new OdfMediaType("application/vnd.oasis.opendocument.text"),
            OdfNamespaces.GetPrefix(OdfNamespaces.Office));

        Assert.Equal(
            new OdfMediaType("application/vnd.oasis.opendocument.text"),
            document.GetMediaTypeAttributeValue("mimetype", OdfNamespaces.Office));
        document.SetAttribute("mimetype", OdfNamespaces.Office, "not a media type");
        Assert.Null(document.GetMediaTypeAttributeValue("mimetype", OdfNamespaces.Office));
    }

    /// <summary>
    /// 驗證 typed DOM coverage 文件列出 ODFDOM 對標缺口與 coverage guard。
    /// </summary>
    [Fact]
    public void TypedDomCoverageDocumentDeclaresParityGaps()
    {
        string repoRoot = FindRepositoryRoot();
        string document = File.ReadAllText(Path.Combine(repoRoot, "docs", "typed-dom-coverage.md"));

        Assert.Contains("ODFDOM", document, StringComparison.Ordinal);
        Assert.Contains("Coverage guard", document, StringComparison.Ordinal);
        Assert.Contains("Generated typed element classes", document, StringComparison.Ordinal);
        Assert.Contains("schema-to-wrapper coverage report", document, StringComparison.Ordinal);
        Assert.Contains("schema-specific child collection", document, StringComparison.Ordinal);
        Assert.Contains("ODFDOM 風格 sample usage parity tests", document, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證 <c>OdfKit.DOM</c> 命名空間中以 lexical form 包裝字串值的型別，其 <c>GetHashCode</c>／
    /// <c>Equals</c> 契約一致：相等的值必須產生相同雜湊碼，明顯不同的值雜湊碼應不同。
    /// 涵蓋先前真機掃描比對中顯示零測試引用的全部型別，逐一確認並非真實缺口而是已有等價測試
    /// 覆蓋（透過共用基礎設施）的假陰性。
    /// </summary>
    [Fact]
    public void LexicalFormValueTypesGetHashCodeIsConsistentWithEquals()
    {
        AssertHashCodeContract(new OdfAngle("45.5"), new OdfAngle("45.5"), new OdfAngle("90"));
        AssertHashCodeContract(new OdfCellAddressReference("'My Sheet'.$A$1"), new OdfCellAddressReference("'My Sheet'.$A$1"), new OdfCellAddressReference("'My Sheet'.$B$2"));
        AssertHashCodeContract(new OdfCellRangeAddress("'My Sheet'.$A$1:'My Sheet'.$C$3"), new OdfCellRangeAddress("'My Sheet'.$A$1:'My Sheet'.$C$3"), new OdfCellRangeAddress("'My Sheet'.$A$1:'My Sheet'.$D$4"));
        AssertHashCodeContract(
            new OdfCellRangeAddressList("'My Sheet'.$A$1:'My Sheet'.$C$3 .D4:.E5"),
            new OdfCellRangeAddressList("'My Sheet'.$A$1:'My Sheet'.$C$3 .D4:.E5"),
            new OdfCellRangeAddressList("'My Sheet'.$A$1:'My Sheet'.$C$3"));
        AssertHashCodeContract(new OdfCharacter("*"), new OdfCharacter("*"), new OdfCharacter("#"));
        AssertHashCodeContract(new OdfColor("#112233"), new OdfColor("#112233"), new OdfColor("#445566"));
        AssertHashCodeContract(new OdfCountryCode("TW"), new OdfCountryCode("TW"), new OdfCountryCode("JP"));
        AssertHashCodeContract(new OdfDuration("PT1H30M"), new OdfDuration("PT1H30M"), new OdfDuration("PT2H"));
        AssertHashCodeContract(new OdfPoint2D(0, 0), new OdfPoint2D(0, 0), new OdfPoint2D(10, -20));
        AssertHashCodeContract(new OdfIriReference("Pictures/image.png"), new OdfIriReference("Pictures/image.png"), new OdfIriReference("Pictures/other.png"));
        AssertHashCodeContract(new OdfLanguageCode("zh"), new OdfLanguageCode("zh"), new OdfLanguageCode("en"));
        AssertHashCodeContract(new OdfLanguageTag("zh-Hant-TW"), new OdfLanguageTag("zh-Hant-TW"), new OdfLanguageTag("en-US"));
        AssertHashCodeContract(new OdfLineWidth("150%"), new OdfLineWidth("150%"), new OdfLineWidth("200%"));
        AssertHashCodeContract(new OdfMediaType("application/vnd.oasis.opendocument.text"), new OdfMediaType("application/vnd.oasis.opendocument.text"), new OdfMediaType("text/plain"));
        AssertHashCodeContract(new OdfNamespacedToken("draw:shape"), new OdfNamespacedToken("draw:shape"), new OdfNamespacedToken("table:cell"));
        AssertHashCodeContract(new OdfPercent("87.5%"), new OdfPercent("87.5%"), new OdfPercent("12.5%"));
        AssertHashCodeContract(new OdfPointList([new OdfPoint2D(0, 0), new OdfPoint2D(10, -20)]), new OdfPointList([new OdfPoint2D(0, 0), new OdfPoint2D(10, -20)]), new OdfPointList([new OdfPoint2D(1, 1)]));
        AssertHashCodeContract(new OdfScriptCode("Hant"), new OdfScriptCode("Hant"), new OdfScriptCode("Latn"));
        AssertHashCodeContract(new OdfStyleName("CellStyle1"), new OdfStyleName("CellStyle1"), new OdfStyleName("Accent2"));
        AssertHashCodeContract(
            new OdfStyleNameList([new OdfStyleName("CellStyle1"), new OdfStyleName("Accent2")]),
            new OdfStyleNameList([new OdfStyleName("CellStyle1"), new OdfStyleName("Accent2")]),
            new OdfStyleNameList([new OdfStyleName("Accent2")]));
        AssertHashCodeContract(new OdfTargetFrameName("_blank"), new OdfTargetFrameName("_blank"), new OdfTargetFrameName("_self"));
        AssertHashCodeContract(new OdfTextEncoding("UTF-8"), new OdfTextEncoding("UTF-8"), new OdfTextEncoding("Big5"));
        AssertHashCodeContract(new OdfTime(new TimeSpan(12, 30, 45), TimeSpan.Zero), new OdfTime(new TimeSpan(12, 30, 45), TimeSpan.Zero), new OdfTime(new TimeSpan(8, 0, 0), TimeSpan.Zero));
        AssertHashCodeContract(new OdfXmlName("Shape1"), new OdfXmlName("Shape1"), new OdfXmlName("Shape2"));

        // 多欄位複合結構：以 X／Y／Z 座標組合計算雜湊碼，而非單一 lexical form 字串。
        AssertHashCodeContract(
            new OdfPoint3D(OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(2), OdfLength.FromCentimeters(3)),
            new OdfPoint3D(OdfLength.FromCentimeters(1), OdfLength.FromCentimeters(2), OdfLength.FromCentimeters(3)),
            new OdfPoint3D(OdfLength.FromCentimeters(4), OdfLength.FromCentimeters(5), OdfLength.FromCentimeters(6)));
        AssertHashCodeContract(new OdfVector3D(1, 2, 3), new OdfVector3D(1, 2, 3), new OdfVector3D(7, 8, 9));
        AssertHashCodeContract(
            new OdfBorderWidths(OdfLength.FromPoints(0.5), OdfLength.FromPoints(1), OdfLength.FromPoints(0.5)),
            new OdfBorderWidths(OdfLength.FromPoints(0.5), OdfLength.FromPoints(1), OdfLength.FromPoints(0.5)),
            new OdfBorderWidths(OdfLength.FromPoints(1), OdfLength.FromPoints(2), OdfLength.FromPoints(1)));

        // OdfAttributeName：以 localName ＋ namespaceUri 組合鍵計算雜湊碼。
        var attr1 = new OdfAttributeName("href", OdfNamespaces.XLink);
        var attr2 = new OdfAttributeName("href", OdfNamespaces.XLink);
        var attr3 = new OdfAttributeName("name", OdfNamespaces.Table);
        Assert.Equal(attr1, attr2);
        Assert.Equal(attr1.GetHashCode(), attr2.GetHashCode());
        Assert.NotEqual(attr1.GetHashCode(), attr3.GetHashCode());
    }

    /// <summary>
    /// 驗證雜湊碼與相等性契約：相等的兩個值必須產生相同雜湊碼；明顯不同的第三個值雜湊碼應不同
    /// （非嚴格要求，僅用於合理性檢查，避免實作恆傳回固定值）。
    /// </summary>
    private static void AssertHashCodeContract<T>(T value, T equalValue, T differentValue)
        where T : IEquatable<T>
    {
        Assert.True(value.Equals(equalValue));
        Assert.Equal(value.GetHashCode(), equalValue.GetHashCode());
        Assert.NotEqual(value.GetHashCode(), differentValue.GetHashCode());
    }

    /// <summary>
    /// 驗證 <see cref="OdfNodeChildList"/> 透過 <see cref="System.Collections.Generic.IList{T}"/>
    /// 介面公開的 <c>Insert(int, OdfNode)</c>／<c>RemoveAt(int)</c>：插入後相鄰節點的雙向鏈結
    /// （<c>PreviousSibling</c>／<c>NextSibling</c>）與 <c>SiblingIndex</c> 快取必須正確重新編號；
    /// 移除後節點不可再透過 <c>Parent</c>／<c>SiblingIndex</c> 殘留懸空參照，且父節點的
    /// <c>FirstChild</c>／<c>LastChild</c> 在邊界（移除首尾節點）情況下也必須正確更新。
    /// </summary>
    [Fact]
    public void OdfNodeChildListInsertAndRemoveAtMaintainConsistentLinkedListState()
    {
        TextPElement parent = new("text");
        var first = new TextSpanElement("text") { TextContent = "first" };
        var second = new TextSpanElement("text") { TextContent = "second" };
        var third = new TextSpanElement("text") { TextContent = "third" };
        parent.Children.Add(first);
        parent.Children.Add(second);
        parent.Children.Add(third);

        // Insert 於中間索引：驗證透過 IList<T> 介面插入與透過 InsertBefore 插入語意一致。
        var inserted = new TextSpanElement("text") { TextContent = "inserted" };
        parent.Children.Insert(1, inserted);
        Assert.Equal(["first", "inserted", "second", "third"], parent.Children.Select(c => c.TextContent));
        Assert.Same(first, inserted.PreviousSibling);
        Assert.Same(second, inserted.NextSibling);
        Assert.Equal(1, inserted.SiblingIndex);
        Assert.Equal(2, second.SiblingIndex);
        Assert.Equal(3, third.SiblingIndex);

        // Insert 於末尾索引（index == Count）：應等同於 Append，而非擲出例外。
        var appended = new TextSpanElement("text") { TextContent = "appended" };
        parent.Children.Insert(parent.Children.Count, appended);
        Assert.Same(parent, appended.Parent);
        Assert.Same(third, appended.PreviousSibling);
        Assert.Null(appended.NextSibling);
        Assert.Same(appended, parent.Children[^1]);

        // RemoveAt 移除中間節點：不可留下懸空參照，且前後相鄰節點需重新連結。
        int insertedIndex = parent.Children.IndexOf(inserted);
        parent.Children.RemoveAt(insertedIndex);
        Assert.Null(inserted.Parent);
        Assert.Null(inserted.PreviousSibling);
        Assert.Null(inserted.NextSibling);
        Assert.Equal(-1, inserted.SiblingIndex);
        Assert.Equal(["first", "second", "third", "appended"], parent.Children.Select(c => c.TextContent));

        // RemoveAt 移除首節點：父節點 FirstChild 須正確前移，不可指向已移除節點。
        parent.Children.RemoveAt(0);
        Assert.Equal(["second", "third", "appended"], parent.Children.Select(c => c.TextContent));
        Assert.Same(second, parent.Children[0]);
        Assert.Null(second.PreviousSibling);

        // RemoveAt 移除尾節點：父節點 LastChild 須正確後移，不可指向已移除節點。
        parent.Children.RemoveAt(parent.Children.Count - 1);
        Assert.Equal(["second", "third"], parent.Children.Select(c => c.TextContent));
        Assert.Same(third, parent.Children[^1]);
        Assert.Null(third.NextSibling);

        // 真機相容性：移除與插入後的最終樹狀結構仍須能完整序列化並由 OdfXmlReader 正確還原。
        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office"))
            .AppendElement(new OfficeTextElement("office"))
            .AppendElement(parent);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;

        TextPElement parsedParent = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<TextPElement>()
            .Single();
        Assert.Equal(["second", "third"], parsedParent.TextSpanChildElements.Select(c => c.TextContent));
    }

    /// <summary>
    /// 驗證 <see cref="OdfNode.ResetModifiedState"/> 會遞迴將節點本身及其所有子節點（含巢狀後代）
    /// 的 <see cref="OdfNode.IsModified"/> 標記重設為 <see langword="false"/>，而不僅是頂層節點。
    /// </summary>
    [Fact]
    public void ResetModifiedStateRecursivelyClearsIsModifiedOnNodeAndAllDescendants()
    {
        TextPElement paragraph = new("text");
        TextSpanElement span = paragraph.AppendElement(new TextSpanElement("text"));
        span.TextContent = "巢狀文字";
        TextSpanElement nestedSpan = span.AppendElement(new TextSpanElement("text"));
        nestedSpan.TextContent = "更深層的文字";

        // AppendChild／SetAttribute／TextContent 設定均會標記 IsModified，新建立的節點樹預期全數為 true。
        Assert.True(paragraph.IsModified);
        Assert.True(span.IsModified);
        Assert.True(nestedSpan.IsModified);

        paragraph.ResetModifiedState();

        Assert.False(paragraph.IsModified);
        Assert.False(span.IsModified);
        Assert.False(nestedSpan.IsModified);
        // 文字節點本身（TextContent 內部建立的 Text 型別子節點）亦屬於子節點樹的一部分，須一併重設。
        Assert.All(nestedSpan.Children, child => Assert.False(child.IsModified));

        // 重設後再次修改，須能正確重新標記為已修改，確認旗標可逆向切換而非被永久鎖定。
        nestedSpan.SetAttribute("style-name", OdfNamespaces.Text, "Emphasis", "text");
        Assert.True(nestedSpan.IsModified);
        Assert.False(paragraph.IsModified);
    }

    /// <summary>
    /// 驗證 <see cref="OdfPercent.FromPercent"/> 由數值建立的百分比，其 lexical form 與
    /// <see cref="OdfPercent.Percent"/> 數值皆正確，並可由原生字串建構子產生的等價值互通比對。
    /// </summary>
    [Fact]
    public void OdfPercentFromPercentProducesEquivalentLexicalFormAndValue()
    {
        OdfPercent fromValue = OdfPercent.FromPercent(42.5m);
        Assert.Equal("42.5%", fromValue.Value);
        Assert.Equal(42.5m, fromValue.Percent);
        Assert.Equal(new OdfPercent("42.5%"), fromValue);

        OdfPercent fromNegative = OdfPercent.FromPercent(-12.5m);
        Assert.Equal(-12.5m, fromNegative.Percent);
        Assert.Equal(new OdfPercent("-12.5%"), fromNegative);

        OdfPercent fromInteger = OdfPercent.FromPercent(100m);
        Assert.Equal("100%", fromInteger.Value);
        Assert.Equal(new OdfPercent("100%"), fromInteger);
    }

    /// <summary>
    /// 驗證 <see cref="OdfDuration.TryGetTimeSpan"/> 可正確將合法 XML Schema duration 字串
    /// （含年／月單位，依 <see cref="System.Xml.XmlConvert.ToTimeSpan(string)"/> 之近似換算規則）
    /// 轉換為 <see cref="TimeSpan"/>；並在以 <see langword="default"/> 建構（<c>Value</c> 為
    /// <see langword="null"/>，繞過建構子驗證）時安全傳回 <see langword="false"/> 而非擲出例外。
    /// </summary>
    [Fact]
    public void OdfDurationTryGetTimeSpanConvertsValidDurationAndHandlesDefaultValueSafely()
    {
        var duration = new OdfDuration("PT1H30M");
        Assert.True(duration.TryGetTimeSpan(out TimeSpan timeSpan));
        Assert.Equal(new TimeSpan(1, 30, 0), timeSpan);

        var negativeDuration = new OdfDuration("-PT15M");
        Assert.True(negativeDuration.TryGetTimeSpan(out TimeSpan negativeTimeSpan));
        Assert.Equal(TimeSpan.FromMinutes(-15), negativeTimeSpan);

        // OdfDuration 建構子已透過 IsValid 驗證 lexical form 必定可由 XmlConvert.ToTimeSpan 換算，
        // 故唯一能繞過驗證、使 Value 為 null 的途徑是 default(OdfDuration)；此時 TryGetTimeSpan
        // 應安全傳回 false，而非讓 XmlConvert 對 null 字串擲出例外。
        OdfDuration defaultDuration = default;
        Assert.False(defaultDuration.TryGetTimeSpan(out TimeSpan fallbackTimeSpan));
        Assert.Equal(default, fallbackTimeSpan);
    }

    /// <summary>
    /// 驗證 <see cref="OdfElement.CloneNode"/> 會一併複製屬性原始命名空間前綴，而非僅複製
    /// 屬性值本身。先前 <c>OdfElement.CloneNode</c> 的覆寫版本直接以
    /// <c>OdfNodeFactory.CreateElement</c> 建立新節點並逐一複製 <c>Attributes</c> 字典，未呼叫
    /// 基底 <see cref="OdfNode.CloneNode"/> 亦未複製前綴記錄；對於未登錄於
    /// <see cref="OdfNamespaces.GetPrefix(string)"/> 標準對映表的命名空間（例如保留原始前綴的
    /// 第三方擴充屬性），複製後的節點在序列化時會遺失原始前綴，改用自動產生的 <c>nsN</c> 佔位
    /// 前綴。由於文件樹中絕大多數節點皆為 <see cref="OdfElement"/> 子類別（透過虛擬呼叫多型
    /// 分派至此覆寫版本），此問題會影響所有透過 <c>CloneNode</c>／<c>ImportNode</c> 的複製路徑
    /// （例如版面拆列、追蹤修訂快照、跨文件匯入）。
    /// </summary>
    [Fact]
    public void OdfElementCloneNodePreservesOriginalAttributeNamespacePrefix()
    {
        const string customNamespaceUri = "urn:example:third-party-extension:1.0";
        TextPElement paragraph = new("text");
        paragraph.SetAttribute("custom-flag", customNamespaceUri, "preserved", "ext");

        OdfNode clone = paragraph.CloneNode(deep: true);

        Assert.IsType<TextPElement>(clone);
        Assert.Equal("preserved", clone.GetAttribute("custom-flag", customNamespaceUri));
        Assert.Equal("ext", clone.GetAttributePrefix(new OdfAttributeName("custom-flag", customNamespaceUri)));

        // 真機相容性：序列化後自訂前綴必須完整保留在輸出 XML 中，而非被改寫成自動產生的 nsN 佔位前綴。
        using MemoryStream stream = new();
        OfficeDocumentContentElement document = new("office");
        document.AppendElement(new OfficeBodyElement("office"))
            .AppendElement(new OfficeTextElement("office"))
            .AppendElement((TextPElement)clone);
        OdfXmlWriter.Write(document, stream, new OdfSaveOptions { IndentXml = false });
        stream.Position = 0;
        string xml = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("ext:custom-flag=\"preserved\"", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("ns1:custom-flag", xml, StringComparison.Ordinal);

        TextPElement parsedParagraph = Assert.IsType<OfficeDocumentContentElement>(OdfXmlReader.Parse(stream))
            .DescendantElements<TextPElement>()
            .Single();
        Assert.Equal("preserved", parsedParagraph.GetAttribute("custom-flag", customNamespaceUri));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OdfKit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到 repository root。");
    }
}
