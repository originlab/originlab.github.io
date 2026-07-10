namespace OriginLab.DocumentGeneration;

public class DocsTransformationArgs
{
    public required string SourceFolder { get; init; }

    public required string OutputFolder { get; init; }
    
    public required string BooksXmlFolder { get; init; }

    public required string SharedImagesFolder { get; init; }
}