using System.Buffers;
using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal abstract partial class DocToStaticPagesTransformer : DocTransformer, IDocTransformer
{
    public const int MaxSiblingNodes = 10 * 2;

    private readonly bool UseWebp;


    #region Language specific members

    protected string Language { get; private set; } = null!;

    private INode[] LayoutNodes = null!;

    private readonly Dictionary<string, string> VisitedImages = new(StringComparer.OrdinalIgnoreCase);

    #endregion


    protected DocToStaticPagesTransformer(DocToStaticPagesTransformerArgs args, ProblemRecorder problems) : base(args, problems)
    {
        UseWebp = args.UseWebp;
    }

    public async Task TransformAsync()
    {
        foreach (var language in AvailableLanguages)
        {
            var html = await InitializeLanguageLayoutAsync(language);

            var langDir = Directory.CreateDirectory(Path.Combine(OutputFolder, language));
            await File.WriteAllTextAsync(Path.Combine(langDir.FullName, "layout.html"), html);

            await TransformAsync(language);
        }

        File.WriteAllText(Path.Combine(OutputFolder, "404.html"), await Template.Render404PageAsync());
    }

    internal async Task<string> InitializeLanguageLayoutAsync(string language)
    {
        var parser = new HtmlParser();
        var layout = parser.ParseDocument("<html></html>");

        var html = await Template.RenderDocumentPageAsync(new DocumentPageModel
        {
            Language = language,
            AvailableLanguages = AvailableLanguages,
            BookUrlName = BookUrlName,
        });

        var scripts = await Template.RenderApplyLayoutScriptsAsync(new ApplyLayoutModel
        {
            LayoutPageUrl = '/'.TryPrefixEach(BookUrlName, language, $"layout.html?v={FileHash.FromString(html)}"),
        });

        Language = language;
        LayoutNodes = parser.ParseFragment(scripts, layout.Head!).ToArray();
        VisitedImages.Clear();

        return html;
    }

    protected internal override void Transform(IHtmlDocument document, IHtmlHeadElement head, IHtmlBodyElement body, string sourceFile)
    {
        base.Transform(document, head, body, sourceFile);

        head.PrependNodes(LayoutNodes);

        var loading = document.CreateElement<IHtmlDivElement>();
        loading.ClassName = "loading";

        var mainContent = document.CreateElement<IHtmlDivElement>();
        var placeholder = document.CreateElement<IHtmlDivElement>();

        mainContent.Id = "main-content";
        mainContent.AppendChild(placeholder);

        placeholder.Replace(body.ChildNodes.ToArray());
        body.AppendNodes(loading, mainContent);
    }

    protected override bool TryResolveHref(string sourceDir, string href, out string result, out string? titleEn)
    {
        titleEn = null;

        if (!href.StartsWith('/') && Uri.IsWellFormedUriString(href, UriKind.Relative))
        {
            var fullPath = Path.GetFullPath(href, sourceDir);
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

                    titleEn = link.titleEn;
                    return true;
                }
            }

            result = "Unknown href mapping";
            return false;
        }
        else if (href.StartsWith("mailto:") || href.StartsWith("javascript:") || Uri.IsWellFormedUriString(href, UriKind.Absolute))
        {
            result = href;
            return true;
        }

        result = "Unrecognized href pattern";
        return false;
    }

    protected override void TransformImage(IHtmlImageElement img, string sourceFile, string sourceDir)
    {
        if (img.GetAttribute("src") is not string srcFull)
        {
            return;
        }

        var src = srcFull;
        var sep = srcFull.AsSpan().IndexOfAny("?#");
        if (sep > -1)
        {
            src = srcFull[..sep];
        }

        if (src.StartsWith("../images/"))
        {
            img.SetAttribute("loading", "lazy");

            var srcImg = new FileInfo(Path.GetFullPath(src, sourceDir));
            var copy = true;

            var fileName = Path.GetFileName(src.AsSpan());
            if (SharedImages.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(fileName, out var url))
            {
                copy = false;
            }
            else if (srcImg.Exists)
            {
                url = '/'.TryPrefixEach(BookUrlName, Language, srcFull["../".Length..]);

                if (Language == "en")
                {
                    if (!EnglishImages.TryGetValue(src, out var visited))
                    {
                        var size = srcImg.Length;
                        var hash = FileHash.UInt64FromFile(srcImg.FullName);

                        EnglishImages.Add(src, (size, hash, url));
                    }
                    else
                    {
                        url = visited.url;
                        copy = false;
                    }
                }
                else
                {
                    if (VisitedImages.TryGetValue(src, out var prevUrl))
                    {
                        url = prevUrl;
                        copy = false;
                    }
                    else
                    {
                        VisitedImages.Add(src, url);

                        if (EnglishImages.TryGetValue(src, out var visited) && srcImg.Length == visited.size && FileHash.UInt64FromFile(srcImg.FullName) == visited.hash)
                        {
                            url = VisitedImages[src] = visited.url;
                            copy = false;
                        }
                    }
                }
            }
            else
            {
                var srcImgEn = $"{SourceFolderEn}{srcImg.FullName.AsSpan(SourceFolderEn.Length)}";

                if (!File.Exists(srcImgEn))
                {
                    ReportProblem(sourceFile, "Image src not found", src, img.SourceReference?.Position);
                }

                url = '/'.TryPrefixEach(BookUrlName, "en", srcFull["../".Length..]);
                copy = false;
            }

            if (UseWebp)
            {
                sep = url.AsSpan().IndexOfAny("?#");

                if (sep > -1)
                {
                    var dot = url.AsSpan(..sep).LastIndexOf('.');
                    url = $"{url.AsSpan(..dot)}.webp{url.AsSpan(sep)}";
                }
                else
                {
                    var dot = url.AsSpan().LastIndexOf('.');
                    url = $"{url.AsSpan(..dot)}.webp";
                }
            }

            img.SetAttribute("src", url);

            if (copy)
            {
                var dstImg = Path.Combine(OutputFolder, Language, src["../".Length..]);

                Directory.CreateDirectory(Path.GetDirectoryName(dstImg)!);

                File.Copy(srcImg.FullName, dstImg, overwrite: true);
            }
        }
        else if (!Uri.IsWellFormedUriString(srcFull, UriKind.Absolute))
        {
            ReportProblem(sourceFile, "Unrecognized src", src, img.SourceReference?.Position);
        }

    }
}