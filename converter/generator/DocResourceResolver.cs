using System.Text.Json;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal abstract class DocResourceResolver
{
    private readonly DocTransformerArgs Args;
    protected string SourceFolder => Args.SourceFolder;
    protected string OutputFolder => Args.OutputFolder;
    protected string BooksXmlFolder => Args.BooksXmlFolder;

    protected Dictionary<string, string> SharedImages => field ??= GetSharedImages();

    protected Dictionary<string, string> MovedPages => field ??= GetMovedPages();

    public DocResourceResolver(DocTransformerArgs args)
    {
        Args = args;
    }

    protected abstract string GetSharedImageSrc(string path, string fileName);

    private Dictionary<string, string> GetSharedImages()
    {
        var images = new Dictionary<string, string>();

        foreach (var path in Directory.EnumerateFiles(Path.Combine(Template.WebRootPath, "books/images")))
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
}
