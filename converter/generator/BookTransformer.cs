using System.Xml.Linq;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal sealed class BookTransformer : Transformer
{
    private readonly string BookDirName;
    private readonly (string url, string file, Nav nav)[] Pages;

    public BookTransformer(string booksXmlFolder, string sourceFolder, string outputFolder)
        : base(booksXmlFolder, sourceFolder, outputFolder)
    {
        BookDirName = Path.GetFileName(Directory.EnumerateDirectories(Path.Combine(SourceFolder, "en")).Single());

        var bookXml = XElement.Load(Path.Combine(sourceFolder, "en", BookDirName, "book.xml"));
        var pages = new List<(string url, string file, Nav nav)>();

        foreach (var p in bookXml.Descendants("page"))
        {
            var url = p.Attribute("url")!.Value;
            url = url.Length == BookUrlName!.Length ? "" : url[(BookUrlName.Length + 1)..];
            url = url.ToLowerInvariant();

            var file = p.Attribute("file")!.Value;
            var parent = p.Parent?.Attribute("file")?.Value;
            var siblings = GetSiblings(p);
            var children = p.Elements("page").Select(p => p.Attribute("file")!.Value).ToArray();

            pages.Add((url, file, new Nav(parent, siblings, children)));
        }

        Pages = pages.ToArray();

        static string[]? GetSiblings(XElement p)
        {
            var list = new List<string>();
            var next = p.PreviousNode;
            var count = 10;

            while (count-- > 0 && next is XElement element)
            {
                list.Add(element.Attribute("file")!.Value);
                next = element.PreviousNode;
            }

            if (list.Count > 0)
            {
                list.Reverse();
                list.Add("");
            }

            next = p.NextNode;
            count = 10;
            var added = false;

            while (count-- > 0 && next is XElement element)
            {
                list.Add(element.Attribute("file")!.Value);
                next = element.NextNode;
                added = true;
            }

            if (!added && list.Count > 0)
            {
                list.RemoveAt(list.Count - 1);
            }

            return list.ToArray();
        }
    }

    protected override string GetBookUrlName() => Path.GetFileName(SourceFolder).ToLowerInvariant();

    public override async Task TransformFilesAsync()
    {
        foreach (var language in AvailableLanguages)
        {
            await TransformAsync(language);
        }
    }

    async Task TransformAsync(string language)
    {
        var srcDir = Path.Combine(SourceFolder, language, BookDirName);
        var srcEnDir = Path.Combine(SourceFolderEn, BookDirName);
        string? fallbackBanner = null;

        var titles = Titles[language] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, file, _) in Pages)
        {
            var srcFile = Path.Combine(srcDir, file);

            if (File.Exists(srcFile) || (language != "en" && File.Exists(srcFile = Path.Combine(srcEnDir, file))))
            {
                titles.Add(file, GetPageTitle(srcFile));
            }
            else
            {
                ReportProblem("en/book.xml", $"Source file not found: {srcFile}");
            }
        }

        foreach (var (url, file, nav) in Pages)
        {
            var dstDir = Path.Combine(OutputFolder, url, language != "en" ? language : "");

            Directory.CreateDirectory(dstDir);

            var srcFile = Path.Combine(srcDir, file);
            var dstFile = Path.Combine(dstDir, "index.html");

            if (File.Exists(srcFile))
            {
                Transform(srcFile, dstFile, nav, language);
            }
            else if (language != "en" && File.Exists(srcFile = Path.Combine(srcEnDir, file)))
            {
                fallbackBanner ??= await Template.RenderEnglishFallbackBannerAsync(language);

                Transform(srcFile, dstFile, nav, language, bannerHtml: fallbackBanner);
            }
            else
            {
                ReportProblem("en/book.xml", $"Source file not found: {srcFile}");
            }
        }
    }
}
