using System.Diagnostics;
using System.Net;

namespace OriginLab.DocumentGeneration.Resolvers;

internal sealed partial class DocsResourceGithubPagesResolver : DocsResourceResolver
{
    private readonly DocsToStaticPagesTransformationArgs Args;

    private string BaseUrl => Args.BaseUrl;

    private bool UseWebp => Args.UseWebp;

    public override string Language
    {
        get => base.Language;
        set
        {
            base.Language = value;
            VisitedImages.Clear();
        }
    }

    private readonly Dictionary<string, (long size, ulong hash, string pageUrl)> EnglishImages = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, (string url, ulong hash)> VisitedImages = new(StringComparer.OrdinalIgnoreCase);

    public DocsResourceGithubPagesResolver(DocsToStaticPagesTransformationArgs args) : base(args)
    {
        Args = args;
    }

    protected override (string url, ulong hash) GetSharedImageSrc(string path, string fileName)
        => ($"/books/images/{fileName}", FastHash.FromFile(path));

    public override bool TryResolveHref(string href, string sourceFile, out string uri)
    {
        var parts = new UrlParts(href);

        if (parts.IsAbosolute || href.StartsWith('/') || href.StartsWith('#'))
        {
            uri = href;
            return true;
        }

        var path = parts is { Query.Length: 0, Hash.Length: 0 } ? href : parts.Path.ToString();
        Debug.Assert(!path.IsEmpty);

        var fullPath = Path.GetFullPath(path, Path.GetDirectoryName(sourceFile)!);

        if (TryGetLink(fullPath, true, out var link))
        {
            if (Language == "en")
            {
                uri = '/'.TrySurroundEach(link.book, link.url);
            }
            else
            {
                uri = '/'.TrySurroundEach(link.book, link.url, Language);
            }

            if (!parts.Query.IsEmpty || !parts.Hash.IsEmpty)
            {
                uri = $"{uri}{parts.Query}{parts.Hash}";
            }

            return true;
        }

        uri = "Unknown href mapping";
        return false;
    }

    private bool TryGetLink(string fullPath, bool urlDecode, out (string book, string url) link)
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

    public override bool TryResolveSrc(string src, string sourceFile, out string uri, out (string src, string dst)? copy)
    {
        src = src.Replace('\\', '/');

        var parts = new UrlParts(src);

        if (parts.IsAbosolute || src.StartsWith('/'))
        {
            uri = src;
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
            uri = sharedImage.url;
            hash = sharedImage.hash;
            needsCopy = false;
        }
        else if (Language == "en")
        {
            if (TryGetEnglishPageUrl(out uri))
            {
                uri = uri == page.url ? $"images/{name}" : $"/{BaseUrl}/{uri}/images/{name}";
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

            if (TryGetEnglishPageUrl(out uri))
            {
                uri = uri == page.url ? $"../images/{name}" : $"/{BaseUrl}/{uri}/images/{name}";
            }
            else
            {
                copy = null;
                return false;
            }
        }
        else if (!VisitedImages.TryGetValue(path, out var visitedImage))
        {
            uri = $"images/{name}";

            if (EnglishImages.TryGetValue(name, out var enImage)
                && srcImg.Length == enImage.size
                && FastHash.FromFile(srcImg.FullName) == enImage.hash)
            {
                uri = enImage.pageUrl == page.url ? $"../images/{name}" : $"/{BaseUrl}/{enImage.pageUrl}/images/{name}";
                hash = enImage.hash;
                needsCopy = false;
            }

            if (hash == 0)
            {
                hash = FastHash.FromFile(srcImg.FullName);
            }

            VisitedImages.Add(path, (uri, hash));
        }
        else
        {
            // Because `path` contains the page file name, and VisitedImages are cleared when Language changes.
            // VistedImages will not be asked from another page or another language. We can reuse the url.

            uri = visitedImage.url;
            hash = visitedImage.hash;
            needsCopy = false;
        }

        if (UseWebp)
        {
            var resultDir = Path.GetDirectoryName(uri.AsSpan());
            var resultFileName = Path.GetFileNameWithoutExtension(uri.AsSpan());

            uri = $"{resultDir}/{resultFileName}.webp";
        }

        uri = $"{uri}?v={FastHash.ToBase64Url(hash)}";

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
