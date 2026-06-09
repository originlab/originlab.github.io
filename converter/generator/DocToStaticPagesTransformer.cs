using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal abstract partial class DocToStaticPagesTransformer : DocTransformer
{
    public const int MaxSiblingNodes = 10 * 2;

    protected string BookUrlName { get; }

    private readonly Dictionary<string, (string book, string url, string titleEn)> PageLinks;

    private readonly bool UseWebp;

    #region Language specific members

    protected string Language { get; private set; } = null!;

    private INode[] LayoutNodes = null!;

    private readonly Dictionary<string, (long size, ulong hash, string url)> EnglishImages = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> VisitedImages = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    protected DocToStaticPagesTransformer(DocToStaticPagesTransformerArgs args, ProblemRecorder problems) : base(args, problems)
    {
        BookUrlName = args.BookUrlName;

        var pages = new List<(string file, string book, string url, string title)>();

        foreach (var xmlFile in Directory.EnumerateFiles(BooksXmlFolder, "*.xml"))
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

        UseWebp = args.UseWebp;
    }

    public override async Task TransformAsync()
    {
        await base.TransformAsync();

        File.WriteAllText(Path.Combine(OutputFolder, "404.html"), await Template.Render404PageAsync());
    }

    protected override async Task TransformAsync(string language)
    {
        var html = await InitializeLanguageLayoutAsync(language);
        var dir = Path.Combine(OutputFolder, language);

        Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(Path.Combine(dir, "layout.html"), html);
    }

    internal async Task<string> InitializeLanguageLayoutAsync(string language)
    {
        var parser = new HtmlParser();
        var layout = parser.ParseDocument("<html></html>");

        var html = await Template.RenderDocumentPageAsync(new DocumentPageModel
        {
            Language = language,
            AvailableLanguages = AvailableLanguages,
            BookUrlName = BookUrlName,
        });

        var scripts = await Template.RenderApplyLayoutScriptsAsync(new ApplyLayoutModel
        {
            LayoutPageUrl = '/'.TryPrefixEach(BookUrlName, language, $"layout.html?v={FileHash.FromString(html)}"),
        });

        Language = language;
        LayoutNodes = parser.ParseFragment(scripts, layout.Head!).ToArray();
        VisitedImages.Clear();

        return html;
    }

    protected internal override void Transform(IHtmlDocument document, IHtmlHeadElement head, IHtmlBodyElement body, string sourceFile)
    {
        base.Transform(document, head, body, sourceFile);

        head.PrependNodes(LayoutNodes);

        var loading = document.CreateElement<IHtmlDivElement>();
        loading.ClassName = "loading";

        var mainContent = document.CreateElement<IHtmlDivElement>();
        var placeholder = document.CreateElement<IHtmlDivElement>();

        mainContent.Id = "main-content";
        mainContent.AppendChild(placeholder);

        placeholder.Replace(body.ChildNodes.ToArray());
        body.AppendNodes(loading, mainContent);
    }

    protected internal override bool TryResolveHref(string href, string sourceDir, out string result, out string? titleEn)
    {
        titleEn = null;

        var parts = new UrlParts(href);

        if (parts.IsAbosolute || href.StartsWith('/') || href.StartsWith('#'))
        {
            result = href;
            return true;
        }

        var path = parts is { Query.Length: 0, Hash.Length: 0 } ? href : parts.Path.ToString();
        Debug.Assert(!path.IsEmpty);

        var fullPath = Path.GetFullPath(path, sourceDir);
        if (fullPath.StartsWith(SourceFolder)
            && Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(fullPath.AsSpan()))) is { IsEmpty: false } targetBookDirContainer)
        {
            var targetFile = WebUtility.UrlDecode(fullPath[(targetBookDirContainer.Length + 1)..].Replace('\\', '/'));

            if (PageLinks.TryGetValue(targetFile, out var link)
                || (MovedPages.TryGetValue(targetFile, out var movedToFile) && PageLinks.TryGetValue(movedToFile, out link)))
            {
                if (Language == "en")
                {
                    result = '/'.TrySurroundEach(link.book, link.url);
                }
                else
                {
                    result = '/'.TrySurroundEach(link.book, link.url, Language);
                }

                if (!parts.Query.IsEmpty || !parts.Hash.IsEmpty)
                {
                    result = $"{result}{parts.Query}{parts.Hash}";
                }

                titleEn = link.titleEn;
                return true;
            }
        }

        result = "Unknown href mapping";
        return false;
    }

    protected override string GetSharedImageSrc(string path, string fileName)
        => $"/books/images/{fileName}?v={FileHash.StringFromFile(path)}";

    protected internal override bool TryResolveSrc(string src, string sourceDir, out string result, out (string src, string dst)? copy)
    {
        var parts = new UrlParts(src);

        if (parts.IsAbosolute || src.StartsWith('/'))
        {
            result = src;
            copy = null;
            return true;
        }

        var path = parts is { Query.Length: 0, Hash.Length: 0 } ? src : parts.Path.ToString();
        Debug.Assert(!path.IsEmpty);

        var indexOfImages = path.IndexOf("images/");
        Debug.Assert(indexOfImages > -1);

        var srcImg = new FileInfo(Path.GetFullPath(path, sourceDir));
        var needsCopy = true;

        var fileName = Path.GetFileName(path);
        if (SharedImages.TryGetValue(fileName, out result!))
        {
            needsCopy = false;
        }
        else if (srcImg.Exists)
        {
            result = '/'.TryPrefixEach(BookUrlName, Language, path[indexOfImages..]);

            if (Language == "en")
            {
                if (!EnglishImages.TryGetValue(path, out var visited))
                {
                    var size = srcImg.Length;
                    var hash = FileHash.UInt64FromFile(srcImg.FullName);

                    EnglishImages.Add(path, (size, hash, result));
                }
                else
                {
                    result = visited.url;
                    needsCopy = false;
                }
            }
            else
            {
                if (VisitedImages.TryGetValue(path, out var prevUrl))
                {
                    result = prevUrl;
                    needsCopy = false;
                }
                else
                {
                    VisitedImages.Add(path, result);

                    if (EnglishImages.TryGetValue(path, out var visited) && srcImg.Length == visited.size && FileHash.UInt64FromFile(srcImg.FullName) == visited.hash)
                    {
                        result = VisitedImages[path] = visited.url;
                        needsCopy = false;
                    }
                }
            }
        }
        else
        {
            var srcImgEn = $"{SourceFolderEn}{srcImg.FullName.AsSpan(SourceFolderEn.Length)}";

            if (!File.Exists(srcImgEn))
            {
                result = "Image src not found";
                copy = null;
                return false;
            }

            result = '/'.TryPrefixEach(BookUrlName, "en", path[indexOfImages..]);
            needsCopy = false;
        }

        if (UseWebp)
        {
            var resultDir = Path.GetDirectoryName(result.AsSpan());
            var resultFileName = Path.GetFileNameWithoutExtension(result.AsSpan());

            result = $"{resultDir}/{resultFileName}.webp";
        }

        if (!needsCopy)
        {
            copy = null;
        }
        else
        {
            var dstImg = Path.Combine(OutputFolder, Language, path[indexOfImages..]);
            copy = (srcImg.FullName, dstImg);
        }

        if (!parts.Query.IsEmpty)
        {
            result = $"{result}{parts.Query}";
        }

        return true;
    }

    protected override IHtmlElement? TransformImage(IHtmlDocument document, IHtmlImageElement img, string sourceFile, string sourceDir)
    {
        if (base.TransformImage(document, img, sourceFile, sourceDir) is IHtmlElement transformed)
        {
            transformed.SetAttribute("loading", "lazy");

            return transformed;
        }

        return null;
    }
}