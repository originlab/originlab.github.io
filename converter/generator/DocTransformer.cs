using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngleSharp.Common;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Text;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal abstract partial class DocTransformer
{
    protected string SourceFolder { get; }
    protected string SourceFolderEn { get; }
    protected string OutputFolder { get; }
    protected string BooksXmlFolder { get; }
    protected string BookUrlName { get; }

    protected string[] AvailableLanguages { get; }

    protected readonly Dictionary<string, (string book, string url, string titleEn)> PageLinks;

    protected Dictionary<string, string> MovedPages => field ??= GetMovedPages();

    protected static Dictionary<string, string> SharedImages => field ??= GetSharedImages();

    protected readonly Dictionary<string, (long size, ulong hash, string url)> EnglishImages = new(StringComparer.OrdinalIgnoreCase);

    private readonly ProblemRecorder Problems;

    protected DocTransformer(DocTransformerArgs args, ProblemRecorder problems)
    {
        var sourceFolder = args.SourceFolder;
        SourceFolder = sourceFolder;
        SourceFolderEn = Path.Combine(sourceFolder, "en");

        var booksXmlFolder = args.BooksXmlFolder;
        BooksXmlFolder = booksXmlFolder;

        OutputFolder = args.OutputFolder;
        BookUrlName = args.BookUrlName;

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

        var pages = new List<(string file, string book, string url, string title)>();

        foreach (var xmlFile in Directory.EnumerateFiles(booksXmlFolder, "*.xml"))
        {
            var dirName = Path.GetFileNameWithoutExtension(xmlFile);

            foreach (var p in XElement.Load(xmlFile).Descendants("page"))
            {
                var file = $"{dirName}/{p.Attribute("file")!.Value}";
                var url = p.Attribute("url")!.Value;
                var sep = url.IndexOf('/');
                var title = p.Attribute("title")!.Value;

                pages.Add((file, book: sep < 0 ? url : url[..sep], url: sep < 0 ? "" : url[(sep + 1)..], title));
            }
        }

        PageLinks = pages.ToDictionary(p => p.file, p => (p.book.ToLowerInvariant(), p.url.ToLowerInvariant(), p.title), StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string> GetMovedPages()
    {
        using var movedJson = File.OpenRead(Path.Combine(BooksXmlFolder, "Moved.json"));
#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        return JsonSerializer.Deserialize<Dictionary<string, string>>(movedJson, new JsonSerializerOptions
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            AllowDuplicateProperties = true,
        })
        ?.ToDictionary(StringComparer.OrdinalIgnoreCase) ?? [];
    }

    private static Dictionary<string, string> GetSharedImages()
    {
        var images = new Dictionary<string, string>();

        foreach (var imgFile in Directory.EnumerateFiles(Path.Combine(Template.WebRootPath, "books/images")))
        {
            var fileName = Path.GetFileName(imgFile);
            images.Add(fileName, $"/books/images/{fileName}?v={FileHash.StringFromFile(imgFile)}");
        }

        return images;
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

        using var sw = new StreamWriter(destinationFile);
        document.ToHtml(sw, HtmlMarkupFormatter.Instance);
    }

    internal protected virtual void Transform(IHtmlDocument document, IHtmlHeadElement head, IHtmlBodyElement body, string sourceFile)
    {
        var sourceDir = Path.GetDirectoryName(sourceFile)!;

        foreach (var a in document.Descendants<IHtmlAnchorElement>())
        {
            TransformAnchor(document, a, sourceFile, sourceDir);
        }

        foreach (var img in document.Descendants<IHtmlImageElement>())
        {
            TransformImage(document, img, sourceFile, sourceDir);
        }
    }

    protected abstract bool TryResolveHref(string href, string sourceDir, out string result, out string? titleEn);

    protected virtual IHtmlElement? TransformAnchor(IHtmlDocument document, IHtmlAnchorElement a, string sourceFile, string sourceDir)
    {
        if (a.GetAttribute("href") is string href && !href.IsBlank)
        {
            var parts = new UrlParts(href);

            if (TryResolveHref(parts.File.Length == href.Length ? href : parts.File.ToString(), sourceDir, out var result, out var title))
            {
                a.SetAttribute("href", $"{result}{parts.Query}{parts.Hash}");

                if (a.Title.IsBlank && !title.IsEmpty)
                {
                    a.Title = title;
                }

                return a;
            }
            else
            {
                ReportProblem(sourceFile, result, href.ToString(), a.SourceReference?.Position);
            }
        }

        return null;
    }

    protected abstract bool TryResolveSrc(string src, string sourceDir, out string result, out (string src, string dst)? copy);

    protected virtual IHtmlElement? TransformImage(IHtmlDocument document, IHtmlImageElement img, string sourceFile, string sourceDir)
    {
        if (img.GetAttribute("src") is not string src)
        {
            return null;
        }

        var parts = new UrlParts(src);

        if (TryResolveSrc(parts.File.ToString(), sourceDir, out var result, out var copy))
        {
            img.SetAttribute("src", $"{result}{parts.Query}{parts.Hash}");

            if (copy is (string srcImg, string dstImg))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dstImg)!);
                File.Copy(srcImg, dstImg, overwrite: true);
            }

            return img;
        }
        else
        {
            ReportProblem(sourceFile, result, parts.File.ToString(), img.SourceReference?.Position);
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
