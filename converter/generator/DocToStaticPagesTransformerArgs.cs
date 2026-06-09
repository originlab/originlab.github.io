namespace OriginLab.DocumentGeneration;

public class DocToStaticPagesTransformerArgs : DocTransformerArgs
{
    public required string BookUrlName { get; init; }

    public bool UseWebp { get; init; }
}
