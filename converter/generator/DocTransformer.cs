using System.Buffers;
using System.Diagnostics;
using System.Net;
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

internal abstract partial class DocTransformer : IDocTransformer
{
    public const int MaxSiblingNodes = 10 * 2;

    protected string SourceFolder { get; }
    protected string SourceFolderEn { get; }
    protected string OutputFolder { get; }
    protected string BooksXmlFolder { get; }
    protected string BookUrlName { get; }

    protected string[] AvailableLanguages { get; }

    private readonly string AvailableLanguagesExpression;

    private readonly Dictionary<string, (string book, string url, string titleEn)> PageLinks;

    private Dictionary<string, string> MovedPages => field ??= GetMovedPages();

    private static Dictionary<string, string> SharedImages => field ??= GetSharedImages();

    private readonly Dictionary<string, (long size, ulong hash, string url)> EnglishImages = new(StringComparer.OrdinalIgnoreCase);

    private readonly bool UseWebp;

    private readonly ProblemRecorder Problems;

    #region Language specific members

    protected string Language { get; private set; } = null!;

    private INode[] LayoutNodes = null!;

    private readonly Dictionary<string, string> VisitedImages = new(StringComparer.OrdinalIgnoreCase);

    #endregion

    protected DocTransformer(DocTransformerArgs args, ProblemRecorder problems)
    {
        var (sourceFolder, outputFolder, booksXmlFolder, bookUrlName, useWebp) = args;

        SourceFolder = sourceFolder;
        SourceFolderEn = Path.Combine(sourceFolder, "en");
        OutputFolder = outputFolder;
        BooksXmlFolder = booksXmlFolder;
        BookUrlName = bookUrlName;
        UseWebp = useWebp;

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
        AvailableLanguagesExpression = String.Join(',', languages);

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

    public async Task TransformAsync()
    {
        foreach (var language in AvailableLanguages)
        {
            var html = await InitializeLanguageLayoutAsync(language);

            var langDir = Directory.CreateDirectory(Path.Combine(OutputFolder, language));
            await File.WriteAllTextAsync(Path.Combine(langDir.FullName, "layout.html"), html);

            await TransformFilesAsync(language);
        }

        File.WriteAllText(Path.Combine(OutputFolder, "404.html"), await Template.Render404PageAsync());
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

    public abstract Task TransformFilesAsync(string language);

    protected void Transform(string sourceFile, string destinationFile, in Nav nav = default, string? headerHtml = null, string? bannerHtml = null, string? footerHtml = null)
    {
        using var fs = File.OpenRead(sourceFile);
        var parser = new HtmlParser(new HtmlParserOptions { IsKeepingSourceReferences = true });
        var document = parser.ParseDocument(fs);
        var head = document.Head!;
        var body = document.Body!;

        var headerNodes = headerHtml.IsBlank ? null : parser.ParseFragment(headerHtml, head);
        var bannerNodes = bannerHtml.IsBlank ? null : parser.ParseFragment(bannerHtml, body);
        var footerNodes = footerHtml.IsBlank ? null : parser.ParseFragment(footerHtml, body);

        Transform(document, sourceFile, nav, headerNodes, bannerNodes, footerNodes);

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

    internal void Transform(IHtmlDocument document, string sourceFile, in Nav nav, INodeList? headerNodes, INodeList? bannerNodes, INodeList? footerNodes)
    {
        CleanUp(document);

        var head = document.Head!;
        var body = document.Body!;

        head.PrependNodes(LayoutNodes);

        if (headerNodes is not null)
        {
            head.PrependNodes(headerNodes.ToArray());
        }

        if (bannerNodes is not null)
        {
            body.PrependNodes(bannerNodes.ToArray());
        }

        var sourceDir = Path.GetDirectoryName(sourceFile)!;

        foreach (var a in document.Descendants<IHtmlAnchorElement>())
        {
            TransformAnchor(a, sourceFile, sourceDir);
        }

        foreach (var img in document.Descendants<IHtmlImageElement>())
        {
            TransformImage(img, sourceFile, sourceDir);
        }

        var navDataDiv = CreateNavDataDiv(document, nav, sourceDir);
        body.AppendChild(navDataDiv);

        if (footerNodes is not null)
        {
            body.AppendNodes(footerNodes.ToArray());
        }

        var loading = document.CreateElement<IHtmlDivElement>();
        loading.ClassName = "loading";

        var mainContent = document.CreateElement<IHtmlDivElement>();
        var placeholder = document.CreateElement<IHtmlDivElement>();

        mainContent.Id = "main-content";
        mainContent.AppendChild(placeholder);

        placeholder.Replace(body.ChildNodes.ToArray());
        body.AppendNodes(loading, mainContent);
    }

    private static void CleanUp(IHtmlDocument document)
    {
        document.Prepend(document.Implementation.CreateDocumentType("html", "", ""));

        document.Title = document.QuerySelector("h1")?.Text() ?? "";

        document.QuerySelectorAll("span.mw-editsection, p.urlname, p.hierarchy").Remove();
    }

    string? CachedSiblingsParent;
    int CachedSiblingsCurrentIndex;
    IHtmlElement? CachedSiblings;

    private IHtmlDivElement CreateNavDataDiv(IHtmlDocument document, in Nav nav, string sourceDir)
    {
        var navDataDiv = document.CreateElement<IHtmlDivElement>();

        navDataDiv.Id = "doc-nav-data";
        navDataDiv.IsHidden = true;

        navDataDiv.SetAttribute("data-lang", Language);
        navDataDiv.SetAttribute("data-lang-list", AvailableLanguagesExpression);

        var files = nav.Files;

        if (!files.Parent.IsEmpty)
        {
            if (TryResolveHref(sourceDir, "../" + files.Parent, out var url, out var _))
            {
                navDataDiv.SetAttribute("data-parent-link", url);
            }
        }
        else
        {
            navDataDiv.SetAttribute("data-parent-link", Language == "en" ? "/" : $"/{Language}");
        }

        if (nav.IsBookIndex)
        {
            navDataDiv.SetAttribute("data-book-index", BookUrlName);
        }

        if (files.Siblings is not null)
        {
            IHtmlElement ul;

            if (nav.Files.Parent is string parent && parent == CachedSiblingsParent)
            {
                ul = CreateSiblingsULFromCache(CachedSiblings!, files.Siblings, nav.Titles);
            }
            else
            {
                CachedSiblingsParent = nav.Files.Parent;
                CachedSiblings = ul = CreateDataUL("doc-siblings-data", files.Siblings, nav.Titles);
            }

            navDataDiv.AppendChild(ul);
        }

        if (files.Children is not null)
        {
            var ul = CreateDataUL("doc-children-data", files.Children, nav.Titles);
            navDataDiv.AppendChild(ul);
        }

        return navDataDiv;

        IHtmlElement CreateDataUL(string id, string[] files, Dictionary<string, string> titles)
        {
            var ul = document.CreateElement<IHtmlUnorderedListElement>();

            ul.Id = id;

            for (int i = 0; i < files.Length; i++)
            {
                string? path = files[i];
                var li = document.CreateElement<IHtmlListItemElement>();
                var a = document.CreateElement<IHtmlAnchorElement>();
                var pathSpan = path.AsSpan();
                var isCurrent = false;

                if (path.StartsWith('*'))
                {
                    pathSpan = pathSpan[1..];
                    isCurrent = true;
                    CachedSiblingsCurrentIndex = i;
                }

                if (TryResolveHref(sourceDir, $"../{pathSpan}", out var url, out var _))
                {
                    if (isCurrent)
                    {
                        li.ClassName = "disabled";
                    }
                    else
                    {
                        a.SetAttribute("href", url);
                    }

                    a.TextContent = titles.GetAlternateLookup<ReadOnlySpan<char>>()[pathSpan];
                }

                li.AppendChild(a);
                ul.AppendChild(li);
            }

            return ul;
        }

        IHtmlElement CreateSiblingsULFromCache(IHtmlElement ul, string[] files, Dictionary<string, string> titles)
        {
            document.AdoptNode(ul);

            var currentIdx = Array.FindIndex(files, CachedSiblingsCurrentIndex, files.Length - CachedSiblingsCurrentIndex, f => f.StartsWith('*'));
            var previousLi = ul.Children[CachedSiblingsCurrentIndex].SelfOrNextElementSibling(li => li.FirstElementChild!.GetAttribute("href").IsEmpty);

            Debug.Assert(currentIdx > 0);
            Debug.Assert(previousLi is not null);

            if (TryResolveHref(sourceDir, $"../{files[currentIdx - 1]}", out var url, out var _))
            {
                previousLi.FirstElementChild!.SetAttribute("href", url);
            }

            previousLi.ClassName = null;

            var currentLi = previousLi.NextElementSibling!;
            currentLi.FirstElementChild!.SetAttribute("href", null);
            currentLi.ClassName = "disabled";

            if (ul.ChildElementCount < files.Length)
            {
                var li = document.CreateElement<IHtmlListItemElement>();
                var a = document.CreateElement<IHtmlAnchorElement>();

                if (TryResolveHref(sourceDir, $"../{files[^1]}", out var endUrl, out var _))
                {
                    a.SetAttribute("href", endUrl);
                    a.TextContent = titles[files[^1]];
                }

                li.AppendChild(a);
                ul.AppendChild(li);
            }
            else if (currentIdx == MaxSiblingNodes / 2 && currentIdx != files.Length - 1 && currentIdx == CachedSiblingsCurrentIndex)
            {
                var li = (IHtmlElement)ul.RemoveChild(ul.FirstElementChild!);

                if (TryResolveHref(sourceDir, $"../{files[^1]}", out var endUrl, out var _)
                    && endUrl != ul.LastElementChild!.FirstElementChild!.GetAttribute("href"))
                {
                    var a = li.FirstElementChild!;

                    a.SetAttribute("href", endUrl);
                    a.TextContent = titles[files[^1]];

                    ul.AppendChild(li);
                }
            }

            CachedSiblingsCurrentIndex = currentIdx;
            return ul;
        }
    }

    protected virtual void TransformAnchor(IHtmlAnchorElement a, string sourceFile, string sourceDir)
    {
        if (a.GetAttribute("href") is string strHref && !strHref.IsBlank)
        {
            ReadOnlySpan<char> href = strHref, hash = "";
            var hashIndex = strHref.IndexOf('#');
            if (hashIndex == 0)
            {
                return;
            }
            else if (hashIndex > 0)
            {
                hash = strHref.AsSpan(hashIndex);
                href = strHref.AsSpan(..hashIndex);
            }

            if (TryResolveHref(sourceDir, hashIndex > 0 ? href.ToString() : strHref, out var result, out var title))
            {
                a.SetAttribute("href", $"{result}{hash}");

                if (a.Title.IsBlank && !title.IsEmpty)
                {
                    a.Title = title;
                }
            }
            else
            {
                ReportProblem(sourceFile, result, href.ToString(), a.SourceReference?.Position);
            }
        }
    }

    private bool TryResolveHref(string sourceDir, string href, out string result, out string? titleEn)
    {
        titleEn = null;

        if (!href.StartsWith('/') && Uri.IsWellFormedUriString(href, UriKind.Relative))
        {
            var fullPath = Path.GetFullPath(href, sourceDir);
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

                    titleEn = link.titleEn;
                    return true;
                }
            }

            result = "Unknown href mapping";
            return false;
        }
        else if (href.StartsWith("mailto:") || href.StartsWith("javascript:") || Uri.IsWellFormedUriString(href, UriKind.Absolute))
        {
            result = href;
            return true;
        }

        result = "Unrecognized href pattern";
        return false;
    }

    protected virtual void TransformImage(IHtmlImageElement img, string sourceFile, string sourceDir)
    {
        if (img.GetAttribute("src") is not string srcFull)
        {
            return;
        }

        var src = srcFull;
        var sep = srcFull.AsSpan().IndexOfAny("?#");
        if (sep > -1)
        {
            src = srcFull[..sep];
        }

        if (src.StartsWith("../images/"))
        {
            img.SetAttribute("loading", "lazy");

            var srcImg = new FileInfo(Path.GetFullPath(src, sourceDir));
            var copy = true;

            var fileName = Path.GetFileName(src.AsSpan());
            if (SharedImages.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(fileName, out var url))
            {
                copy = false;
            }
            else if (srcImg.Exists)
            {
                url = '/'.TryPrefixEach(BookUrlName, Language, srcFull["../".Length..]);

                if (Language == "en")
                {
                    if (!EnglishImages.TryGetValue(src, out var visited))
                    {
                        var size = srcImg.Length;
                        var hash = FileHash.UInt64FromFile(srcImg.FullName);

                        EnglishImages.Add(src, (size, hash, url));
                    }
                    else
                    {
                        url = visited.url;
                        copy = false;
                    }
                }
                else
                {
                    if (VisitedImages.TryGetValue(src, out var prevUrl))
                    {
                        url = prevUrl;
                        copy = false;
                    }
                    else
                    {
                        VisitedImages.Add(src, url);

                        if (EnglishImages.TryGetValue(src, out var visited) && srcImg.Length == visited.size && FileHash.UInt64FromFile(srcImg.FullName) == visited.hash)
                        {
                            url = VisitedImages[src] = visited.url;
                            copy = false;
                        }
                    }
                }
            }
            else
            {
                var srcImgEn = $"{SourceFolderEn}{srcImg.FullName.AsSpan(SourceFolderEn.Length)}";

                if (!File.Exists(srcImgEn))
                {
                    ReportProblem(sourceFile, "Image src not found", src, img.SourceReference?.Position);
                }

                url = '/'.TryPrefixEach(BookUrlName, "en", srcFull["../".Length..]);
                copy = false;
            }

            if (UseWebp)
            {
                sep = url.AsSpan().IndexOfAny("?#");

                if (sep > -1)
                {
                    var dot = url.AsSpan(..sep).LastIndexOf('.');
                    url = $"{url.AsSpan(..dot)}.webp{url.AsSpan(sep)}";
                }
                else
                {
                    var dot = url.AsSpan().LastIndexOf('.');
                    url = $"{url.AsSpan(..dot)}.webp";
                }
            }

            img.SetAttribute("src", url);

            if (copy)
            {
                var dstImg = Path.Combine(OutputFolder, Language, src["../".Length..]);

                Directory.CreateDirectory(Path.GetDirectoryName(dstImg)!);

                File.Copy(srcImg.FullName, dstImg, overwrite: true);
            }
        }
        else if (!Uri.IsWellFormedUriString(srcFull, UriKind.Absolute))
        {
            ReportProblem(sourceFile, "Unrecognized src", src, img.SourceReference?.Position);
        }
    }

    protected void ReportProblem(string sourcePath, string category, string? details = null, TextPosition? position = null)
        => Problems.Record(sourcePath, category, details, position);

    [GeneratedRegex(@"<h1[^>]*>.*?</h1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeaderRegex { get; }
}