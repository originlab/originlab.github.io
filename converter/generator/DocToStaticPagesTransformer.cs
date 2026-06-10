using System.Buffers;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal abstract partial class DocToStaticPagesTransformer : DocTransformer
{
    public const int MaxSiblingNodes = 10 * 2;

    protected string BookUrlName { get; }

    private INode[] LayoutNodes = null!;

    protected DocToStaticPagesTransformer(DocToStaticPagesTransformerArgs args, IDocResourceResolver resourceResolver, ProblemRecorder problems) : base(args, resourceResolver, problems)
    {
        BookUrlName = args.BookUrlName;
    }

    public override async Task TransformAsync()
    {
        await base.TransformAsync();

        File.WriteAllText(Path.Combine(OutputFolder, "404.html"), await Template.Render404PageAsync());
    }

    protected override async Task TransformAsync(string language)
    {
        var html = await InitializeLanguageLayoutAsync(language);
        var dir = Path.Combine(OutputFolder, language);

        Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(Path.Combine(dir, "layout.html"), html);
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

    protected override IHtmlElement? TransformImage(IHtmlDocument document, IHtmlImageElement img, string sourceFile, string sourceDir)
    {
        if (base.TransformImage(document, img, sourceFile, sourceDir) is IHtmlElement transformed)
        {
            transformed.SetAttribute("loading", "lazy");

            return transformed;
        }

        return null;
    }
}