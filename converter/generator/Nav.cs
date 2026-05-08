namespace OriginLab.DocumentGeneration;

readonly struct Nav
{
    public Nav(string? parent, string[]? siblings, string[]? children)
    {
        Parent = parent;
        Siblings = siblings;
        Children = children;
    }

    public string? Parent { get; }
    public string[]? Siblings { get; }
    public string[]? Children { get; }
}
