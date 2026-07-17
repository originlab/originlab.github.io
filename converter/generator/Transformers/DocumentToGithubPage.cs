using System.Buffers;
using System.Diagnostics;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using OriginLab.DocumentGeneration.Resolvers;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration.Transformers;

internal partial class DocumentToGithubPage : DocumentTransformer
{
    public const int MaxSiblingNodes = 10 * 2;

    protected INode[] LayoutScripts { get; private set; } = null!;

    public DocumentToGithubPage(IDocResourceResolver resourceResolver, ProblemRecorder problems)
        : base(resourceResolver, problems)
    {
    }

    public override async Task<string> InitializeLayoutAsync(DocumentPageModel model)
    {
        await base.InitializeLayoutAsync(model);

        var parser = new HtmlParser();
        var layout = parser.ParseDocument("<html></html>");

        var html = await Template.RenderDocumentPageAsync(model);
        var scripts = await Template.RenderApplyLayoutScriptsAsync(new ApplyLayoutModel
        {
            LayoutPageUrl = '/'.TryPrefixEach(model.BookUrlName, model.Language, $"layout.html?v={FastHash.ToBase64Url(FastHash.FromString(html))}"),
        });

        LayoutScripts = parser.ParseFragment(scripts, layout.Head!).ToArray();

        return html;
    }

    public override void Transform(IHtmlDocument document, string sourceFile)
    {
        base.Transform(document, sourceFile);

        Debug.Assert(document.Head is not null);
        Debug.Assert(document.Body is not null);

        document.Head.PrependNodes(LayoutScripts);

        var loading = document.CreateElement<IHtmlDivElement>();
        loading.ClassName = "loading";

        var mainContent = document.CreateElement<IHtmlDivElement>();
        mainContent.Id = "main-content";
        mainContent.AppendNodes(document.Body.ChildNodes.ToArray());

        document.Body.AppendNodes(loading, mainContent);
    }

    protected override IHtmlElement? TransformImage(IHtmlDocument document, IHtmlImageElement img, string sourceFile)
    {
        if (img.ClassList.Contains("tex"))
        {
            var span = document.CreateElement<IHtmlSpanElement>();
            span.ClassName = "tex";

            if (img.IsOnlyChild() && img.Parent!.ChildNodes.OfType<IText>().All(t => t.Text.IsBlank))
            {
                span.TextContent = $@"\[{img.AlternativeText}\]";
            }
            else
            {
                span.TextContent = $@"\({img.AlternativeText}\)";
            }

            return span;
        }
        else if (base.TransformImage(document, img, sourceFile) is IHtmlElement transformed)
        {
            transformed.SetAttribute("loading", "lazy");

            return transformed;
        }

        return null;
    }

    public IHtmlDivElement CreateNavDataDiv(IHtmlDocument document, in Nav nav, string sourceFile, string langList, string baseUrl)
    {
        var navDataDiv = document.CreateElement<IHtmlDivElement>();

        navDataDiv.Id = "doc-nav-data";
        navDataDiv.IsHidden = true;

        navDataDiv.SetAttribute("data-lang", Language);
        navDataDiv.SetAttribute("data-lang-list", langList);

        var files = nav.Files;

        if (!files.Parent.IsEmpty)
        {
            if (ResourceResolver.TryResolveHref("../" + files.Parent, sourceFile, out var url, out var _))
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
            navDataDiv.SetAttribute("data-book-index", baseUrl);
        }

        if (files.Siblings is not null)
        {
            var ul = CreateDataUL("doc-siblings-data", files.Siblings, nav.Titles);

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
                }

                if (isCurrent)
                {
                    li.ClassName = "disabled";
                }
                else
                {
                    a.SetAttribute("href", $"../{pathSpan}");
                }

                a.TextContent = titles.GetAlternateLookup<ReadOnlySpan<char>>()[pathSpan];

                li.AppendChild(a);
                ul.AppendChild(li);
            }

            return ul;
        }
    }
}