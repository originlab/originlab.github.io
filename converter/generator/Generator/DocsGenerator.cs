using AngleSharp.Html;
using AngleSharp.Html.Dom;
using OriginLab.DocumentGeneration.Transformers;

namespace OriginLab.DocumentGeneration.Generator;

abstract class DocsGenerator : IDocsGenerator
{
    protected string SourceFolder { get; }

    protected string OutputFolder { get; }

    protected DocumentTransformer Transformer { get; }

    private readonly ProblemRecorder Problems;

    protected DocsGenerator(string sourceFolder, string outputFolder, DocumentTransformer transformer, ProblemRecorder problems)
    {
        SourceFolder = sourceFolder;
        OutputFolder = outputFolder;
        Transformer = transformer;
        Problems = problems;
    }

    public async Task RunAsync()
    {
        await TransformFilesAsync();

        await Parallel.ForEachAsync(Transformer.FilesToCopy, async (pair, cancel) =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pair.dst)!);
            File.Copy(pair.src, pair.dst, overwrite: true);
        });
    }

    protected virtual async Task TransformFilesAsync()
    {
        foreach (var language in Transformer.AvailableLanguages)
        {
            await TransformFilesAsync(language);
        }
    }

    protected abstract Task TransformFilesAsync(string language);

    protected void Transform(string sourceFile, string destinationFile, Action<IHtmlDocument, string>? beforeTransform = null)
    {
        var dstDir = Path.GetDirectoryName(destinationFile);
        if (dstDir.IsBlank)
        {
            throw new ArgumentException("The directory name of destinationFile is not valid.", nameof(destinationFile));
        }

        Directory.CreateDirectory(dstDir);

        using var fs = File.OpenRead(sourceFile);
        var document = DocumentTransformer.CreateDocument(fs);

        beforeTransform?.Invoke(document, sourceFile);

        Transformer.Transform(document, sourceFile);

        using var sw = new StreamWriter(destinationFile);
        document.ToHtml(sw, HtmlMarkupFormatter.Instance);
    }

    protected void ReportProblem(string sourcePath, string category, string? details = null)
        => Problems.Record(sourcePath, category, details);

}
