using System.Diagnostics;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal sealed class DocBookTransformer : DocToStaticPagesTransformer
{
    private readonly string AvailableLanguagesExpression;

    private readonly string BookDirName;
    private readonly (string url, string file, string titleEn, NavFiles navFiles)[] Pages;

    public DocBookTransformer(DocToStaticPagesTransformerArgs args, ProblemRecorder problems) : base(args, problems)
    {
        AvailableLanguagesExpression = String.Join(',', AvailableLanguages);
        BookDirName = Path.GetFileName(Directory.EnumerateDirectories(Path.Combine(SourceFolder, "en")).Single());

        var bookXml = XElement.Load(Path.Combine(SourceFolder, "en", BookDirName, "book.xml"));
        var pages = new List<(string url, string file, string titleEn, NavFiles nav)>();

        foreach (var p in bookXml.Descendants("page"))
        {
            var url = p.Attribute("url")!.Value;
            url = url.Length == BookUrlName!.Length ? "" : url[(BookUrlName.Length + 1)..];
            url = url.ToLowerInvariant();

            var file = p.Attribute("file")!.Value;
            var titleEn = p.Attribute("title")!.Value;

            var parent = p.Parent?.Attribute("file")?.Value;
            var siblings = GetSiblings(p);
            var children = p.Elements("page").Select(p => p.Attribute("file")!.Value).ToArray();

            pages.Add((url, file, titleEn, new NavFiles(parent, siblings, children)));
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

    protected override async Task TransformAsync(string language)
    {
        var srcDir = Path.Combine(SourceFolder, language, BookDirName);
        var srcEnDir = Path.Combine(SourceFolderEn, BookDirName);
        string? fallbackBanner = null;

        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, file, titleEn, _) in Pages)
        {
            var srcFile = Path.Combine(srcDir, file);

            if (File.Exists(srcFile) || (language != "en" && File.Exists(srcFile = Path.Combine(srcEnDir, file))))
            {
                titles.Add(file, GetPageTitle(srcFile));
            }
            else
            {
                titles.Add(file, titleEn);

                ReportProblem("en/book.xml", "Source file not found", srcFile);
            }
        }

        for (int i = 0; i < Pages.Length; i++)
        {
            var (url, file, _, navFiles) = Pages[i];
            var dstDir = Path.Combine(OutputFolder, url, language != "en" ? language : "");
            var nav = new Nav(navFiles, titles, i == 0);

            Directory.CreateDirectory(dstDir);

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
        Transform(srcFile, dstFile, (document, head, body, file) =>
        {
            if (bannerHtml is string banner)
            {
                var div = document.CreateElement<IHtmlDivElement>();
                div.InnerHtml = banner;

                body.PrependNodes(div.ChildNodes.ToArray());
            }

            var navDataDiv = CreateNavDataDiv(document, nav, Path.GetDirectoryName(file)!);
            body.AppendChild(navDataDiv);
        });
    }


    string? CachedSiblingsParent;
    int CachedSiblingsCurrentIndex;
    IHtmlElement? CachedSiblings;

    private IHtmlDivElement CreateNavDataDiv(IHtmlDocument document, in Nav nav, string sourceDir)
    {
        var navDataDiv = document.CreateElement<IHtmlDivElement>();

        navDataDiv.Id = "doc-nav-data";
        navDataDiv.IsHidden = true;

        navDataDiv.SetAttribute("data-lang", Language);
        navDataDiv.SetAttribute("data-lang-list", AvailableLanguagesExpression);

        var files = nav.Files;

        if (!files.Parent.IsEmpty)
        {
            if (TryResolveHref(sourceDir, "../" + files.Parent, out var url, out var _))
            {
                navDataDiv.SetAttribute("data-parent-link", url);
            }
        }
        else
        {
            navDataDiv.SetAttribute("data-parent-link", Language == "en" ? "/" : $"/{Language}");
        }

        if (nav.IsBookIndex)
        {
            navDataDiv.SetAttribute("data-book-index", BookUrlName);
        }

        if (files.Siblings is not null)
        {
            IHtmlElement ul;

            if (nav.Files.Parent is string parent && parent == CachedSiblingsParent)
            {
                ul = CreateSiblingsULFromCache(CachedSiblings!, files.Siblings, nav.Titles);
            }
            else
            {
                CachedSiblingsParent = nav.Files.Parent;
                CachedSiblings = ul = CreateDataUL("doc-siblings-data", files.Siblings, nav.Titles);
            }

            navDataDiv.AppendChild(ul);
        }

        if (files.Children is not null)
        {
            var ul = CreateDataUL("doc-children-data", files.Children, nav.Titles);
            navDataDiv.AppendChild(ul);
        }

        return navDataDiv;

        IHtmlElement CreateDataUL(string id, string[] files, Dictionary<string, string> titles)
        {
            var ul = document.CreateElement<IHtmlUnorderedListElement>();

            ul.Id = id;

            for (int i = 0; i < files.Length; i++)
            {
                string? path = files[i];
                var li = document.CreateElement<IHtmlListItemElement>();
                var a = document.CreateElement<IHtmlAnchorElement>();
                var pathSpan = path.AsSpan();
                var isCurrent = false;

                if (path.StartsWith('*'))
                {
                    pathSpan = pathSpan[1..];
                    isCurrent = true;
                    CachedSiblingsCurrentIndex = i;
                }

                if (TryResolveHref(sourceDir, $"../{pathSpan}", out var url, out var _))
                {
                    if (isCurrent)
                    {
                        li.ClassName = "disabled";
                    }
                    else
                    {
                        a.SetAttribute("href", url);
                    }

                    a.TextContent = titles.GetAlternateLookup<ReadOnlySpan<char>>()[pathSpan];
                }

                li.AppendChild(a);
                ul.AppendChild(li);
            }

            return ul;
        }

        IHtmlElement CreateSiblingsULFromCache(IHtmlElement ul, string[] files, Dictionary<string, string> titles)
        {
            document.AdoptNode(ul);

            var currentIdx = Array.FindIndex(files, CachedSiblingsCurrentIndex, files.Length - CachedSiblingsCurrentIndex, f => f.StartsWith('*'));
            var previousLi = ul.Children[CachedSiblingsCurrentIndex].SelfOrNextElementSibling(li => li.FirstElementChild!.GetAttribute("href").IsEmpty);

            Debug.Assert(currentIdx > 0);
            Debug.Assert(previousLi is not null);

            if (TryResolveHref(sourceDir, $"../{files[currentIdx - 1]}", out var url, out var _))
            {
                previousLi.FirstElementChild!.SetAttribute("href", url);
            }

            previousLi.ClassName = null;

            var currentLi = previousLi.NextElementSibling!;
            currentLi.FirstElementChild!.SetAttribute("href", null);
            currentLi.ClassName = "disabled";

            if (ul.ChildElementCount < files.Length)
            {
                var li = document.CreateElement<IHtmlListItemElement>();
                var a = document.CreateElement<IHtmlAnchorElement>();

                if (TryResolveHref(sourceDir, $"../{files[^1]}", out var endUrl, out var _))
                {
                    a.SetAttribute("href", endUrl);
                    a.TextContent = titles[files[^1]];
                }

                li.AppendChild(a);
                ul.AppendChild(li);
            }
            else if (currentIdx == MaxSiblingNodes / 2 && currentIdx != files.Length - 1 && currentIdx == CachedSiblingsCurrentIndex)
            {
                var li = (IHtmlElement)ul.RemoveChild(ul.FirstElementChild!);

                if (TryResolveHref(sourceDir, $"../{files[^1]}", out var endUrl, out var _)
                    && endUrl != ul.LastElementChild!.FirstElementChild!.GetAttribute("href"))
                {
                    var a = li.FirstElementChild!;

                    a.SetAttribute("href", endUrl);
                    a.TextContent = titles[files[^1]];

                    ul.AppendChild(li);
                }
            }

            CachedSiblingsCurrentIndex = currentIdx;
            return ul;
        }
    }

}
