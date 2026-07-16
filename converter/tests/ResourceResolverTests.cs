using OriginLab.DocumentGeneration.Resolvers;

namespace OriginLab.DocumentGeneration.Tests;

public class ResourceResolverTests
{
    [Theory]
    [InlineData("./A/Category/App(App).html", "en", "/app/", "Apps")]
    [InlineData("./A/Category/App(App).html", "de", "/app/de/", "Apps")]
    [InlineData("./A/App/A.html", "en", "/app/a/", "App A")]
    [InlineData("./A/App/B.html", "de", "/app/b/de/", "App B")]
    [InlineData("./A/App/B_Script.html", "de", "/build/b-scripts/de/", "B Scripts (Moved from App)")]
    [InlineData("./A/App/B_Script.html#section", "de", "/build/b-scripts/de/#section", "B Scripts (Moved from App)")]
    public void HrefShouldResolveCorrectly(string href, string language, string expectedUrl, string? expectedTitle)
    {
        var resolver = CreateResolver("app", false, out var args);
        resolver.Language = language;

        Assert.True(resolver.TryResolveHref(href, Path.Combine(args.SourceFolder, language, "A/App/A.html"), out var result, out var title));
        Assert.Equal(expectedUrl, result);
        Assert.Equal(expectedTitle, title);
    }

    [Theory]
    [InlineData("/link.aspx?a=b#h", "en")]
    [InlineData("/link.aspx?a=b#h", "de")]
    [InlineData("http://localhost/link.aspx?a=b", "en")]
    [InlineData("http://localhost/link.aspx?a=b", "de")]
    [InlineData("mailto:a@b.lan", "en")]
    [InlineData("mailto:a@b.lan", "de")]
    [InlineData("#section", "en")]
    [InlineData("#section", "de")]
    public void HrefShouldLeaveAbosoluteUrlsAsTheyAre(string href, string language)
    {
        var resolver = CreateResolver("app", false, out var args);
        resolver.Language = language;

        Assert.True(resolver.TryResolveHref(href, Path.Combine(args.SourceFolder, language, "A/App/A.html"), out var result, out var title));
        Assert.Equal(href, result);
        Assert.Null(title);
    }

    [Theory]
    // Image exists in en
    [InlineData("../images/A/a.jpg", "en", false, "images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData("../images/A/a.jpg", "en", true, "images/a.webp?v=KkTJpTT_a1w")]
    [InlineData("../images/A/a.jpg?v=123", "en", false, "images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData("../images/A/a.jpg?v=123", "en", true, "images/a.webp?v=KkTJpTT_a1w")]
    // Image only exists in ja
    [InlineData("../images/A/ja_only.jpg", "ja", false, "images/ja_only.jpg?v=4UpIkuzxHXo")]
    [InlineData("../images/A/ja_only.jpg", "ja", true, "images/ja_only.webp?v=4UpIkuzxHXo")]
    [InlineData("../images/A/ja_only.jpg?v=123", "ja", false, "images/ja_only.jpg?v=4UpIkuzxHXo")]
    [InlineData("../images/A/ja_only.jpg?v=123", "ja", true, "images/ja_only.webp?v=4UpIkuzxHXo")]
    // Shared images
    [InlineData("../images/A/Mini_bulb.png", "en", false, "/books/images/Mini_bulb.png?v=S0SOfLZKApI", false)]
    [InlineData("../images/A/Mini_bulb.png", "de", false, "/books/images/Mini_bulb.png?v=S0SOfLZKApI", false)]
    [InlineData("../images/A/Mini_bulb.png", "de", true, "/books/images/Mini_bulb.webp?v=S0SOfLZKApI", false)]
    [InlineData("../images/A/Mini_bulb.png?v=123", "de", false, "/books/images/Mini_bulb.png?v=S0SOfLZKApI", false)]
    [InlineData("../images/A/Mini_bulb.png?v=123", "de", true, "/books/images/Mini_bulb.webp?v=S0SOfLZKApI", false)]
    public void SrcShouldResolveCorrectly(string src, string language, bool useWebp, string expectedUrl, bool needsCopy = true)
    {
        var resolver = CreateResolver("app", useWebp, out var args);
        resolver.Language = language;

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, language, "A/App/A.html"), out var result, out var copy));
        Assert.Equal(expectedUrl, result);

        if (needsCopy)
        {
            Assert.NotNull(copy);
        }
        else
        {
            Assert.Null(copy);
        }

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, language, "A/App/A.html"), out result, out copy));
        Assert.Equal(expectedUrl, result);
        Assert.Null(copy);
    }

    [Theory]
    // Image exists in en
    [InlineData(@"..\images\A\a.jpg", "en", false, "images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\a.jpg", "en", true, "images/a.webp?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\a.jpg?v=123", "en", false, "images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\a.jpg?v=123", "en", true, "images/a.webp?v=KkTJpTT_a1w")]
    // Image only exists in ja
    [InlineData(@"..\images\A\ja_only.jpg", "ja", false, "images/ja_only.jpg?v=4UpIkuzxHXo")]
    [InlineData(@"..\images\A\ja_only.jpg", "ja", true, "images/ja_only.webp?v=4UpIkuzxHXo")]
    [InlineData(@"..\images\A\ja_only.jpg?v=123", "ja", false, "images/ja_only.jpg?v=4UpIkuzxHXo")]
    [InlineData(@"..\images\A\ja_only.jpg?v=123", "ja", true, "images/ja_only.webp?v=4UpIkuzxHXo")]
    // Shared images
    [InlineData(@"..\images\A\Mini_bulb.png", "en", false, "/books/images/Mini_bulb.png?v=S0SOfLZKApI", false)]
    [InlineData(@"..\images\A\Mini_bulb.png", "de", false, "/books/images/Mini_bulb.png?v=S0SOfLZKApI", false)]
    [InlineData(@"..\images\A\Mini_bulb.png", "de", true, "/books/images/Mini_bulb.webp?v=S0SOfLZKApI", false)]
    [InlineData(@"..\images\A\Mini_bulb.png?v=123", "de", false, "/books/images/Mini_bulb.png?v=S0SOfLZKApI", false)]
    [InlineData(@"..\images\A\Mini_bulb.png?v=123", "de", true, "/books/images/Mini_bulb.webp?v=S0SOfLZKApI", false)]
    public void SrcShouldResolveCorrectly_BackSlashes(string src, string language, bool useWebp, string expectedUrl, bool needsCopy = true)
    {
        var resolver = CreateResolver("app", useWebp, out var args);
        resolver.Language = language;

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, language, "A/App/A.html"), out var result, out var copy));
        Assert.Equal(expectedUrl, result);

        if (needsCopy)
        {
            Assert.NotNull(copy);
        }
        else
        {
            Assert.Null(copy);
        }

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, language, "A/App/A.html"), out result, out copy));
        Assert.Equal(expectedUrl, result);
        Assert.Null(copy);
    }

    [Theory]
    // Image not exist in de but found in en
    [InlineData("../images/A/a.jpg", "de", false, "../images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData("../images/A/a.jpg", "de", true, "../images/a.webp?v=KkTJpTT_a1w")]
    [InlineData("../images/A/a.jpg?v=123", "de", false, "../images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData("../images/A/a.jpg?v=123", "de", true, "../images/a.webp?v=KkTJpTT_a1w")]
    // Image exists in ja but also found in en
    [InlineData("../images/A/a.jpg", "ja", false, "../images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData("../images/A/a.jpg", "ja", true, "../images/a.webp?v=KkTJpTT_a1w")]
    [InlineData("../images/A/a.jpg?v=123", "ja", false, "../images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData("../images/A/a.jpg?v=123", "ja", true, "../images/a.webp?v=KkTJpTT_a1w")]
    public void SrcShouldNotBeCopiedWhenFoundInEn(string src, string language, bool useWebp, string expectedUrl)
    {
        var resolver = CreateResolver("app", useWebp, out var args);

        // The transformer always process en first, so the pic is seen

        resolver.Language = "en";

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, "en/A/App/A.html"), out var result, out var copy));
        Assert.Equal(expectedUrl.AsSpan("../".Length), result);
        Assert.NotNull(copy);

        // Now it process the other language

        resolver.Language = language;

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, language, "A/App/A.html"), out result, out copy));
        Assert.Equal(expectedUrl, result);
        Assert.Null(copy);
    }

    [Theory]
    // Image not exist in de but found in en
    [InlineData(@"..\images\A\a.jpg", "de", false, "../images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\a.jpg", "de", true, "../images/a.webp?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\a.jpg?v=123", "de", false, "../images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\a.jpg?v=123", "de", true, "../images/a.webp?v=KkTJpTT_a1w")]
    // Image exists in ja but also found in en
    [InlineData(@"..\images\A\a.jpg", "ja", false, "../images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\a.jpg", "ja", true, "../images/a.webp?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\a.jpg?v=123", "ja", false, "../images/a.jpg?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\a.jpg?v=123", "ja", true, "../images/a.webp?v=KkTJpTT_a1w")]
    public void SrcShouldNotBeCopiedWhenFoundInEn_BackSlashes(string src, string language, bool useWebp, string expectedUrl)
    {
        var resolver = CreateResolver("app", useWebp, out var args);

        // The transformer always process en first, so the pic is seen

        resolver.Language = "en";

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, "en/A/App/A.html"), out var result, out var copy));
        Assert.Equal(expectedUrl.AsSpan("../".Length), result);
        Assert.NotNull(copy);

        // Now it process the other language

        resolver.Language = language;

        Assert.True(resolver.TryResolveSrc(src, Path.Combine(args.SourceFolder, language, "A/App/A.html"), out result, out copy));
        Assert.Equal(expectedUrl, result);
        Assert.Null(copy);
    }

    internal static DocsResourceGithubPagesResolver CreateResolver(string book, bool useWebp, out DocsToStaticPagesTransformationArgs args)
    {
        var bookDir = Path.GetFullPath("../../../../converter/tests/books/" + book, AppContext.BaseDirectory);

        args = new DocsToStaticPagesTransformationArgs
        {
            BaseUrl = book,
            BooksXmlFolder = Path.GetFullPath("../xml", bookDir),
            SourceFolder = bookDir,
            OutputFolder = Path.GetFullPath("../../../../artifacts/tests/public_html/" + book, AppContext.BaseDirectory),
            SharedImagesFolder = Path.GetFullPath("../images", bookDir),
            UseWebp = useWebp,
        };

        return new DocsResourceGithubPagesResolver(args);
    }
}
