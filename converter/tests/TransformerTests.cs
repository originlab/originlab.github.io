using AngleSharp.Html.Parser;
using OriginLab.DocumentGeneration.Transformers;

namespace OriginLab.DocumentGeneration.Tests;

public class TransformerTests
{
    [Fact]
    public void CleanedDocumentContainsDocType()
    {
        var document = new HtmlParser().ParseDocument("<html></html>");

        DocumentTransformer.CleanUp(document);

        Assert.NotNull(document.Doctype);
    }

    [Fact]
    public void CleanedDocumentContainsTitle()
    {
        var document = new HtmlParser().ParseDocument("""
            <h1>test</h1>
            """);

        DocumentTransformer.CleanUp(document);

        Assert.Equal("test", document.Title);
    }

    [Fact]
    public void CleanedDocumentRemovesEditLinks()
    {
        var document = new HtmlParser().ParseDocument("""
            <span class="mw-editsection"><span class="mw-editsection-bracket">[</span><a href="..." title="Edit section: Categories">edit</a><span class="mw-editsection-bracket">]</span></span>
            """);

        DocumentTransformer.CleanUp(document);

        Assert.NotNull(document.Body);
        Assert.Equal("", document.Body.InnerHtml);
    }

    [Theory]
    [InlineData("""<p class="hierarchy">abc</p>""")]
    [InlineData("""<p class="urlname">abc</p>""")]
    public void CleanedDocumentRemovesTagsWithMetadata(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        DocumentTransformer.CleanUp(document);

        Assert.NotNull(document.Body);
        Assert.Equal("", document.Body.InnerHtml);
    }

    [Fact]
    public void GetsPageTitleFromTheFirstH1()
    {
        Assert.Equal("App A", DocumentTransformer.GetPageTitle(Path.GetFullPath("../../../../converter/tests/books/app/en/A/App/A.html", AppContext.BaseDirectory)));
    }
}
