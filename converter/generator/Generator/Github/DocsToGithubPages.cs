using OriginLab.DocumentGeneration.Templates;
using OriginLab.DocumentGeneration.Transformers;

namespace OriginLab.DocumentGeneration.Generator.Github;

abstract class DocsToGithubPages : DocsGenerator
{
    protected string BaseUrl { get; }

    protected DocsToGithubPages(string baseUrl, string sourceFolder, string outputFolder, DocumentTransformer transformer, ProblemRecorder problems)
        : base(sourceFolder, outputFolder, transformer, problems)
    {
        BaseUrl = baseUrl;
    }

    protected override async Task TransformFilesAsync()
    {
        await base.TransformFilesAsync();

        File.WriteAllText(Path.Combine(OutputFolder, "404.html"), await Template.Render404PageAsync());
    }

    protected override async Task TransformFilesAsync(string language)
    {
        var html = await Transformer.InitializeLayoutAsync(new DocumentPageModel
        {
            Language = language,
            AvailableLanguages = Transformer.AvailableLanguages,
            BookUrlName = BaseUrl,
        });
        var dir = Path.Combine(OutputFolder, language);

        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "layout.html"), html);
    }
}
