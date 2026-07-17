using System.Text.Json;

namespace OriginLab.DocumentGeneration.Resolvers;

internal abstract class DocsResourceResolver : IDocResourceResolver
{
    private readonly DocsTransformationArgs Args;
    protected string SourceFolder => Args.SourceFolder;
    protected string OutputFolder => Args.OutputFolder;
    protected string BooksXmlFolder => Args.BooksXmlFolder;
    protected string SharedImagesFolder => Args.SharedImagesFolder;

    protected Dictionary<string, (string url, ulong hash)> SharedImages => field ??= GetSharedImages();

    protected Dictionary<string, string> MovedPages => field ??= GetMovedPages();

    public string[] AvailableLanguages { get; }

    public abstract string Language { get; set; }

    public DocsResourceResolver(DocsTransformationArgs args)
    {
        var languages = (from subPath in Directory.EnumerateDirectories(args.SourceFolder)
                         let name = Path.GetFileName(subPath)
                         where name.Length == 2
                         select name).ToArray();

        var enIndex = languages.IndexOf("en");
        if (enIndex < 0)
        {
            throw new ArgumentException("Expect en folder exists within SourceFolder", nameof(args));
        }
        else if (enIndex > 0)
        {
            languages[enIndex] = languages[0];
            languages[0] = "en";
        }

        AvailableLanguages = languages;
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

    public abstract bool TryResolveHref(string href, string sourceFile, out string result, out string? titleEn);

    public abstract bool TryResolveSrc(string src, string sourceFile, out string result, out (string src, string dst)? copy);
}
