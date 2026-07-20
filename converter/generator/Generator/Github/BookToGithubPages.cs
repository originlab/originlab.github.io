using System.Diagnostics;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using OriginLab.DocumentGeneration.Templates;
using OriginLab.DocumentGeneration.Transformers;

namespace OriginLab.DocumentGeneration.Generator.Github;

internal sealed class BookToGithubPages : DocsToGithubPages
{
    private const int MaxSiblingNodes = 10 * 2;
    private readonly string AvailableLanguagesExpression;

    private readonly string BookDirName;
    private readonly (string url, string file, NavFiles navFiles)[] Pages;

    private new DocumentToGithubPage Transformer => (DocumentToGithubPage)base.Transformer;

    public BookToGithubPages(DocsToStaticPagesTransformationArgs args, DocumentToGithubPage transformer, ProblemRecorder problems)
        : base(args.BaseUrl, args.SourceFolder, args.OutputFolder, transformer, problems)
    {
        AvailableLanguagesExpression = String.Join(',', Transformer.AvailableLanguages);
        BookDirName = Path.GetFileName(Directory.EnumerateDirectories(Path.Combine(SourceFolder, "en")).Single());

        var bookXml = XElement.Load(Path.Combine(SourceFolder, "en", BookDirName, "book.xml"));
        var pages = new List<(string url, string file, NavFiles nav)>();

        foreach (var p in bookXml.Descendants("page"))
        {
            var url = p.Attribute("url")!.Value;
            url = url.Length == BaseUrl!.Length ? "" : url[(BaseUrl.Length + 1)..];
            url = url.ToLowerInvariant();

            var file = p.Attribute("file")!.Value;

            var parent = p.Parent?.Attribute("file")?.Value;
            var siblings = GetSiblings(p);
            var children = p.Elements("page").Select(p => p.Attribute("file")!.Value).ToArray();

            pages.Add((url, file, new NavFiles(parent, siblings, children)));
        }

        Pages = pages.ToArray();

        static string[]? GetSiblings(XElement p)
        {
            var list = new List<string>();
            var next = p.PreviousNode;
            var count = MaxSiblingNodes / 2;

            while (count-- > 0 && next is XElement element)
            {
                list.Add(element.Attribute("file")!.Value);
                next = element.PreviousNode;
            }

            list.Reverse();
            list.Add("*" + p.Attribute("file")!.Value);

            next = p.NextNode;
            count = MaxSiblingNodes / 2;

            while (count-- > 0 && next is XElement element)
            {
                list.Add(element.Attribute("file")!.Value);
                next = element.NextNode;
            }

            return list.ToArray();
        }
    }

    protected override async Task TransformFilesAsync(string language)
    {
        await base.TransformFilesAsync(language);

        var srcDir = Path.Combine(SourceFolder, language, BookDirName);
        var srcEnDir = Path.Combine(SourceFolder, "en", BookDirName);
        string? fallbackBanner = null;

        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < Pages.Length; i++)
        {
            var (url, file, navFiles) = Pages[i];
            var dstDir = Path.Combine(OutputFolder, url, language != "en" ? language : "");
            var nav = new Nav(navFiles, titles, i == 0);

            var srcFile = Path.Combine(srcDir, file);
            var dstFile = Path.Combine(dstDir, "index.html");

            if (File.Exists(srcFile))
            {
                Transform(srcFile, dstFile, nav);
            }
            else if (language != "en" && File.Exists(srcFile = Path.Combine(srcEnDir, file)))
            {
                fallbackBanner ??= await Template.RenderEnglishFallbackBannerAsync(language);

                Transform(srcFile, dstFile, nav, bannerHtml: fallbackBanner);
            }
            else
            {
                ReportProblem("en/book.xml", "Source file not found", srcFile);
            }
        }
    }

    private void Transform(string srcFile, string dstFile, Nav nav, string? bannerHtml = null)
    {
        Transform(srcFile, dstFile, (document, file) =>
        {
            Debug.Assert(document.Body is not null);

            if (bannerHtml is string banner)
            {
                var div = document.CreateElement<IHtmlDivElement>();
                div.InnerHtml = banner;

                document.Body.PrependNodes(div.ChildNodes.ToArray());
            }

            var navDataDiv = Transformer.CreateNavDataDiv(document, nav, file, AvailableLanguagesExpression, BaseUrl);
            document.Body.AppendChild(navDataDiv);
        });
    }
}
