namespace OriginLab.DocumentGeneration.Resolvers;

public interface IDocResourceResolver
{
    string[] AvailableLanguages { get; }

    public string Language { get; set; }

    bool TryResolveHref(string href, string sourceFile, out string uri);

    bool TryResolveSrc(string src, string sourceFile, out string uri, out (string src, string dst)? copy);

    string? GetTitle(string uri);
}
