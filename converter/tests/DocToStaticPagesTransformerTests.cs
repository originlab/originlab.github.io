using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration.Tests;

public class DocToStaticPagesTransformerTests
{
    [Theory]
    [InlineData("./GettingStarted/Category/Origin_9.1_Getting_Started_Booklet.html", "en", "/user-guide/", "User Guide")]
    [InlineData("./GettingStarted/Category/Origin_9.1_Getting_Started_Booklet.html", "de", "/user-guide/de/", "User Guide")]
    public async Task HrefShouldResolveCorrectly(string href, string language, string expectedUrl, string expectedTitle)
    {
        var bookDir = Path.GetFullPath("../../../index", Template.WebRootPath);
        var args = new DocToStaticPagesTransformerArgs
        {
            BookUrlName = "",
            BooksXmlFolder = Path.GetFullPath("../../../books", Template.WebRootPath),
            SourceFolder = bookDir,
            OutputFolder = Path.GetFullPath("../../../artifacts/tests/public_html", Template.WebRootPath),
        };
        var transformer = new FakeDocToStaticPagesTransformer(args, new ProblemRecorder(args));

        await transformer.InitializeLanguageLayoutAsync(language);

        Assert.True(transformer.TryResolveHref(href, Path.GetFullPath(language, bookDir), out var result, out var title));
        Assert.Equal(expectedUrl, result);
        Assert.Equal(expectedTitle, title);
    }
}
