namespace OriginLab.DocumentGeneration;

readonly struct NavFiles
{
    public NavFiles(string? parent, string[]? siblings, string[]? children)
    {
        Parent = parent;
        Siblings = siblings;
        Children = children;
    }

    public string? Parent { get; }
    public string[]? Siblings { get; }
    public string[]? Children { get; }
}
