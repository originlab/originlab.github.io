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

internal abstract class Transformer
{
    protected readonly string SourceFolder;
    protected readonly string SourceFolderEn;
    protected readonly string OutputFolder;

    protected readonly string[] AvailableLanguages;

    protected string BookUrlName => field ??= GetBookUrlName();
    protected readonly Dictionary<string, (string book, string url, string titleEn)> PageLinks;
    protected readonly Dictionary<string, string> MovedPages;

    protected readonly Dictionary<string, Dictionary<string, string>> Titles = [];
    protected readonly Dictionary<string, (long size, ulong hash, string url)> ImagesEn = new(StringComparer.OrdinalIgnoreCase);

    readonly Dictionary<string, List<(string file, TextPosition? position)>> Problems = [];

    protected Transformer(string booksXmlFolder, string sourceFolder, string outputFolder)
    {
        var languages = (from subPath in Directory.EnumerateDirectories(sourceFolder)
                         let name = Path.GetFileName(subPath)
                         where name.Length == 2
                         select name).ToArray();

        var enIndex = languages.IndexOf("en");
        if (enIndex < 0)
        {
            throw new FileNotFoundException("Expect en folder exists within source book", Path.Combine(sourceFolder, "en"));
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

        using (var movedJson = File.OpenRead(Path.Combine(booksXmlFolder, "Moved.json")))
        {
#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
            MovedPages = JsonSerializer.Deserialize<Dictionary<string, string>>(movedJson, new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true,
                AllowDuplicateProperties = true,
            })
            ?.ToDictionary(StringComparer.OrdinalIgnoreCase) ?? [];
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        }

        SourceFolder = Path.GetFullPath(sourceFolder);
        SourceFolderEn = Path.Combine(SourceFolder, "en");
        OutputFolder = Path.GetFullPath(outputFolder);
    }

    protected abstract string GetBookUrlName();

    public async Task TransformAsync()
    {
        await TransformFilesAsync();

        File.WriteAllText(Path.Combine(OutputFolder, "404.html"), await Template.Render404PageAsync());
    }

    public abstract Task TransformFilesAsync();

    protected void Transform(string sourceFile, string destinationFile, Nav nav, string language, string headerHtml, string? bannerHtml = null)
    {
        using var fs = new FileStream(sourceFile, FileMode.Open, FileAccess.Read);
        var parser = new HtmlParser(new HtmlParserOptions
        {
            IsKeepingSourceReferences = true
        });
        var document = parser.ParseDocument(fs);

        var headerNodes = parser.ParseFragment(headerHtml, document.Head!);
        var bannerNodes = bannerHtml.IsBlank ? null : parser.ParseFragment(bannerHtml, document.Body!);

        Transform(document, sourceFile, nav, language, headerNodes, bannerNodes);

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

    void Transform(IHtmlDocument document, string sourceFile, Nav nav, string language, INodeList headerNodes, INodeList? bannerNodes)
    {
        document.Title = GetPageTitle(document);

        var head = document.Head!;
        var body = document.Body!;

        head.PrependNodes(headerNodes.ToArray());

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

        var loading = document.CreateElement<IHtmlDivElement>();
        loading.ClassName = "loading";

        var mainContent = document.CreateElement<IHtmlDivElement>();
        var placeholder = document.CreateElement<IHtmlDivElement>();

        mainContent.Id = "main-content";
        mainContent.AppendChild(placeholder);

        placeholder.Replace(body.ChildNodes.ToArray());
        body.AppendNodes(loading, mainContent);
    }

    private IHtmlDivElement CreateNavDataDiv(IHtmlDocument document, Nav nav, string sourceDir, string language)
    {
        var navDataDiv = document.CreateElement<IHtmlDivElement>();

        navDataDiv.Id = "doc-nav-data";
        navDataDiv.IsHidden = true;

        if (!nav.Parent.IsEmpty)
        {
            if (TryResolveHref(sourceDir, language, "../" + nav.Parent, out var url, out var _))
            {
                navDataDiv.SetAttribute("data-parent-link", url);
            }
        }
        else
        {
            navDataDiv.SetAttribute("data-parent-link", language == "en" ? "/" : $"/{language}");
        }

        if (nav.Siblings is not null)
        {
            var ul = CreateDataUL("doc-siblings-data", nav.Siblings);
            navDataDiv.AppendChild(ul);
        }

        if (nav.Children is not null)
        {
            var ul = CreateDataUL("doc-children-data", nav.Children);
            navDataDiv.AppendChild(ul);
        }

        return navDataDiv;

        IHtmlUnorderedListElement CreateDataUL(string id, string[] files)
        {
            var ul = document.CreateElement<IHtmlUnorderedListElement>();
            var titles = Titles[language];

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
        if (img.GetAttribute("src") is not string src)
        {
            return;
        }

        if (src.StartsWith("../images/"))
        {
            img.SetAttribute("loading", "lazy");

            var srcImg = Path.GetFullPath(src, sourceDir);
            var copy = true;

            var sep = srcImg.AsSpan().IndexOfAny("?#");
            if (sep > -1)
            {
                srcImg = srcImg[..sep];
            }

            if (File.Exists(srcImg))
            {
                var url = $"/{BookUrlName}/{language}/{src.AsSpan("../".Length)}";

                if (AvailableLanguages.Length > 1)
                {
                    if (language == "en")
                    {
                        if (!ImagesEn.ContainsKey(src))
                        {
                            var size = new FileInfo(srcImg).Length;
                            var hash = FileHash.FromFile(srcImg);

                            ImagesEn.Add(src, (size, hash, url));
                        }
                        else
                        {
                            copy = false;
                        }
                    }
                    else if (ImagesEn.TryGetValue(src, out var enImg) && new FileInfo(srcImg).Length == enImg.size && FileHash.FromFile(srcImg) == enImg.hash)
                    {
                        url = enImg.url;
                        copy = false;
                    }
                }

                img.SetAttribute("src", url);
            }
            else
            {
                var srcImgEn = $"{SourceFolderEn}{srcImg.AsSpan(SourceFolderEn.Length)}";
                copy = false;

                if (!File.Exists(srcImgEn))
                {
                    ReportProblem(sourceFile, $"Image src not found: {src}", img.SourceReference?.Position);
                }

                img.SetAttribute("src", $"/{BookUrlName}/en/{src.AsSpan("../".Length)}");
            }

            if (copy)
            {
                var dstImg = Path.Combine(OutputFolder, language, src["../".Length..]);

                sep = dstImg.AsSpan().IndexOfAny("?#");
                if (sep > -1)
                {
                    dstImg = dstImg[..sep];
                }

                var dstImgDir = Path.GetDirectoryName(dstImg)!;
                Directory.CreateDirectory(dstImgDir);

                File.Copy(srcImg, dstImg, overwrite: true);
            }
        }
        else if (!Uri.IsWellFormedUriString(src, UriKind.Absolute))
        {
            ReportProblem(sourceFile, $"Unrecognized src: {src}", img.SourceReference?.Position);
        }
    }

    protected async Task<string> GenerateLayoutAsync(string language, string? rootUrlPrefix = null)
    {
        var langDir = Directory.CreateDirectory(Path.Combine(OutputFolder, language));
        var layoutHtml = await Template.RenderDocumentPageAsync(new DocumentPageModel
        {
            RootUrlPrefix = rootUrlPrefix,
            Language = language,
            AvailableLanguages = AvailableLanguages,
            BookUrlName = BookUrlName,
        });

        File.WriteAllText(Path.Combine(langDir.FullName, "layout.html"), layoutHtml);

        var layoutScripts = await Template.RenderApplyLayoutScriptsAsync(new ApplyLayoutModel
        {
            LayoutPageUrl = rootUrlPrefix + '/'.TryPrefixEach(BookUrlName, language, $"layout.html?v={FileHash.FromString(layoutHtml)}"),
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