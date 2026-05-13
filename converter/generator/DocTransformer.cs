using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using AngleSharp.Common;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Text;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal abstract class DocTransformer
{
    protected string SourceFolder { get; }
    protected string SourceFolderEn { get; }
    protected string OutputFolder { get; }
    protected string BooksXmlFolder { get; }
    protected string BookUrlName { get; }

    protected string[] AvailableLanguages { get; }

    private readonly Dictionary<string, (string book, string url, string titleEn)> PageLinks;

    private Dictionary<string, string> MovedPages => field ??= GetMovedPages();

    private static Dictionary<string, string> SharedImages => field ??= GetSharedImages();

    private readonly Dictionary<string, (long size, ulong hash, string url)> ImagesEn = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, INode[]> LayoutNodes = [];
    private readonly Dictionary<string, List<(string file, TextPosition? position)>> Problems = [];

    protected DocTransformer(string sourceFolder, string outputFolder, string booksXmlFolder, string bookUrlName)
    {
        var languages = (from subPath in Directory.EnumerateDirectories(sourceFolder)
                         let name = Path.GetFileName(subPath)
                         where name.Length == 2
                         select name).ToArray();

        var enIndex = languages.IndexOf("en");
        if (enIndex < 0)
        {
            throw new ArgumentException("Expect en folder exists within sourceFolder", nameof(sourceFolder));
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

        SourceFolder = Path.GetFullPath(sourceFolder);
        SourceFolderEn = Path.Combine(SourceFolder, "en");
        OutputFolder = Path.GetFullPath(outputFolder);
        BooksXmlFolder = booksXmlFolder;
        BookUrlName = bookUrlName;
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

        foreach (var imgFile in Directory.EnumerateFiles(Path.Combine(Template.WebRootPath, "images/books")))
        {
            var fileName = Path.GetFileName(imgFile);
            images.Add(fileName, $"/images/books/{fileName}?v={FileHash.StringFromFile(imgFile)}");
        }

        return images;
    }

    public async Task TransformAsync()
    {
        var parser = new HtmlParser(new HtmlParserOptions { IsKeepingSourceReferences = true });
        var layout = parser.ParseDocument("<html></html>");

        foreach (var language in AvailableLanguages)
        {
            var scripts = await GenerateLayoutAsync(language);

            LayoutNodes.Add(language, parser.ParseFragment(scripts, layout.Head!).ToArray());
        }

        await TransformFilesAsync();

        File.WriteAllText(Path.Combine(OutputFolder, "404.html"), await Template.Render404PageAsync());
    }

    public abstract Task TransformFilesAsync();

    protected void Transform(string sourceFile, string destinationFile, string language, in Nav nav = default, string? headerHtml = null, string? bannerHtml = null, string? footerHtml = null)
    {
        using var fs = File.OpenRead(sourceFile);
        var parser = new HtmlParser(new HtmlParserOptions { IsKeepingSourceReferences = true });
        var document = parser.ParseDocument(fs);
        var head = document.Head!;
        var body = document.Body!;

        var headerNodes = headerHtml.IsBlank ? null : parser.ParseFragment(headerHtml, head);
        var bannerNodes = bannerHtml.IsBlank ? null : parser.ParseFragment(bannerHtml, body);
        var footerNodes = footerHtml.IsBlank ? null : parser.ParseFragment(footerHtml, body);

        Transform(document, sourceFile, language, nav, headerNodes, bannerNodes, footerNodes);

        using var sw = new StreamWriter(destinationFile);
        document.ToHtml(sw, HtmlMarkupFormatter.Instance);
    }

    protected static string GetPageTitle(string sourceFile)
    {
        using var fs = new FileStream(sourceFile, FileMode.Open, FileAccess.Read);
        var parser = new HtmlParser();
        var document = parser.ParseDocument(fs);

        return GetPageTitle(document);
    }

    private static string GetPageTitle(IHtmlDocument document)
    {
        if (document.QuerySelector("h1") is IElement firstHeading)
        {
            return firstHeading.Text();
        }

        return "";
    }

    void Transform(IHtmlDocument document, string sourceFile, string language, in Nav nav, INodeList? headerNodes, INodeList? bannerNodes, INodeList? footerNodes)
    {
        document.Title = GetPageTitle(document);

        var head = document.Head!;
        var body = document.Body!;

        head.PrependNodes(LayoutNodes[language]);

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
            TransformAnchor(a, sourceFile, language, sourceDir);
        }

        foreach (var img in document.Descendants<IHtmlImageElement>())
        {
            TransformImage(img, sourceFile, language, sourceDir);
        }

        var navDataDiv = CreateNavDataDiv(document, nav, sourceDir, language);
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

    private IHtmlDivElement CreateNavDataDiv(IHtmlDocument document, in Nav nav, string sourceDir, string language)
    {
        var navDataDiv = document.CreateElement<IHtmlDivElement>();

        navDataDiv.Id = "doc-nav-data";
        navDataDiv.IsHidden = true;

        var files = nav.Files;

        if (!files.Parent.IsEmpty)
        {
            if (TryResolveHref(sourceDir, language, "../" + files.Parent, out var url, out var _))
            {
                navDataDiv.SetAttribute("data-parent-link", url);
            }
        }
        else
        {
            navDataDiv.SetAttribute("data-parent-link", language == "en" ? "/" : $"/{language}");
        }

        if (nav.IsBookIndex)
        {
            navDataDiv.SetAttribute("data-book-index", BookUrlName);
        }

        if (files.Siblings is not null)
        {
            var ul = CreateDataUL("doc-siblings-data", files.Siblings, nav.Titles);
            navDataDiv.AppendChild(ul);
        }

        if (files.Children is not null)
        {
            var ul = CreateDataUL("doc-children-data", files.Children, nav.Titles);
            navDataDiv.AppendChild(ul);
        }

        return navDataDiv;

        IHtmlUnorderedListElement CreateDataUL(string id, string[] files, Dictionary<string, string> titles)
        {
            var ul = document.CreateElement<IHtmlUnorderedListElement>();

            ul.Id = id;

            foreach (var path in files)
            {
                var li = document.CreateElement<IHtmlListItemElement>();

                if (TryResolveHref(sourceDir, language, "../" + path, out var url, out var titleEn))
                {
                    var a = document.CreateElement<IHtmlAnchorElement>();

                    a.SetAttribute("href", url);

                    if (titles.TryGetValue(path, out var title))
                    {
                        a.TextContent = title;
                    }
                    else
                    {
                        a.TextContent = titleEn ?? "";
                    }

                    li.AppendChild(a);
                }
                else
                {
                    li.SetAttribute("role", "separator");
                    li.ClassName = "divider";
                }

                ul.AppendChild(li);
            }

            return ul;
        }
    }

    protected virtual void TransformAnchor(IHtmlAnchorElement a, string sourceFile, string language, string sourceDir)
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

            if (TryResolveHref(sourceDir, language, hashIndex > 0 ? href.ToString() : strHref, out var result, out var title))
            {
                a.SetAttribute("href", $"{result}{hash}");

                if (!title.IsEmpty)
                {
                    a.SetAttribute("title", title);
                }
            }
            else
            {
                ReportProblem(sourceFile, $"{result} for href: {href}", a.SourceReference?.Position);
            }
        }
    }

    private bool TryResolveHref(string sourceDir, string language, string href, out string result, out string? titleEn)
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
                    if (language == "en")
                    {
                        result = '/'.TryPrefixEach(link.book, link.url);
                    }
                    else
                    {
                        result = '/'.TryPrefixEach(link.book, link.url, language);
                    }

                    titleEn = link.titleEn;
                    return true;
                }
            }

            result = "Unknown mapping";
            return false;
        }
        else if (href.StartsWith("mailto:") || href.StartsWith("javascript:") || Uri.IsWellFormedUriString(href, UriKind.Absolute))
        {
            result = href;
            return true;
        }

        result = "Unrecognized pattern";
        return false;
    }

    protected virtual void TransformImage(IHtmlImageElement img, string sourceFile, string language, string sourceDir)
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

            var srcImg = Path.GetFullPath(src, sourceDir);
            var copy = true;

            var fileName = Path.GetFileName(src.AsSpan());
            if (SharedImages.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(fileName, out var url))
            {
                copy = false;
            }
            else if (File.Exists(srcImg))
            {
                url = $"/{BookUrlName}/{language}/{srcFull.AsSpan("../".Length)}";

                if (AvailableLanguages.Length > 1)
                {
                    if (language == "en")
                    {
                        if (!ImagesEn.ContainsKey(src))
                        {
                            var size = new FileInfo(srcImg).Length;
                            var hash = FileHash.UInt64FromFile(srcImg);

                            ImagesEn.Add(src, (size, hash, url));
                        }
                        else
                        {
                            copy = false;
                        }
                    }
                    else if (ImagesEn.TryGetValue(src, out var enImg) && new FileInfo(srcImg).Length == enImg.size && FileHash.UInt64FromFile(srcImg) == enImg.hash)
                    {
                        url = enImg.url;
                        copy = false;
                    }
                }
            }
            else
            {
                var srcImgEn = $"{SourceFolderEn}{srcImg.AsSpan(SourceFolderEn.Length)}";

                if (!File.Exists(srcImgEn))
                {
                    ReportProblem(sourceFile, $"Image src not found: {src}", img.SourceReference?.Position);
                }

                url = $"/{BookUrlName}/en/{srcFull.AsSpan("../".Length)}";
                copy = false;
            }

            img.SetAttribute("src", url);

            if (copy)
            {
                var dstImg = Path.Combine(OutputFolder, language, src["../".Length..]);

                Directory.CreateDirectory(Path.GetDirectoryName(dstImg)!);

                File.Copy(srcImg, dstImg, overwrite: true);
            }
        }
        else if (!Uri.IsWellFormedUriString(srcFull, UriKind.Absolute))
        {
            ReportProblem(sourceFile, $"Unrecognized src: {src}", img.SourceReference?.Position);
        }
    }

    private async Task<string> GenerateLayoutAsync(string language)
    {
        var langDir = Directory.CreateDirectory(Path.Combine(OutputFolder, language));
        var layoutHtml = await Template.RenderDocumentPageAsync(new DocumentPageModel
        {
            RootUrlPrefix = null,
            Language = language,
            AvailableLanguages = AvailableLanguages,
            BookUrlName = BookUrlName,
        });

        File.WriteAllText(Path.Combine(langDir.FullName, "layout.html"), layoutHtml);

        var layoutScripts = await Template.RenderApplyLayoutScriptsAsync(new ApplyLayoutModel
        {
            LayoutPageUrl = '/'.TryPrefixEach(BookUrlName, language, $"layout.html?v={FileHash.FromString(layoutHtml)}"),
            PlaceHolderId = "doc-content-placeholder",
            MainContentId = "main-content",
        });

        return layoutScripts;
    }

    protected void ReportProblem(string sourcePath, string message, TextPosition? position = null)
    {
        var file = Path.GetRelativePath(SourceFolder, sourcePath);

        if (!Problems.TryGetValue(message, out var list))
        {
            Problems[message] = list = [];
        }

        list.Add((file, position));
    }

    public void PrintProblems()
    {
        if (Problems.Count > 0)
        {
            var error = Console.Error;

            error.WriteLine();
            error.WriteLine("Problems:");

            foreach (var (message, list) in Problems.OrderByDescending(kvp => kvp.Value.Count))
            {
                error.WriteLine();
                error.WriteLine($"::warning::{list.Count}x {message}");

                foreach (var details in list.ToLookup(i => i.file, i => i.position))
                {
                    if (details.Count() > 1)
                    {
                        error.WriteLine($"::group::{details.Key}");

                        foreach (var p in details)
                        {
                            if (p is TextPosition position)
                            {
                                error.WriteLine($"\tLine: {position.Line}, Column: {position.Column}");
                            }
                        }

                        error.WriteLine("::endgroup::");
                    }
                    else if (details.First() is TextPosition position)
                    {
                        error.WriteLine($"File: {details.Key}, Line: {position.Line}, Column {position.Column}");
                    }
                    else
                    {
                        error.WriteLine($"File: {details.Key}");
                    }
                }
            }
        }
    }
}