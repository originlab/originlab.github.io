namespace OriginLab.DocumentGeneration.Tests;

public class DocToStaticPagesTransformerTests
{
    [Theory]
    [InlineData("./A/Category/App(App).html", "app", "en", "/app/", "Apps")]
    [InlineData("./A/Category/App(App).html", "app", "de", "/app/de/", "Apps")]
    [InlineData("./A/App/A.html", "app", "en", "/app/a/", "App A")]
    [InlineData("./A/App/B.html", "app", "de", "/app/b/de/", "App B")]
    [InlineData("./A/App/B_Script.html", "app", "de", "/build/b-scripts/de/", "B Scripts (Moved from App)")]
    public async Task HrefShouldResolveCorrectly(string href, string book, string language, string expectedUrl, string expectedTitle)
    {
        var bookDir = Path.GetFullPath("../../../../converter/tests/books/" + book, AppContext.BaseDirectory);
        var args = new DocToStaticPagesTransformerArgs
        {
            BookUrlName = "",
            BooksXmlFolder = Path.GetFullPath("../xml", bookDir),
            SourceFolder = bookDir,
            OutputFolder = Path.GetFullPath("../../../../artifacts/tests/public_html" + book, AppContext.BaseDirectory),
        };
        var transformer = new FakeDocToStaticPagesTransformer(args, new ProblemRecorder(args));

        await transformer.InitializeLanguageLayoutAsync(language);

        Assert.True(transformer.TryResolveHref(href, Path.GetFullPath(language, bookDir), out var result, out var title));
        Assert.Equal(expectedUrl, result);
        Assert.Equal(expectedTitle, title);
    }
}
