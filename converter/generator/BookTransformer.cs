using System.Xml.Linq;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal sealed class BookTransformer : Transformer
{
    private readonly string BookDirName;
    private readonly (string url, string file, string? parent, string[]? siblings)[] Pages;

    public BookTransformer(string booksXmlFolder, string sourceFolder, string outputFolder)
        : base(booksXmlFolder, sourceFolder, outputFolder)
    {
        BookDirName = Path.GetFileName(Directory.EnumerateDirectories(Path.Combine(SourceFolder, "en")).Single());

        var bookXml = XElement.Load(Path.Combine(sourceFolder, "en", BookDirName, "book.xml"));
        var pages = new List<(string url, string file, string? parent, string[]? siblings)>();

        foreach (var p in bookXml.Descendants("page"))
        {
            var url = p.Attribute("url")!.Value;
            url = url.Length == BookUrlName!.Length ? "" : url[(BookUrlName.Length + 1)..];
            url = url.ToLowerInvariant();

            var file = p.Attribute("file")!.Value;
            var parent = p.Parent?.Attribute("file")?.Value;
            var siblings = p.Parent?.Elements("page").Take(10).Select(c => c.Attribute("file")!.Value).ToArray();

            pages.Add((url, file, parent, siblings));
        }

        Pages = pages.ToArray();
    }

    protected override string GetBookUrlName() => Path.GetFileName(SourceFolder).ToLowerInvariant();

    public override async Task TransformFilesAsync()
    {
        foreach (var language in AvailableLanguages)
        {
            var scripts = await GenerateLayoutAsync(language);
            await TransformAsync(language, scripts);
        }
    }

    async Task TransformAsync(string language, string layoutScripts)
    {
        var srcDir = Path.Combine(SourceFolder, language, BookDirName);
        var srcEnDir = Path.Combine(SourceFolderEn, BookDirName);
        string? fallbackBanner = null;

        foreach (var (url, file, parent, siblings) in Pages)
        {
            var dstDir = Path.Combine(OutputFolder, url, language != "en" ? language : "");
            Directory.CreateDirectory(dstDir);

            var srcFile = Path.Combine(srcDir, file);
            var dstFile = Path.Combine(dstDir, "index.html");

            if (File.Exists(srcFile))
            {
                Transform(srcFile, dstFile, parent, siblings, language, layoutScripts);
            }
            else if (language != "en" && File.Exists(srcFile = Path.Combine(srcEnDir, file)))
            {
                fallbackBanner ??= await Template.RenderEnglishFallbackBannerAsync(language);
                Transform(srcFile, dstFile, parent, siblings, language, layoutScripts, fallbackBanner);
            }
            else
            {
                ReportProblem("en/book.xml", $"Source file not found: {srcFile}");
            }
        }
    }
}
