using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using OriginLab.DocumentGeneration.Transformers;

namespace OriginLab.DocumentGeneration.Tests;

public class TransformerTests
{
    [Fact]
    public void CleanedDocumentContainsDocType()
    {
        var document = CreateDocument("<html></html>");

        DocumentTransformer.CleanUp(document);

        Assert.NotNull(document.Doctype);
    }

    [Fact]
    public void CleanedDocumentContainsTitle()
    {
        var document = CreateDocument("""
            <h1>test</h1>
            """);

        DocumentTransformer.CleanUp(document);

        Assert.Equal("test", document.Title);
    }

    [Fact]
    public void CleanedDocumentRemovesEditLinks()
    {
        var document = CreateDocument("""
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
        var document = CreateDocument(html);
        DocumentTransformer.CleanUp(document);

        Assert.NotNull(document.Body);
        Assert.Equal("", document.Body.InnerHtml);
    }

    [Fact]
    public void GetsPageTitleFromTheFirstH1()
    {
        Assert.Equal("App A", DocumentTransformer.GetPageTitle(Path.GetFullPath("../../../../converter/tests/books/app/en/A/App/A.html", AppContext.BaseDirectory)));
    }

    [Fact]
    public void ResolveResolvableAnchors()
    {
        var resolver = ResourceResolverTests.CreateResolver("app", false, out var args);
        var transformer = new DocumentTransformer(resolver, null!, new ProblemRecorder(args));
        var document = CreateDocument("""
            <a id="remain" href="../App/B.html">haha</a>
            """);

        resolver.Language = "en";
        transformer.Transform(document, Path.GetFullPath("en/A/App/A.html", args.SourceFolder));

        var anchor = document.QuerySelector("a#remain");
        Assert.NotNull(anchor);
        Assert.Equal("/app/b/", anchor.GetAttribute("href"));
    }

    [Fact]
    public void UnresolvableAnchorsRemain()
    {
        var resolver = ResourceResolverTests.CreateResolver("app", false, out var args);
        var transformer = new DocumentTransformer(resolver, null!, new ProblemRecorder(args));
        var document = CreateDocument("""
            <a id="remain" href="../things/never/resolvable.html">haha</a>
            """);

        resolver.Language = "en";
        transformer.Transform(document, Path.GetFullPath("en/A/App/A.html", args.SourceFolder));

        var anchor = document.QuerySelector("a#remain");
        Assert.NotNull(anchor);
        Assert.Equal("../things/never/resolvable.html", anchor.GetAttribute("href"));
    }

    private static IHtmlDocument CreateDocument(string source)
    {
        var parser = new HtmlParser(new HtmlParserOptions { IsKeepingSourceReferences = true });
        return parser.ParseDocument(source);
    }
}
