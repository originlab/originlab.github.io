namespace OriginLab.DocumentGeneration;

public record class DocTransformerArgs(string SourceFolder, string OutputFolder, string BooksXmlFolder, string BookUrlName, bool UseWebp);