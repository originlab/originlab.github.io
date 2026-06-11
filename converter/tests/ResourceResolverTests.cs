namespace OriginLab.DocumentGeneration.Tests;

public class ResourceResolverTests
{
    [Theory]
    [InlineData("./A/Category/App(App).html", "app", "en", "/app/", "Apps")]
    [InlineData("./A/Category/App(App).html", "app", "de", "/app/de/", "Apps")]
    [InlineData("./A/App/A.html", "app", "en", "/app/a/", "App A")]
    [InlineData("./A/App/B.html", "app", "de", "/app/b/de/", "App B")]
    [InlineData("./A/App/B_Script.html", "app", "de", "/build/b-scripts/de/", "B Scripts (Moved from App)")]
    [InlineData("/link.aspx?a=b#h", "app", "de", "/link.aspx?a=b#h", null)]
    [InlineData("http://localhost/link.aspx?a=b", "app", "de", "http://localhost/link.aspx?a=b", null)]
    [InlineData("mailto:a@b.lan", "app", "de", "mailto:a@b.lan", null)]
    public void HrefShouldResolveCorrectly(string href, string book, string language, string expectedUrl, string? expectedTitle)
    {
        var bookDir = Path.GetFullPath("../../../../converter/tests/books/" + book, AppContext.BaseDirectory);
        var args = new DocToStaticPagesTransformerArgs
        {
            BookUrlName = book,
            BooksXmlFolder = Path.GetFullPath("../xml", bookDir),
            SourceFolder = bookDir,
            OutputFolder = Path.GetFullPath("../../../../artifacts/tests/public_html/" + book, AppContext.BaseDirectory),
        };

        var resolver = new DocToStaticPagesResourceResolver(args)
        {
            Language = language
        };

        Assert.True(resolver.TryResolveHref(href, Path.GetFullPath(language, bookDir), out var result, out var title));
        Assert.Equal(expectedUrl, result);
        Assert.Equal(expectedTitle, title);
    }

    [Theory]
    [InlineData("../images/A/a.jpg", "app", "en", "A/App", false, "/app/en/images/A/a.jpg")]
    [InlineData("../images/A/a.jpg?v=123", "app", "en", "A/App", false, "/app/en/images/A/a.jpg?v=123")]
    [InlineData("../images/A/a.jpg?v=123", "app", "en", "A/App", true, "/app/en/images/A/a.webp?v=123")]
    [InlineData("../images/A/a.jpg?v=123", "app", "de", "A/App", true, "/app/en/images/A/a.webp?v=123")]
    public void SrcShouldResolveCorrectly(string src, string book, string language, string sourceDir, bool useWebp, string expectedUrl)
    {
        var bookDir = Path.GetFullPath("../../../../converter/tests/books/" + book, AppContext.BaseDirectory);
        var args = new DocToStaticPagesTransformerArgs
        {
            BookUrlName = book,
            BooksXmlFolder = Path.GetFullPath("../xml", bookDir),
            SourceFolder = bookDir,
            OutputFolder = Path.GetFullPath("../../../../artifacts/tests/public_html/" + book, AppContext.BaseDirectory),
            UseWebp = useWebp,
        };

        var resolver = new DocToStaticPagesResourceResolver(args)
        {
            Language = language
        };

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(bookDir, language, sourceDir), out var result, out _));
        Assert.Equal(expectedUrl, result);
    }
}
