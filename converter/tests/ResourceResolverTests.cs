using OriginLab.DocumentGeneration.Resolvers;

namespace OriginLab.DocumentGeneration.Tests;

public class ResourceResolverTests
{
    [Fact]
    public void GetsPageTitleFromTheFirstH1()
    {
        Assert.Equal("App A", DocsResourceResolver.ReadPageTitle(Path.GetFullPath("../../../../converter/tests/books/app/en/A/App/A.html", AppContext.BaseDirectory)));
    }

    [Theory]
    [InlineData("./A/Category/App(App).html", "en", "/app/")]
    [InlineData("./A/Category/App(App).html", "ja", "/app/ja/")]
    [InlineData("./A/App/A.html", "en", "/app/a/")]
    [InlineData("./A/App/B.html", "ja", "/app/b/ja/")]
    [InlineData("./A/App/B_Script.html", "ja", "/build/b-scripts/ja/")]
    [InlineData("./A/App/B_Script.html#section", "ja", "/build/b-scripts/ja/#section")]
    public void HrefShouldResolveCorrectly(string href, string language, string expectedUrl)
    {
        var resolver = CreateResolver("app", false, out var args);
        resolver.Language = language;

        Assert.True(resolver.TryResolveHref(href, Path.Combine(args.SourceFolder, language, "A/App/A.html"), out var result));
        Assert.Equal(expectedUrl, result);
    }

    [Theory]
    [InlineData("/link.aspx?a=b#h", "en")]
    [InlineData("/link.aspx?a=b#h", "ja")]
    [InlineData("http://localhost/link.aspx?a=b", "en")]
    [InlineData("http://localhost/link.aspx?a=b", "ja")]
    [InlineData("mailto:a@b.lan", "en")]
    [InlineData("mailto:a@b.lan", "ja")]
    [InlineData("#section", "en")]
    [InlineData("#section", "ja")]
    public void HrefShouldLeaveAbosoluteUrlsAsTheyAre(string href, string language)
    {
        var resolver = CreateResolver("app", false, out var args);
        resolver.Language = language;

        Assert.True(resolver.TryResolveHref(href, Path.Combine(args.SourceFolder, language, "A/App/A.html"), out var result));
        Assert.Equal(href, result);
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
    [InlineData("../images/A/Mini_bulb.png", "ja", false, "/books/images/Mini_bulb.png?v=S0SOfLZKApI", false)]
    [InlineData("../images/A/Mini_bulb.png", "ja", true, "/books/images/Mini_bulb.webp?v=S0SOfLZKApI", false)]
    [InlineData("../images/A/Mini_bulb.png?v=123", "ja", false, "/books/images/Mini_bulb.png?v=S0SOfLZKApI", false)]
    [InlineData("../images/A/Mini_bulb.png?v=123", "ja", true, "/books/images/Mini_bulb.webp?v=S0SOfLZKApI", false)]
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
    [InlineData(@"..\images\A\Mini_bulb.png", "ja", false, "/books/images/Mini_bulb.png?v=S0SOfLZKApI", false)]
    [InlineData(@"..\images\A\Mini_bulb.png", "ja", true, "/books/images/Mini_bulb.webp?v=S0SOfLZKApI", false)]
    [InlineData(@"..\images\A\Mini_bulb.png?v=123", "ja", false, "/books/images/Mini_bulb.png?v=S0SOfLZKApI", false)]
    [InlineData(@"..\images\A\Mini_bulb.png?v=123", "ja", true, "/books/images/Mini_bulb.webp?v=S0SOfLZKApI", false)]
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
    // Image not exist in ja but found in en
    [InlineData("../images/A/en_only.jpg", "ja", false, "../images/en_only.jpg?v=KkTJpTT_a1w")]
    [InlineData("../images/A/en_only.jpg", "ja", true, "../images/en_only.webp?v=KkTJpTT_a1w")]
    [InlineData("../images/A/en_only.jpg?v=123", "ja", false, "../images/en_only.jpg?v=KkTJpTT_a1w")]
    [InlineData("../images/A/en_only.jpg?v=123", "ja", true, "../images/en_only.webp?v=KkTJpTT_a1w")]
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

    [Fact]
    public void SrcImageSharedByMultipleEnglishPages()
    {
        var resolver = CreateResolver("app", false, out var args);
        resolver.Language = "en";

        Assert.True(resolver.TryResolveSrc("../images/A/a.jpg", Path.Combine(args.SourceFolder, "en/A/App/A.html"), out var result, out var copy));
        Assert.Equal("images/a.jpg?v=KkTJpTT_a1w", result);
        Assert.NotNull(copy);

        // Now it process another page

        Assert.True(resolver.TryResolveSrc("../images/B/a.jpg", Path.Combine(args.SourceFolder, "en/A/App/B.html"), out result, out copy));
        Assert.Equal("/app/a/images/a.jpg?v=KkTJpTT_a1w", result);
        Assert.Null(copy);
    }

    [Fact]
    public void SrcImageNotExists_ButFoundInEn_DiffPage()
    {
        var resolver = CreateResolver("app", false, out var args);

        // The transformer always process en first, so the pic is seen

        resolver.Language = "en";

        Assert.True(resolver.TryResolveSrc("../images/A/en_only.jpg", Path.Combine(args.SourceFolder, "en/A/App/A.html"), out var result, out var copy));
        Assert.Equal("images/en_only.jpg?v=KkTJpTT_a1w", result);
        Assert.NotNull(copy);

        // Now it process the other language

        resolver.Language = "ja";

        Assert.True(resolver.TryResolveSrc("../images/B/en_only.jpg", Path.Combine(args.SourceFolder, resolver.Language, "A/App/B.html"), out result, out copy));
        Assert.Equal("/app/a/images/en_only.jpg?v=KkTJpTT_a1w", result);
        Assert.Null(copy);
    }

    [Fact]
    public void SrcImageNotExists_ButFoundInEn_DiffPage2()
    {
        var resolver = CreateResolver("app", false, out var args);

        // The transformer always process en first, so the pic is seen

        resolver.Language = "en";

        Assert.True(resolver.TryResolveSrc("../images/A/en_only.jpg", Path.Combine(args.SourceFolder, "en/A/App/A.html"), out var result, out var copy));
        Assert.Equal("images/en_only.jpg?v=KkTJpTT_a1w", result);
        Assert.NotNull(copy);

        // Now it process the other language

        resolver.Language = "ja";

        Assert.True(resolver.TryResolveSrc("../images/B/en_only.jpg", Path.Combine(args.SourceFolder, resolver.Language, "A/App/B.html"), out result, out copy));
        Assert.Equal("/app/a/images/en_only.jpg?v=KkTJpTT_a1w", result);
        Assert.Null(copy);

        // The pic appears the second time

        Assert.True(resolver.TryResolveSrc("../images/B/en_only.jpg", Path.Combine(args.SourceFolder, resolver.Language, "A/App/B.html"), out result, out copy));
        Assert.Equal("/app/a/images/en_only.jpg?v=KkTJpTT_a1w", result);
        Assert.Null(copy);
    }

    [Fact]
    public void SrcImageExists_AlsoFoundInEn_DiffPage2()
    {
        var resolver = CreateResolver("app", false, out var args);

        // The transformer always process en first, so the pic is seen

        resolver.Language = "en";

        Assert.True(resolver.TryResolveSrc("../images/A/a.jpg", Path.Combine(args.SourceFolder, "en/A/App/A.html"), out var result, out var copy));
        Assert.Equal("images/a.jpg?v=KkTJpTT_a1w", result);
        Assert.NotNull(copy);

        // Now it process the other language

        resolver.Language = "ja";

        Assert.True(resolver.TryResolveSrc("../images/B/a.jpg", Path.Combine(args.SourceFolder, "ja/A/App/B.html"), out result, out copy));
        Assert.Equal("/app/a/images/a.jpg?v=KkTJpTT_a1w", result);
        Assert.Null(copy);

        // The pic appears the second time

        Assert.True(resolver.TryResolveSrc("../images/B/a.jpg", Path.Combine(args.SourceFolder, "ja/A/App/B.html"), out result, out copy));
        Assert.Equal("/app/a/images/a.jpg?v=KkTJpTT_a1w", result);
        Assert.Null(copy);
    }

    [Fact]
    public void SrcImageExists_NotFoundInEn_SamePage()
    {
        var resolver = CreateResolver("app", false, out var args);

        // Skip en, so the en pic is not seen

        resolver.Language = "ja";

        Assert.True(resolver.TryResolveSrc("../images/A/a.jpg", Path.Combine(args.SourceFolder, "ja/A/App/A.html"), out var result, out var copy));
        Assert.Equal("images/a.jpg?v=KkTJpTT_a1w", result);
        Assert.NotNull(copy);

        Assert.True(resolver.TryResolveSrc("../images/A/a.jpg", Path.Combine(args.SourceFolder, "ja/A/App/A.html"), out result, out copy));
        Assert.Equal("images/a.jpg?v=KkTJpTT_a1w", result);
        Assert.Null(copy);
    }

    [Theory]
    // Image not exist in ja but found in en
    [InlineData(@"..\images\A\en_only.jpg", "ja", false, "../images/en_only.jpg?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\en_only.jpg", "ja", true, "../images/en_only.webp?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\en_only.jpg?v=123", "ja", false, "../images/en_only.jpg?v=KkTJpTT_a1w")]
    [InlineData(@"..\images\A\en_only.jpg?v=123", "ja", true, "../images/en_only.webp?v=KkTJpTT_a1w")]
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
