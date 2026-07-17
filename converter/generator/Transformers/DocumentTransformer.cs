using System.Buffers;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Text;
using OriginLab.DocumentGeneration.Resolvers;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration.Transformers;

internal partial class DocumentTransformer
{
    public string[] AvailableLanguages => ResourceResolver.AvailableLanguages;

    public string Language
    {
        get => ResourceResolver.Language;
        private set => ResourceResolver.Language = value;
    }

    public IReadOnlyList<(string src, string dst)> FilesToCopy => ImagesToCopy;

    protected IDocResourceResolver ResourceResolver { get; }

    private readonly List<(string src, string dst)> ImagesToCopy = [];

    private readonly ProblemRecorder Problems;

    public DocumentTransformer(IDocResourceResolver resourceResolver, ProblemRecorder problems)
    {
        ResourceResolver = resourceResolver;
        Problems = problems;
    }

    public virtual async Task<string> InitializeLayoutAsync(DocumentPageModel model)
    {
        Language = model.Language;

        return "";
    }

    public static IHtmlDocument CreateDocument(Stream source)
    {
        var parser = new HtmlParser(new HtmlParserOptions { IsKeepingSourceReferences = true });
        var document = parser.ParseDocument(source);

        CleanUp(document);

        return document;
    }

    internal static void CleanUp(IHtmlDocument document)
    {
        document.Prepend(document.Implementation.CreateDocumentType("html", "", ""));
        document.QuerySelectorAll("span.mw-editsection, p.urlname, p.hierarchy").Remove();
        document.Title = document.QuerySelector("h1")?.Text() ?? "";
    }

    public virtual void Transform(IHtmlDocument document, string sourceFile)
    {
        foreach (var a in document.Descendants<IHtmlAnchorElement>().ToList())
        {
            if (TransformAnchor(document, a, sourceFile) is IHtmlElement transformed)
            {
                if (transformed != a)
                {
                    a.Replace(transformed);
                }
            }
            else
            {
                a.Remove();
            }
        }

        foreach (var img in document.Descendants<IHtmlImageElement>().ToList())
        {
            if (TransformImage(document, img, sourceFile) is IHtmlElement transformed)
            {
                if (transformed != img)
                {
                    img.Replace(transformed);
                }
            }
            else
            {
                img.Remove();
            }
        }
    }

    protected virtual IHtmlElement? TransformAnchor(IHtmlDocument document, IHtmlAnchorElement a, string sourceFile)
    {
        if (a.GetAttribute("href") is not string href || href.IsBlank)
        {
            return a;
        }

        if (ResourceResolver.TryResolveHref(href, sourceFile, out var result, out var title))
        {
            a.SetAttribute("href", result);

            if ((a.Title.IsBlank || a.Title.Contains(':')) && !title.IsEmpty)
            {
                a.Title = title;
            }
        }
        else
        {
            var parts = new UrlParts(href);
            ReportProblem(sourceFile, result, parts.Path.ToString(), a.SourceReference?.Position);
        }

        return a;
    }

    protected virtual IHtmlElement? TransformImage(IHtmlDocument document, IHtmlImageElement img, string sourceFile)
    {
        if (img.GetAttribute("src") is not string src || src.IsBlank)
        {
            return img;
        }

        if (ResourceResolver.TryResolveSrc(src, sourceFile, out var result, out var copy))
        {
            img.SetAttribute("src", result);

            if (copy is (string srcImg, string dstImg))
            {
                ImagesToCopy.Add((srcImg, dstImg));
            }
        }
        else
        {
            var parts = new UrlParts(src);
            ReportProblem(sourceFile, result, parts.Path.ToString(), img.SourceReference?.Position);
        }

        return img;
    }

    public static string? GetPageTitle(string sourceFile)
    {
        string? title = null;

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

        ArrayPool<char>.Shared.Return(buffer);

        return title;
    }

    [GeneratedRegex(@"<h1[^>]*>.*?</h1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeaderRegex { get; }

    protected void ReportProblem(string sourcePath, string category, string? details = null, TextPosition? position = null)
        => Problems.Record(sourcePath, category, details, position);
}
