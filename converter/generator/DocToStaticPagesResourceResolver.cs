using System.Diagnostics;
using System.Net;
using System.Xml.Linq;

namespace OriginLab.DocumentGeneration;

internal class DocToStaticPagesResourceResolver : DocResourceResolver, IDocResourceResolver
{
    private readonly DocToStaticPagesTransformerArgs Args;

    private string BookUrlName => Args.BookUrlName;
    private bool UseWebp => Args.UseWebp;

    private readonly string SourceFolderEn;

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

    private readonly Dictionary<string, string> VisitedImages = new(StringComparer.OrdinalIgnoreCase);

    public DocToStaticPagesResourceResolver(DocToStaticPagesTransformerArgs args) : base(args)
    {
        SourceFolderEn = Path.Combine(args.SourceFolder, "en");
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

    protected override string GetSharedImageSrc(string path, string fileName)
        => $"/books/images/{fileName}?v={FileHash.StringFromFile(path)}";

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
        var needsCopy = true;

        var fileName = Path.GetFileName(path);
        if (SharedImages.TryGetValue(fileName, out result!))
        {
            needsCopy = false;
        }
        else if (srcImg.Exists)
        {
            result = '/'.TryPrefixEach(BookUrlName, Language, path[indexOfImages..]);

            if (Language == "en")
            {
                if (!EnglishImages.TryGetValue(path, out var visited))
                {
                    var size = srcImg.Length;
                    var hash = FileHash.UInt64FromFile(srcImg.FullName);

                    EnglishImages.Add(path, (size, hash, result));
                }
                else
                {
                    result = visited.url;
                    needsCopy = false;
                }
            }
            else
            {
                if (VisitedImages.TryGetValue(path, out var prevUrl))
                {
                    result = prevUrl;
                    needsCopy = false;
                }
                else
                {
                    VisitedImages.Add(path, result);

                    if (EnglishImages.TryGetValue(path, out var visited) && srcImg.Length == visited.size && FileHash.UInt64FromFile(srcImg.FullName) == visited.hash)
                    {
                        result = VisitedImages[path] = visited.url;
                        needsCopy = false;
                    }
                }
            }
        }
        else
        {
            var srcImgEn = $"{SourceFolderEn}{srcImg.FullName.AsSpan(SourceFolderEn.Length)}";

            if (!File.Exists(srcImgEn))
            {
                result = "Image src not found";
                copy = null;
                return false;
            }

            result = '/'.TryPrefixEach(BookUrlName, "en", path[indexOfImages..]);
            needsCopy = false;
        }

        if (UseWebp)
        {
            var resultDir = Path.GetDirectoryName(result.AsSpan());
            var resultFileName = Path.GetFileNameWithoutExtension(result.AsSpan());

            result = $"{resultDir}/{resultFileName}.webp";
        }

        if (!needsCopy)
        {
            copy = null;
        }
        else
        {
            var dstImg = Path.Combine(OutputFolder, Language, path[indexOfImages..]);
            copy = (srcImg.FullName, dstImg);
        }

        if (!parts.Query.IsEmpty)
        {
            result = $"{result}{parts.Query}";
        }

        return true;
    }
}
