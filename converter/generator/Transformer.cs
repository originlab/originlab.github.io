using System.Runtime.InteropServices;
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

    readonly Dictionary<string, List<(string message, TextPosition? position)>> Problems = [];
    readonly Dictionary<string, int> ProblemCounts = [];

    protected Transformer(string booksXmlFolder, string sourceFolder, string outputFolder)
    {
        var languages = (from subPath in Directory.EnumerateDirectories(sourceFolder)
                         let name = Path.GetFileName(subPath)
                         where name.Length == 2
                         select name).ToArray();

        if (!languages.Contains("en"))
            throw new FileNotFoundException("Expect en folder exists within source book", Path.Combine(sourceFolder, "en"));

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
        var bannerNodes = String.IsNullOrWhiteSpace(bannerHtml) ? null : parser.ParseFragment(bannerHtml, document.Body!);

        Transform(document, sourceFile, nav, language, headerNodes, bannerNodes);

        using var sw = new StreamWriter(destinationFile);
        document.ToHtml(sw, HtmlMarkupFormatter.Instance);
    }

    void Transform(IHtmlDocument document, string sourceFile, Nav nav, string language, INodeList headerNodes, INodeList? bannerNodes)
    {
        if (document.QuerySelector("h1.firstHeading") is IElement firstHeading)
        {
            document.Title = firstHeading.Text();
        }

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

        var familyDiv = CreateFamilyDiv(document, nav, sourceDir, language);
        body.AppendChild(familyDiv);

        var loading = document.CreateElement<IHtmlDivElement>();
        loading.ClassName = "loading";

        var mainContent = document.CreateElement<IHtmlDivElement>();
        var placeholder = document.CreateElement<IHtmlDivElement>();

        mainContent.Id = "main-content";
        mainContent.AppendChild(placeholder);

        placeholder.Replace(body.ChildNodes.ToArray());
        body.AppendNodes(loading, mainContent);
    }

    private IHtmlDivElement CreateFamilyDiv(IHtmlDocument document, Nav nav, string sourceDir, string language)
    {
        var familyDiv = document.CreateElement<IHtmlDivElement>();

        familyDiv.Id = "doc-family-data";
        familyDiv.IsHidden = true;

        if (!String.IsNullOrEmpty(nav.Parent))
        {
            if (TryResolveHref(sourceDir, language, "../" + nav.Parent, out var url, out var _))
            {
                familyDiv.SetAttribute("data-parent-link", url);
            }
        }
        else
        {
            familyDiv.SetAttribute("data-parent-link", language == "en" ? "/" : $"/{language}");
        }

        if (nav.Siblings is not null)
        {
            var siblingsUl = document.CreateElement<IHtmlUnorderedListElement>();

            siblingsUl.Id = "doc-siblings-data";

            familyDiv.AppendChild(siblingsUl);

            foreach (var sibling in nav.Siblings)
            {
                if (TryResolveHref(sourceDir, language, "../" + sibling, out var url, out var titleEn))
                {
                    var a = document.CreateElement<IHtmlAnchorElement>();
                    var li = document.CreateElement<IHtmlListItemElement>();

                    li.AppendChild(a);
                    siblingsUl.AppendChild(li);

                    a.SetAttribute("href", url);
                    a.TextContent = titleEn ?? "";
                }
            }
        }

        return familyDiv;
    }

    protected virtual void TransformAnchor(IHtmlAnchorElement a, string sourceFile, string language, string sourceDir)
    {
        if (a.GetAttribute("href") is string href && !String.IsNullOrWhiteSpace(href))
        {
            if (TryResolveHref(sourceDir, language, href, out var result, out var title))
            {
                a.SetAttribute("href", result);

                if (!String.IsNullOrEmpty(title))
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

        string? hash = null;
        var hashIndex = href.IndexOf('#');
        if (hashIndex == 0)
        {
            result = href;
            return true;
        }
        else if (hashIndex > 0)
        {
            hash = href[hashIndex..];
            href = href[..hashIndex];
        }

        if (!href.StartsWith('/') && Uri.IsWellFormedUriString(href, UriKind.Relative))
        {
            var fullPath = Path.GetFullPath(href, sourceDir);
            if (fullPath.StartsWith(SourceFolder)
                && Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(fullPath.AsSpan()))) is { IsEmpty: false } targetBookDirContainer)
            {
                var targetFile = fullPath[(targetBookDirContainer.Length + 1)..].Replace('\\', '/');

                if (PageLinks.TryGetValue(targetFile, out var link)
                    || (MovedPages.TryGetValue(targetFile, out var movedToFile) && PageLinks.TryGetValue(movedToFile, out link)))
                {
                    if (language == "en")
                    {
                        result = $"/{link.book}/{link.url}{hash}";
                    }
                    else
                    {
                        result = $"/{link.book}/{link.url}/{language}{hash}";
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

            var sep = srcImg.AsSpan().IndexOfAny("?#");
            if (sep > -1)
            {
                srcImg = srcImg[..sep];
            }

            if (File.Exists(srcImg))
            {
                img.SetAttribute("src", $"/{BookUrlName}/{language}/{src.AsSpan("../".Length)}");
            }
            else
            {
                var srcImgEn = $"{SourceFolderEn}{srcImg.AsSpan(SourceFolderEn.Length)}";

                if (!File.Exists(srcImgEn))
                {
                    ReportProblem(sourceFile, $"Image src not found: {src}", img.SourceReference?.Position);
                }

                img.SetAttribute("src", $"/{BookUrlName}/en/{src.AsSpan("../".Length)}");

                return;
            }

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
            LayoutPageUrl = $"{rootUrlPrefix}/{(String.IsNullOrEmpty(BookUrlName) ? "" : $"{BookUrlName}/")}{language}/layout.html?v={FileHash.FromString(layoutHtml)}",
            PlaceHolderId = "doc-content-placeholder",
            MainContentId = "main-content",
        });

        return layoutScripts;
    }

    protected void ReportProblem(string sourcePath, string message, TextPosition? position = null)
    {
        var file = Path.GetRelativePath(SourceFolder, sourcePath);

        if (!Problems.TryGetValue(file, out var detailsList))
        {
            Problems[file] = detailsList = [];
        }

        detailsList.Add((message, position));

        ref int count = ref CollectionsMarshal.GetValueRefOrAddDefault(ProblemCounts, message, out _);
        count++;
    }

    public void PrintProblems()
    {
        foreach (var (file, detailsList) in Problems)
        {
            Console.Error.WriteLine($"::group::{file}");

            foreach (var (message, position) in detailsList)
            {
                Console.Error.Write($"::warning file={file}");

                if (position is not null)
                {
                    Console.Error.Write($",line={position.Value.Line},col={position.Value.Column}");
                }

                Console.Error.Write("::");
                Console.Error.WriteLine(message);
            }

            Console.Error.WriteLine("::endgroup::");
        }

        if (ProblemCounts.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Summary:");

            foreach (var (message, count) in ProblemCounts.OrderByDescending(kvp => kvp.Value))
            {
                Console.Error.WriteLine($"{count}x {message}");
            }
        }
    }
}