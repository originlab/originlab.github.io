using System.Diagnostics;
using System.Net;
using System.Xml.Linq;

namespace OriginLab.DocumentGeneration.Resolvers;

internal sealed partial class DocsResourceGithubPagesResolver : DocsResourceResolver, IDocResourceResolver
{
    private readonly DocsToStaticPagesTransformationArgs Args;

    private string BaseUrl => Args.BaseUrl;

    private bool UseWebp => Args.UseWebp;

    public string Language
    {
        get;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            field = value;
            VisitedImages.Clear();
        }
    } = null!;

    private readonly Dictionary<string, (string book, string url, string titleEn)> PageLinks;

    private readonly Dictionary<string, (long size, ulong hash, string pageUrl)> EnglishImages = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, (string url, ulong hash)> VisitedImages = new(StringComparer.OrdinalIgnoreCase);

    public DocsResourceGithubPagesResolver(DocsToStaticPagesTransformationArgs args) : base(args)
    {
        Args = args;

        var pages = new List<(string file, string book, string url, string title)>();

        foreach (var xmlFile in Directory.EnumerateFiles(args.BooksXmlFolder, "*.xml"))
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

    protected override (string url, ulong hash) GetSharedImageSrc(string path, string fileName)
        => ($"/books/images/{fileName}", FastHash.FromFile(path));

    public bool TryResolveHref(string href, string sourceFile, out string result, out string? titleEn)
    {
        titleEn = null;

        var parts = new UrlParts(href);

        if (parts.IsAbosolute || href.StartsWith('/') || href.StartsWith('#'))
        {
            result = href;
            return true;
        }

        var path = parts is { Query.Length: 0, Hash.Length: 0 } ? href : parts.Path.ToString();
        Debug.Assert(!path.IsEmpty);

        var fullPath = Path.GetFullPath(path, Path.GetDirectoryName(sourceFile)!);

        if (TryGetLink(fullPath, true, out var link))
        {
            if (Language == "en")
            {
                result = '/'.TrySurroundEach(link.book, link.url);
            }
            else
            {
                result = '/'.TrySurroundEach(link.book, link.url, Language);
            }

            if (!parts.Query.IsEmpty || !parts.Hash.IsEmpty)
            {
                result = $"{result}{parts.Query}{parts.Hash}";
            }

            titleEn = link.titleEn;
            return true;
        }

        result = "Unknown href mapping";
        return false;
    }

    private bool TryGetLink(string fullPath, bool urlDecode, out (string book, string url, string titleEn) link)
    {
        if (!fullPath.StartsWith(SourceFolder)
            || Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(fullPath.AsSpan()))) is not { IsEmpty: false } targetBookDirContainer)
        {
            link = default;
            return false;
        }

        var targetFile = fullPath[(targetBookDirContainer.Length + 1)..];
        if (urlDecode)
        {
            targetFile = WebUtility.UrlDecode(targetFile);
        }

        targetFile = targetFile.Replace('\\', '/');

        return PageLinks.TryGetValue(targetFile, out link)
            || (MovedPages.TryGetValue(targetFile, out var movedToFile) && PageLinks.TryGetValue(movedToFile, out link));
    }

    public bool TryResolveSrc(string src, string sourceFile, out string result, out (string src, string dst)? copy)
    {
        src = src.Replace('\\', '/');

        var parts = new UrlParts(src);

        if (parts.IsAbosolute || src.StartsWith('/'))
        {
            result = src;
            copy = null;
            return true;
        }

        var path = parts is { Query.Length: 0, Hash.Length: 0 } ? src : parts.Path.ToString();
        Debug.Assert(!path.IsEmpty);

        var srcImg = new FileInfo(Path.GetFullPath(path, Path.GetDirectoryName(sourceFile)!));
        var name = Path.GetFileName(path);
        var hash = 0UL;
        var needsCopy = true;
        var found = TryGetLink(sourceFile, false, out var page);
        Debug.Assert(found);

        bool TryGetEnglishPageUrl(out string result)
        {
            if (!srcImg.Exists)
            {
                result = "Image src not found";
                return false;
            }

            if (EnglishImages.TryGetValue(name, out var enImage))
            {
                result = enImage.pageUrl;
                hash = enImage.hash;
                needsCopy = false;
            }
            else
            {
                result = page.url;

                var size = srcImg.Length;
                hash = FastHash.FromFile(srcImg.FullName);

                EnglishImages.Add(name, (size, hash, page.url));
            }

            return true;
        }

        if (SharedImages.TryGetValue(Path.GetFileName(path), out var sharedImage))
        {
            result = sharedImage.url;
            hash = sharedImage.hash;
            needsCopy = false;
        }
        else if (Language == "en")
        {
            if (TryGetEnglishPageUrl(out result))
            {
                result = result == page.url ? $"images/{name}" : $"/{BaseUrl}/{result}/images/{name}";
            }
            else
            {
                copy = null;
                return false;
            }
        }
        else if (!srcImg.Exists)
        {
            srcImg = new FileInfo($"{SourceFolder}/en/{srcImg.FullName.AsSpan(SourceFolder.Length + 4)}");

            if (TryGetEnglishPageUrl(out result))
            {
                result = result == page.url ? $"../images/{name}" : $"/{BaseUrl}/{result}/images/{name}";
            }
            else
            {
                copy = null;
                return false;
            }
        }
        else if (!VisitedImages.TryGetValue(path, out var visitedImage))
        {
            result = $"images/{name}";

            if (EnglishImages.TryGetValue(name, out var enImage)
                && srcImg.Length == enImage.size
                && FastHash.FromFile(srcImg.FullName) == enImage.hash)
            {
                result = enImage.pageUrl == page.url ? $"../images/{name}" : $"/{BaseUrl}/{enImage.pageUrl}/images/{name}";
                hash = enImage.hash;
                needsCopy = false;
            }

            if (hash == 0)
            {
                hash = FastHash.FromFile(srcImg.FullName);
            }

            VisitedImages.Add(path, (result, hash));
        }
        else
        {
            // Because `path` contains the page file name, and VisitedImages are cleared when Language changes.
            // VistedImages will not be asked from another page or another language. We can reuse the url.

            result = visitedImage.url;
            hash = visitedImage.hash;
            needsCopy = false;
        }

        if (UseWebp)
        {
            var resultDir = Path.GetDirectoryName(result.AsSpan());
            var resultFileName = Path.GetFileNameWithoutExtension(result.AsSpan());

            result = $"{resultDir}/{resultFileName}.webp";
        }

        result = $"{result}?v={FastHash.ToBase64Url(hash)}";

        if (!needsCopy)
        {
            copy = null;
        }
        else
        {
            var dst = Language == "en"
                ? Path.Combine(OutputFolder, page.url, "images", name)
                : Path.Combine(OutputFolder, page.url, Language, "images", name);

            copy = (srcImg.FullName, dst);
        }

        return true;
    }
}
