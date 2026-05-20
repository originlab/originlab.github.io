namespace OriginLab.DocumentGeneration;

internal class DocIndexTransformer : DocTransformer
{
    public DocIndexTransformer(string booksXmlFolder, string sourceFolder, string outputFolder)
        : base(sourceFolder, outputFolder, booksXmlFolder, "")
    {
    }

    public override async Task TransformFilesAsync(string language)
    {
        foreach (var sourceFile in Directory.EnumerateFiles(SourceFolder, "index.html", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(SourceFolder, sourceFile);

            if (Path.GetFileName(Path.GetDirectoryName(relativePath)) != Language)
            {
                continue;
            }

            var destinationFile = language != "en" ? Path.Combine(OutputFolder, relativePath)
                                                   : Path.GetFullPath(Path.Combine(OutputFolder, Path.GetDirectoryName(relativePath)!, "..", Path.GetFileName(relativePath)))
                                                   ;
            var destinationDir = Path.GetDirectoryName(destinationFile)!;
            Directory.CreateDirectory(destinationDir);

            Transform(sourceFile, destinationFile);
        }
    }
}
