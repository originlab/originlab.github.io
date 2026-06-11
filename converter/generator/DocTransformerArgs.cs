namespace OriginLab.DocumentGeneration;

public class DocTransformerArgs
{
    public required string SourceFolder { get; init; }

    public required string OutputFolder { get; init; }
    
    public required string BooksXmlFolder { get; init; }

    public required string SharedImagesFolder { get; init; }
}