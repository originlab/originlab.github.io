using System.Buffers;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using OriginLab.DocumentGeneration.Transformers;

namespace OriginLab.DocumentGeneration.Generator;

abstract class DocsGenerator : IDocsGenerator
{
    protected string SourceFolder => TransformationArgs.SourceFolder;

    protected string OutputFolder => TransformationArgs.OutputFolder;

    protected bool IsDevServer => TransformationArgs.IsDevServer;

    protected DocumentTransformer Transformer { get; }

    protected string[] AvailableLanguages { get; }

    private readonly DocsTransformationArgs TransformationArgs;
    private readonly ProblemRecorder Problems;

    protected DocsGenerator(DocsTransformationArgs args, DocumentTransformer transformer, ProblemRecorder problems)
    {
        var languages = (from subPath in Directory.EnumerateDirectories(args.SourceFolder)
                         let name = Path.GetFileName(subPath)
                         where name.Length == 2
                         select name).ToArray();

        var enIndex = languages.IndexOf("en");
        if (enIndex < 0)
        {
            throw new ArgumentException($"Expect en folder exists within {nameof(args.SourceFolder)}", nameof(args));
        }
        else if (enIndex > 0)
        {
            languages[enIndex] = languages[0];
            languages[0] = "en";
        }

        AvailableLanguages = languages;
        TransformationArgs = args;
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
        foreach (var language in AvailableLanguages)
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
