using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

class DocsToGithubPagesTransformationDriver<T> : DocsTransformationDriver<T> where T : DocumentTransformer
{
    protected string BaseUrl { get; }

    public DocsToGithubPagesTransformationDriver(string baseUrl, string sourceFolder, string outputFolder, T transformer, ProblemRecorder problems)
        : base(sourceFolder, outputFolder, transformer, problems)
    {
        BaseUrl = baseUrl;
    }

    public override async Task RunAsync()
    {
        await base.RunAsync();

        File.WriteAllText(Path.Combine(OutputFolder, "404.html"), await Template.Render404PageAsync());
    }

    protected override async Task TransformFilesAsync(string language)
    {
        var html = await Transformer.InitializeLayoutAsync(new DocumentPageModel
        {
            Language = language,
            AvailableLanguages = AvailableLanguages,
            BookUrlName = BaseUrl,
        });
        var dir = Path.Combine(OutputFolder, language);

        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "layout.html"), html);
    }
}
