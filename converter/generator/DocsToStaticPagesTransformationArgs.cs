namespace OriginLab.DocumentGeneration;

public class DocsToStaticPagesTransformationArgs : DocsTransformationArgs
{
    public required string BaseUrl { get; init; }

    public bool UseWebp { get; init; }
}
