using System.Diagnostics;
using System.Net;
using System.Xml.Linq;

namespace OriginLab.DocumentGeneration;

internal sealed partial class DocToStaticPagesResourceResolver : DocResourceResolver, IDocResourceResolver
{
    private readonly DocToStaticPagesTransformerArgs Args;

    private string BookUrlName => Args.BookUrlName;
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

    private readonly Dictionary<string, (long size, ulong hash, string url)> EnglishImages = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, (string url, ulong hash)> VisitedImages = new(StringComparer.OrdinalIgnoreCase);

    public DocToStaticPagesResourceResolver(DocToStaticPagesTransformerArgs args) : base(args)
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

    public bool TryResolveHref(string href, string sourceDir, out string result, out string? titleEn)
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

        var fullPath = Path.GetFullPath(path, sourceDir);
        if (fullPath.StartsWith(SourceFolder)
            && Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(fullPath.AsSpan()))) is { IsEmpty: false } targetBookDirContainer)
        {
            var targetFile = WebUtility.UrlDecode(fullPath[(targetBookDirContainer.Length + 1)..].Replace('\\', '/'));

            if (PageLinks.TryGetValue(targetFile, out var link)
                || (MovedPages.TryGetValue(targetFile, out var movedToFile) && PageLinks.TryGetValue(movedToFile, out link)))
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
        }

        result = "Unknown href mapping";
        return false;
    }

    public bool TryResolveSrc(string src, string sourceDir, out string result, out (string src, string dst)? copy)
    {
        var parts = new UrlParts(src);

        if (parts.IsAbosolute || src.StartsWith('/'))
        {
            result = src;
            copy = null;
            return true;
        }

        var path = parts is { Query.Length: 0, Hash.Length: 0 } ? src : parts.Path.ToString();
        Debug.Assert(!path.IsEmpty);

        var indexOfImages = path.IndexOf("images/");
        Debug.Assert(indexOfImages > -1);

        var srcImg = new FileInfo(Path.GetFullPath(path, sourceDir));
        var hash = 0UL;
        var needsCopy = true;

        bool TryResolveEnglishImage(out string result)
        {
            if (!srcImg.Exists)
            {
                result = "Image src not found";
                return false;
            }

            if (EnglishImages.TryGetValue(path, out var enImage))
            {
                result = enImage.url;
                hash = enImage.hash;
                needsCopy = false;
            }
            else
            {
                result = '/'.TryPrefixEach(BookUrlName, "en", path[indexOfImages..]);

                var size = srcImg.Length;
                hash = FastHash.FromFile(srcImg.FullName);

                EnglishImages.Add(path, (size, hash, result));
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
            if (!TryResolveEnglishImage(out result))
            {
                copy = null;
                return false;
            }
        }
        else
        {
            if (!srcImg.Exists)
            {
                srcImg = new FileInfo($"{SourceFolder}/en/{srcImg.FullName.AsSpan(SourceFolder.Length + 4)}");

                if (!TryResolveEnglishImage(out result))
                {
                    copy = null;
                    return false;
                }
            }
            else
            {
                if (VisitedImages.TryGetValue(path, out var visitedImage))
                {
                    result = visitedImage.url;
                    hash = visitedImage.hash;
                    needsCopy = false;
                }
                else
                {
                    result = '/'.TryPrefixEach(BookUrlName, Language, path[indexOfImages..]);

                    if (EnglishImages.TryGetValue(path, out var enImage)
                        && srcImg.Length == enImage.size
                        && FastHash.FromFile(srcImg.FullName) == enImage.hash)
                    {
                        result = enImage.url;
                        hash = enImage.hash;
                        needsCopy = false;
                    }

                    if (hash == 0)
                    {
                        hash = FastHash.FromFile(srcImg.FullName);
                    }

                    VisitedImages.Add(path, (result, hash));
                }
            }
        }

        if (UseWebp)
        {
            var resultDir = Path.GetDirectoryName(result.AsSpan());
            var resultFileName = Path.GetFileNameWithoutExtension(result.AsSpan());

            result = $"{resultDir}/{resultFileName}.webp";
        }

        result = $"{result}?v={FastHash.ToBase64Url(hash)}";
        copy = !needsCopy ? null : (srcImg.FullName, Path.Combine(OutputFolder, Language, path[indexOfImages..]));

        return true;
    }
}
