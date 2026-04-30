using System.Runtime.InteropServices;
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
    protected readonly Dictionary<string, (string book, string url)> PageLinks;

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

        PageLinks = (from xmlFile in Directory.EnumerateFiles(booksXmlFolder, "*.xml")
                     let dirName = Path.GetFileNameWithoutExtension(xmlFile)
                     from p in XElement.Load(xmlFile).Descendants("page")
                     let url = p.Attribute("url")!.Value
                     let sep = url.IndexOf('/')
                     let file = $"{dirName}/{p.Attribute("file")!.Value}"
                     select (file, book: sep < 0 ? url : url[..sep], url: sep < 0 ? "" : url[(sep + 1)..]))
                     .ToDictionary(p => p.file, p => (p.book.ToLowerInvariant(), p.url.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

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

    protected void Transform(string sourceFile, string destinationFile, string language, string layoutScripts)
    {
        using var fs = new FileStream(sourceFile, FileMode.Open, FileAccess.Read);
        var parser = new HtmlParser(new HtmlParserOptions
        {
            IsKeepingSourceReferences = true
        });
        var document = parser.ParseDocument(fs);

        Transform(document, sourceFile, language, layoutScripts);

        using var sw = new StreamWriter(destinationFile);
        document.ToHtml(sw, HtmlMarkupFormatter.Instance);
    }

    void Transform(IHtmlDocument document, string sourceFile, string language, string layoutScripts)
    {
        if (document.QuerySelector("h1.firstHeading") is IElement firstHeading)
        {
            document.Title = firstHeading.Text();
        }

        document.Head!.InnerHtml += layoutScripts;

        var sourceDir = Path.GetDirectoryName(sourceFile)!;

        foreach (var a in document.Descendants<IHtmlAnchorElement>())
        {
            TransformAnchor(a, sourceFile, language, sourceDir);
        }

        foreach (var img in document.Descendants<IHtmlImageElement>())
        {
            TransformImage(img, sourceFile, language, sourceDir);
        }

        var loading = document.CreateElement<IHtmlDivElement>();
        loading.ClassName = "loading";

        var mainContent = document.CreateElement<IHtmlDivElement>();
        var placeholder = document.CreateElement<IHtmlDivElement>();

        mainContent.Id = "main-content";
        mainContent.AppendChild(placeholder);

        placeholder.Replace(document.Body!.ChildNodes.ToArray());
        document.Body.Replace(loading, mainContent);
    }

    protected virtual void TransformAnchor(IHtmlAnchorElement a, string sourceFile, string language, string sourceDir)
    {
        if (a.GetAttribute("href") is string href && !String.IsNullOrWhiteSpace(href))
        {
            string? hash = null;
            var hashIndex = href.IndexOf('#');
            if (hashIndex > -1)
            {
                hash = href[hashIndex..];
                href = href[..hashIndex];
            }

            if (!String.IsNullOrWhiteSpace(href))
            {
                if (Uri.IsWellFormedUriString(href, UriKind.Relative) && !href.StartsWith('/'))
                {
                    var fullPath = Path.GetFullPath(href, sourceDir);
                    if (fullPath.StartsWith(SourceFolder)
                        && Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(fullPath.AsSpan()))) is { IsEmpty: false } targetBookDirContainer
                        && PageLinks.TryGetValue(fullPath[(targetBookDirContainer.Length + 1)..].Replace('\\', '/'), out var link))
                    {
                        if (language == "en")
                        {
                            a.SetAttribute("href", $"/{link.book}/{link.url}{hash}");
                        }
                        else
                        {
                            a.SetAttribute("href", $"/{link.book}/{link.url}/{language}{hash}");
                        }

                        //if (String.IsNullOrWhiteSpace(a.Title) || a.Title.Contains(':'))
                        //{
                        //    a.Title = link.title;
                        //}
                    }
                    else
                    {
                        ReportProblem(sourceFile, $"Mapping unknown for href: {href}", a.SourceReference?.Position);
                    }
                }
                else if (!href.StartsWith('#')
                    && !href.StartsWith("mailto:")
                    && !href.StartsWith("javascript:")
                    && !Uri.IsWellFormedUriString(href, UriKind.Absolute))
                {
                    ReportProblem(sourceFile, $"Unrecognized href: {href}", a.SourceReference?.Position);
                }
            }
        }
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
            ContainerId = "doc-static-content-container",
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