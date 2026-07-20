using System.Buffers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace OriginLab.DocumentGeneration.Resolvers;

internal abstract partial class DocsResourceResolver : IDocResourceResolver
{
    private readonly DocsTransformationArgs Args;
    protected string SourceFolder => Args.SourceFolder;
    protected string OutputFolder => Args.OutputFolder;
    protected string BooksXmlFolder => Args.BooksXmlFolder;
    protected string SharedImagesFolder => Args.SharedImagesFolder;

    protected Dictionary<string, (string url, ulong hash)> SharedImages => field ??= GetSharedImages();

    protected Dictionary<string, string> MovedPages => field ??= GetMovedPages();

    public string[] AvailableLanguages { get; }

    public virtual string Language
    {
        get;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            var index = AvailableLanguages.IndexOf(value, StringComparer.OrdinalIgnoreCase);
            if (index < 0)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(index, 0, nameof(value));
            }

            LanguageIndex = index;
            field = value;
        }
    } = "en";

    private int LanguageIndex;

    protected readonly Dictionary<string, (string book, string url)> PageLinks;

    protected readonly Dictionary<string, List<string>> PageTitles;

    public DocsResourceResolver(DocsTransformationArgs args)
    {
        var languages = (from subPath in Directory.EnumerateDirectories(args.SourceFolder)
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

        var pages = new List<(string file, string book, string url, List<string> titles)>();

        foreach (var xmlFile in Directory.EnumerateFiles(args.BooksXmlFolder, "*.xml"))
        {
            var dirName = Path.GetFileNameWithoutExtension(xmlFile);

            foreach (var p in XElement.Load(xmlFile).Descendants("page"))
            {
                var file = $"{dirName}/{p.Attribute("file")!.Value}";
                var url = p.Attribute("url")!.Value;
                var sep = url.IndexOf('/');
                var title = p.Attribute("title")!.Value;

                pages.Add((file, book: sep < 0 ? url : url[..sep], url: sep < 0 ? "" : url[(sep + 1)..], [title]));
            }
        }

        var srcEnDir = Directory.EnumerateDirectories(Path.Combine(args.SourceFolder, "en")).Single();
        var bookName = Path.GetFileName(srcEnDir);

        for (int i = 1; i < languages.Length; i++)
        {
            var language = languages[i];

            foreach (var (file, book, url, titles) in pages)
            {
                var srcFile = Path.Combine(args.SourceFolder, language, bookName, file);

                if (!File.Exists(srcFile) && !File.Exists(srcFile = Path.Combine(srcEnDir, file))
                    || ReadPageTitle(srcFile) is not string title)
                {
                    title = titles[0];
                }

                titles.Add(title);
            }
        }

        PageLinks = pages.ToDictionary(p => p.file, p => (p.book.ToLowerInvariant(), p.url.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
        PageTitles = pages.ToDictionary(p => $"{p.book}/{p.url}", p => p.titles, StringComparer.OrdinalIgnoreCase);
        AvailableLanguages = languages;
        Args = args;
    }

    protected abstract (string url, ulong hash) GetSharedImageSrc(string path, string fileName);

    private Dictionary<string, (string url, ulong hash)> GetSharedImages()
    {
        var images = new Dictionary<string, (string url, ulong hash)>();

        foreach (var path in Directory.EnumerateFiles(SharedImagesFolder))
        {
            var fileName = Path.GetFileName(path);
            images.Add(fileName, GetSharedImageSrc(path, fileName));
        }

        return images;
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

    public abstract bool TryResolveHref(string href, string sourceFile, out string uri);

    public abstract bool TryResolveSrc(string src, string sourceFile, out string uri, out (string src, string dst)? copy);

    public virtual string? GetTitle(string uri)
    {
        if (PageTitles.TryGetValue(uri, out var titles))
        {
            return titles[LanguageIndex];
        }

        return null;
    }

    internal string? GetPageTitle(string sourceFile)
    {
        return null;
    }

    internal static string? ReadPageTitle(string sourceFile)
    {
        string? title = null;

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

        ArrayPool<char>.Shared.Return(buffer);

        return title;
    }

    [GeneratedRegex(@"<h1[^>]*>.*?</h1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HeaderRegex { get; }
}
