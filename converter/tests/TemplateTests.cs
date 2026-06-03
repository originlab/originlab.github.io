using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration.Tests;

public class TemplateTests
{
    [Theory]
    [InlineData("de")]
    [InlineData("ja")]
    [InlineData("zh")]
    public async Task NoEntitiesInThe404PageTitles(string language)
    {
        var html = await Template.Render404PageAsync(language);

        Assert.Matches(@"<title>[^&#]*?</title>", html);
    }
}
