using System.Xml.Linq;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal sealed class BookTransformer : Transformer
{
    private readonly string BookDirName;
    private readonly (string url, string file)[] Pages;

    public BookTransformer(string booksXmlFolder, string sourceFolder, string outputFolder)
        : base(booksXmlFolder, sourceFolder, outputFolder)
    {
        BookDirName = Path.GetFileName(Directory.EnumerateDirectories(Path.Combine(SourceFolder, "en")).Single());

        var bookXml = XElement.Load(Path.Combine(sourceFolder, "en", BookDirName, "book.xml"));

        Pages = (from p in bookXml.Descendants("page")
                 let url = p.Attribute("url")!.Value
                 let file = p.Attribute("file")!.Value
                 select ((url.Length == BookUrlName.Length ? "" : url[(BookUrlName.Length + 1)..]).ToLowerInvariant(), file)).ToArray();

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

        foreach (var (url, file) in Pages)
        {
            var dstDir = Path.Combine(OutputFolder, url, language != "en" ? language : "");
            Directory.CreateDirectory(dstDir);

            var srcFile = Path.Combine(srcDir, file);
            var dstFile = Path.Combine(dstDir, "index.html");

            if (File.Exists(srcFile))
            {
                Transform(srcFile, dstFile, language, layoutScripts);
            }
            else if (language != "en" && File.Exists(srcFile = Path.Combine(srcEnDir, file)))
            {
                fallbackBanner ??= await Template.RenderEnFallbackBannerAsync(language);
                Transform(srcFile, dstFile, language, layoutScripts, fallbackBanner);
            }
            else
            {
                ReportProblem("en/book.xml", $"Source file not found: {srcFile}");
            }
        }
    }
}
