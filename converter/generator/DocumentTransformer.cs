using System.Diagnostics;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Text;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal class DocumentTransformer
{
    public string Language
    {
        get => ResourceResolver.Language;
        private set => ResourceResolver.Language = value;
    }

    protected IDocResourceResolver ResourceResolver { get; }

    protected IOutputOperations Output { get; }

    private readonly ProblemRecorder Problems;

    public DocumentTransformer(IDocResourceResolver resourceResolver, IOutputOperations output, ProblemRecorder problems)
    {
        ResourceResolver = resourceResolver;
        Output = output;
        Problems = problems;
    }

    public virtual async Task<string> InitializeLayoutAsync(DocumentPageModel model)
    {
        Language = model.Language;

        return "";
    }

    public static void CleanUp(IHtmlDocument document)
    {
        document.Prepend(document.Implementation.CreateDocumentType("html", "", ""));
        document.QuerySelectorAll("span.mw-editsection, p.urlname, p.hierarchy").Remove();
        document.Title = document.QuerySelector("h1")?.Text() ?? "";
    }

    public virtual void Transform(IHtmlDocument document, string sourceFile)
    {
        var sourceDir = Path.GetDirectoryName(sourceFile);

        Debug.Assert(!sourceDir.IsEmpty);

        foreach (var a in document.Descendants<IHtmlAnchorElement>().ToList())
        {
            if (TransformAnchor(document, a, sourceFile, sourceDir) is IHtmlElement transformed)
            {
                if (transformed != a)
                {
                    a.Replace(transformed);
                }
                else
                {
                    a.Remove();
                }
            }
        }

        foreach (var img in document.Descendants<IHtmlImageElement>().ToList())
        {
            if (TransformImage(document, img, sourceFile, sourceDir) is IHtmlElement transformed)
            {
                if (transformed != img)
                {
                    img.Replace(transformed);
                }
                else
                {
                    img.Remove();
                }
            }
        }
    }

    protected virtual IHtmlElement? TransformAnchor(IHtmlDocument document, IHtmlAnchorElement a, string sourceFile, string sourceDir)
    {
        if (a.GetAttribute("href") is not string href || href.IsBlank)
        {
            return null;
        }

        if (ResourceResolver.TryResolveHref(href, sourceDir, out var result, out var title))
        {
            a.SetAttribute("href", result);

            if ((a.Title.IsBlank || a.Title.Contains(':')) && !title.IsEmpty)
            {
                a.Title = title;
            }

            return a;
        }
        else
        {
            var parts = new UrlParts(href);
            ReportProblem(sourceFile, result, parts.Path.ToString(), a.SourceReference?.Position);
        }

        return null;
    }

    protected virtual IHtmlElement? TransformImage(IHtmlDocument document, IHtmlImageElement img, string sourceFile, string sourceDir)
    {
        if (img.GetAttribute("src") is not string src || src.IsBlank)
        {
            return null;
        }

        if (ResourceResolver.TryResolveSrc(src, sourceDir, out var result, out var copy))
        {
            img.SetAttribute("src", result);

            if (copy is (string srcImg, string dstImg))
            {
                Output.CreateDirectory(Path.GetDirectoryName(dstImg)!);
                Output.CopyFile(srcImg, dstImg, overwrite: true);
            }

            return img;
        }
        else
        {
            var parts = new UrlParts(src);
            ReportProblem(sourceFile, result, parts.Path.ToString(), img.SourceReference?.Position);
        }

        return null;
    }

    protected void ReportProblem(string sourcePath, string category, string? details = null, TextPosition? position = null)
        => Problems.Record(sourcePath, category, details, position);
}
