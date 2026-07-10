using System.Text.Json;

namespace OriginLab.DocumentGeneration;

internal abstract class DocResourceResolver
{
    private readonly DocsTransformationArgs Args;
    protected string SourceFolder => Args.SourceFolder;
    protected string OutputFolder => Args.OutputFolder;
    protected string BooksXmlFolder => Args.BooksXmlFolder;
    protected string SharedImagesFolder => Args.SharedImagesFolder;

    protected Dictionary<string, (string url, ulong hash)> SharedImages => field ??= GetSharedImages();

    protected Dictionary<string, string> MovedPages => field ??= GetMovedPages();

    public DocResourceResolver(DocsTransformationArgs args)
    {
        Args = args;
    }

    protected abstract (string url, ulong hash) GetSharedImageSrc(string path, string fileName);

    private Dictionary<string, (string url, ulong hash)> GetSharedImages()
    {
        var images = new Dictionary<string, (string url, ulong hash)>();

        foreach (var path in Directory.EnumerateFiles(SharedImagesFolder))
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
