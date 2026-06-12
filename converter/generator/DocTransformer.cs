using System.Buffers;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Text;

namespace OriginLab.DocumentGeneration;

internal abstract partial class DocTransformer : IDocTransformer
{
    protected string SourceFolder { get; }
    protected string OutputFolder { get; }
    protected string BooksXmlFolder { get; }

    protected string[] AvailableLanguages { get; }

    protected string Language
    {
        get => ResourceResolver.Language;
        set => ResourceResolver.Language = value;
    }

    protected IDocResourceResolver ResourceResolver { get; }

    protected IOutputOperations Output { get; }

    private readonly ProblemRecorder Problems;

    protected DocTransformer(DocTransformerArgs args, IDocResourceResolver resourceResolver, IOutputOperations output, ProblemRecorder problems)
    {
        var sourceFolder = args.SourceFolder;
        SourceFolder = sourceFolder;

        var booksXmlFolder = args.BooksXmlFolder;
        BooksXmlFolder = booksXmlFolder;

        OutputFolder = args.OutputFolder;
        Problems = problems;

        var languages = (from subPath in Directory.EnumerateDirectories(sourceFolder)
                         let name = Path.GetFileName(subPath)
                         where name.Length == 2
                         select name).ToArray();

        var enIndex = languages.IndexOf("en");
        if (enIndex < 0)
        {
            throw new ArgumentException("Expect en folder exists within SourceFolder", nameof(args));
        }
        else if (enIndex > 0)
        {
            languages[enIndex] = languages[0];
            languages[0] = "en";
        }

        AvailableLanguages = languages;
        ResourceResolver = resourceResolver;
        Output = output;
    }

    public virtual async Task TransformAsync()
    {
        foreach (var language in AvailableLanguages)
        {
            await TransformAsync(language);
        }
    }

    protected abstract Task TransformAsync(string language);

    protected void Transform(string sourceFile, string destinationFile, Action<IHtmlDocument, IHtmlHeadElement, IHtmlBodyElement, string>? beforeTransform = null)
    {
        using var fs = File.OpenRead(sourceFile);
        var parser = new HtmlParser(new HtmlParserOptions { IsKeepingSourceReferences = true });
        var document = parser.ParseDocument(fs);
        var head = document.Head!;
        var body = (IHtmlBodyElement)document.Body!;

        CleanUp(document);
        beforeTransform?.Invoke(document, head, body, sourceFile);
        Transform(document, head, body, sourceFile);

        using var sw = Output.CreateStreamWriter(destinationFile);
        document.ToHtml(sw, HtmlMarkupFormatter.Instance);
    }

    internal protected virtual void Transform(IHtmlDocument document, IHtmlHeadElement head, IHtmlBodyElement body, string sourceFile)
    {
        var sourceDir = Path.GetDirectoryName(sourceFile)!;

        foreach (var a in document.Descendants<IHtmlAnchorElement>())
        {
            if (TransformAnchor(document, a, sourceFile, sourceDir) is IHtmlElement transformed && transformed != a)
            {
                a.Replace(transformed);
            }
        }

        foreach (var img in document.Descendants<IHtmlImageElement>())
        {
            if (TransformImage(document, img, sourceFile, sourceDir) is IHtmlElement transformed && transformed != img)
            {
                img.Replace(transformed);
            }
        }
    }

    protected virtual IHtmlElement? TransformAnchor(IHtmlDocument document, IHtmlAnchorElement a, string sourceFile, string sourceDir)
    {
        if (a.GetAttribute("href") is not string href || href.IsBlank)
        {
            return null;
        }

        if (ResourceResolver.TryResolveHref(href, sourceDir, out var result, out var title))
        {
            a.SetAttribute("href", result);

            if ((a.Title.IsBlank || a.Title.Contains(':')) && !title.IsEmpty)
            {
                a.Title = title;
            }

            return a;
        }
        else
        {
            var parts = new UrlParts(href);
            ReportProblem(sourceFile, result, parts.Path.ToString(), a.SourceReference?.Position);
        }

        return null;
    }

    protected virtual IHtmlElement? TransformImage(IHtmlDocument document, IHtmlImageElement img, string sourceFile, string sourceDir)
    {
        if (img.GetAttribute("src") is not string src || src.IsBlank)
        {
            return null;
        }

        if (ResourceResolver.TryResolveSrc(src, sourceDir, out var result, out var copy))
        {
            img.SetAttribute("src", result);

            if (copy is (string srcImg, string dstImg))
            {
                Output.CreateDirectory(Path.GetDirectoryName(dstImg)!);
                Output.CopyFile(srcImg, dstImg, overwrite: true);
            }

            return img;
        }
        else
        {
            var parts = new UrlParts(src);
            ReportProblem(sourceFile, result, parts.Path.ToString(), img.SourceReference?.Position);
        }

        return null;
    }

    private static void CleanUp(IHtmlDocument document)
    {
        document.Prepend(document.Implementation.CreateDocumentType("html", "", ""));

        document.Title = document.QuerySelector("h1")?.Text() ?? "";

        document.QuerySelectorAll("span.mw-editsection, p.urlname, p.hierarchy").Remove();
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

    protected void ReportProblem(string sourcePath, string category, string? details = null, TextPosition? position = null)
        => Problems.Record(sourcePath, category, details, position);

    [GeneratedRegex(@"<h1[^>]*>.*?</h1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeaderRegex { get; }

}
