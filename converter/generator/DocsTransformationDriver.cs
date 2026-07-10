using System.Buffers;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Text;

namespace OriginLab.DocumentGeneration;

abstract partial class DocsTransformationDriver<T> : IDocsTransformationDriver
    where T : DocumentTransformer
{
    protected string SourceFolder { get; }

    protected string OutputFolder { get; }

    protected T Transformer { get; }

    protected string[] AvailableLanguages { get; }

    private readonly ProblemRecorder Problems;

    protected DocsTransformationDriver(string sourceFolder, string outputFolder, T transformer, ProblemRecorder problems)
    {
        var languages = (from subPath in Directory.EnumerateDirectories(sourceFolder)
                         let name = Path.GetFileName(subPath)
                         where name.Length == 2
                         select name).ToArray();

        var enIndex = languages.IndexOf("en");
        if (enIndex < 0)
        {
            throw new ArgumentException("Expect en folder exists within SourceFolder", nameof(sourceFolder));
        }
        else if (enIndex > 0)
        {
            languages[enIndex] = languages[0];
            languages[0] = "en";
        }

        AvailableLanguages = languages;
        SourceFolder = sourceFolder;
        OutputFolder = outputFolder;
        Transformer = transformer;
        Problems = problems;
    }

    public virtual async Task RunAsync()
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
        var parser = new HtmlParser(new HtmlParserOptions { IsKeepingSourceReferences = true });
        var document = parser.ParseDocument(fs);

        DocumentTransformer.CleanUp(document);

        beforeTransform?.Invoke(document, sourceFile);

        Transformer.Transform(document, sourceFile);

        using var sw = new StreamWriter(destinationFile);
        document.ToHtml(sw, HtmlMarkupFormatter.Instance);
    }

    protected string GetPageTitle(string sourceFile)
    {
        string title = null!;

        using var reader = new StreamReader(sourceFile);
        var buffer = ArrayPool<char>.Shared.Rent(1024);
        var read = reader.ReadBlock(buffer);
        if (read > 0)
        {
            foreach (var match in HeaderRegex.EnumerateMatches(buffer.AsSpan(0, read)))
            {
                var parser = new HtmlParser();
                var doc = parser.ParseDocument(buffer.AsMemory(match.Index, match.Length));

                title = doc.QuerySelector("h1")!.Text();
                break;
            }
        }

        if (title is null)
        {
            title = "";
            ReportProblem(sourceFile, "Missing h1");
        }

        ArrayPool<char>.Shared.Return(buffer);

        return title;
    }

    [GeneratedRegex(@"<h1[^>]*>.*?</h1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeaderRegex { get; }

    protected void ReportProblem(string sourcePath, string category, string? details = null, TextPosition? position = null)
        => Problems.Record(sourcePath, category, details, position);

}
