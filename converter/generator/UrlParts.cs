namespace OriginLab.DocumentGeneration;

internal readonly ref struct UrlParts
{
    public ReadOnlySpan<char> File { get; }
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

        File = sep > -1 ? span[..sep] : span;
    }
}
