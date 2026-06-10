namespace OriginLab.DocumentGeneration;

public interface IDocResourceResolver
{
    public string Language { get; set; }

    bool TryResolveHref(string href, string sourceDir, out string result, out string? titleEn);

    bool TryResolveSrc(string src, string sourceDir, out string result, out (string src, string dst)? copy);
}
