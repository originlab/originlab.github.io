namespace OriginLab.DocumentGeneration;

internal readonly ref struct UrlParts
{
    public bool IsAbosolute { get; }
    public ReadOnlySpan<char> Path { get; }
    public ReadOnlySpan<char> Query { get; }
    public ReadOnlySpan<char> Hash { get; }

    public UrlParts(string url)
    {
        var span = url.AsSpan();
        int sep;

        sep = span.LastIndexOf('#');
        if (sep > -1)
        {
            Hash = span[sep..];
            span = span[..sep];
        }

        sep = span.LastIndexOf('?');
        if (sep > -1)
        {
            Query = span[sep..];
            span = span[..sep];
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            Path = uri.AbsolutePath;
            IsAbosolute = true;
        }
        else
        {
            Path = sep > -1 ? span[..sep] : span;
        }
    }
}
