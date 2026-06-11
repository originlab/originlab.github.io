namespace OriginLab.DocumentGeneration.Tests;

public class ResourceResolverTests
{
    [Theory]
    [InlineData("./A/Category/App(App).html", "en", "/app/", "Apps")]
    [InlineData("./A/Category/App(App).html", "de", "/app/de/", "Apps")]
    [InlineData("./A/App/A.html", "en", "/app/a/", "App A")]
    [InlineData("./A/App/B.html", "de", "/app/b/de/", "App B")]
    [InlineData("./A/App/B_Script.html", "de", "/build/b-scripts/de/", "B Scripts (Moved from App)")]
    [InlineData("/link.aspx?a=b#h", "de", "/link.aspx?a=b#h", null)]
    [InlineData("http://localhost/link.aspx?a=b", "de", "http://localhost/link.aspx?a=b", null)]
    [InlineData("mailto:a@b.lan", "de", "mailto:a@b.lan", null)]
    public void HrefShouldResolveCorrectly(string href, string language, string expectedUrl, string? expectedTitle)
    {
        var resolver = CreateResolver("app", false, out var args);
        resolver.Language = language;

        Assert.True(resolver.TryResolveHref(href, Path.GetFullPath(language, args.SourceFolder), out var result, out var title));
        Assert.Equal(expectedUrl, result);
        Assert.Equal(expectedTitle, title);
    }

    [Theory]
    // Image exists in en
    [InlineData("../images/A/a.jpg", "en", false, "/app/en/images/A/a.jpg?v=2a44c9a534ff6b5c")]
    [InlineData("../images/A/a.jpg", "en", true, "/app/en/images/A/a.webp?v=2a44c9a534ff6b5c")]
    [InlineData("../images/A/a.jpg?v=123", "en", false, "/app/en/images/A/a.jpg?v=2a44c9a534ff6b5c")]
    [InlineData("../images/A/a.jpg?v=123", "en", true, "/app/en/images/A/a.webp?v=2a44c9a534ff6b5c")]
    // Image only exists in ja
    [InlineData("../images/A/b.jpg", "ja", false, "/app/ja/images/A/b.jpg?v=e14a4892ecf11d7a")]
    [InlineData("../images/A/b.jpg", "ja", true, "/app/ja/images/A/b.webp?v=e14a4892ecf11d7a")]
    [InlineData("../images/A/b.jpg?v=123", "ja", false, "/app/ja/images/A/b.jpg?v=e14a4892ecf11d7a")]
    [InlineData("../images/A/b.jpg?v=123", "ja", true, "/app/ja/images/A/b.webp?v=e14a4892ecf11d7a")]
    // Shared images
    [InlineData("../images/A/Mini_bulb.png", "en", false, "/books/images/Mini_bulb.png?v=4b448e7cb64a0292", false)]
    [InlineData("../images/A/Mini_bulb.png", "de", false, "/books/images/Mini_bulb.png?v=4b448e7cb64a0292", false)]
    [InlineData("../images/A/Mini_bulb.png", "de", true, "/books/images/Mini_bulb.webp?v=4b448e7cb64a0292", false)]
    [InlineData("../images/A/Mini_bulb.png?v=123", "de", false, "/books/images/Mini_bulb.png?v=4b448e7cb64a0292", false)]
    [InlineData("../images/A/Mini_bulb.png?v=123", "de", true, "/books/images/Mini_bulb.webp?v=4b448e7cb64a0292", false)]
    public void SrcShouldResolveCorrectly(string src, string language, bool useWebp, string expectedUrl, bool needsCopy = true)
    {
        var resolver = CreateResolver("app", useWebp, out var args);
        resolver.Language = language;

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, language, "A/App"), out var result, out var copy));
        Assert.Equal(expectedUrl, result);

        if (needsCopy)
        {
            Assert.NotNull(copy);
        }
        else
        {
            Assert.Null(copy);
        }

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, language, "A/App"), out result, out copy));
        Assert.Equal(expectedUrl, result);
        Assert.Null(copy);
    }

    [Theory]
    // Image not exist in de but found in en
    [InlineData("../images/A/a.jpg", "de", false, "/app/en/images/A/a.jpg?v=2a44c9a534ff6b5c")]
    [InlineData("../images/A/a.jpg", "de", true, "/app/en/images/A/a.webp?v=2a44c9a534ff6b5c")]
    [InlineData("../images/A/a.jpg?v=123", "de", false, "/app/en/images/A/a.jpg?v=2a44c9a534ff6b5c")]
    [InlineData("../images/A/a.jpg?v=123", "de", true, "/app/en/images/A/a.webp?v=2a44c9a534ff6b5c")]
    // Image exists in ja but found in en
    [InlineData("../images/A/a.jpg", "ja", false, "/app/en/images/A/a.jpg?v=2a44c9a534ff6b5c")]
    [InlineData("../images/A/a.jpg", "ja", true, "/app/en/images/A/a.webp?v=2a44c9a534ff6b5c")]
    [InlineData("../images/A/a.jpg?v=123", "ja", false, "/app/en/images/A/a.jpg?v=2a44c9a534ff6b5c")]
    [InlineData("../images/A/a.jpg?v=123", "ja", true, "/app/en/images/A/a.webp?v=2a44c9a534ff6b5c")]
    public void SrcShouldNotBeCopiedWhenFoundInEn(string src, string language, bool useWebp, string expectedUrl)
    {
        var resolver = CreateResolver("app", useWebp, out var args);

        // The transformer always process en first, so the pic is seen

        resolver.Language = "en";

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, "en/A/App"), out var result, out var copy));
        Assert.Equal(expectedUrl, result);
        Assert.NotNull(copy);

        // Now it process the other language

        resolver.Language = language;

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, language, "A/App"), out result, out copy));
        Assert.Equal(expectedUrl, result);
        Assert.Null(copy);
    }

    private static DocToStaticPagesResourceResolver CreateResolver(string book, bool useWebp, out DocToStaticPagesTransformerArgs args)
    {
        var bookDir = Path.GetFullPath("../../../../converter/tests/books/" + book, AppContext.BaseDirectory);

        args = new DocToStaticPagesTransformerArgs
        {
            BookUrlName = book,
            BooksXmlFolder = Path.GetFullPath("../xml", bookDir),
            SourceFolder = bookDir,
            OutputFolder = Path.GetFullPath("../../../../artifacts/tests/public_html/" + book, AppContext.BaseDirectory),
            SharedImagesFolder = Path.GetFullPath("../images", bookDir),
            UseWebp = useWebp,
        };

        return new DocToStaticPagesResourceResolver(args);
    }
}
