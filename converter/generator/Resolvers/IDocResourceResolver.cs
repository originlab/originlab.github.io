namespace OriginLab.DocumentGeneration.Resolvers;

public interface IDocResourceResolver
{
    string[] AvailableLanguages { get; }

    public string Language { get; set; }
    bool TryResolveHref(string href, string sourceFile, out string result, out string? titleEn);

    bool TryResolveSrc(string src, string sourceFile, out string result, out (string src, string dst)? copy);
}
