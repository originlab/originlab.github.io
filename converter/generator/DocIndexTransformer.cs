namespace OriginLab.DocumentGeneration;

internal class DocIndexTransformer : DocTransformer
{
    public DocIndexTransformer(string booksXmlFolder, string sourceFolder, string outputFolder)
        : base(sourceFolder, outputFolder, booksXmlFolder, "")
    {
    }

    public override async Task TransformFilesAsync()
    {
        foreach (var sourceFile in Directory.EnumerateFiles(SourceFolder, "index.html", SearchOption.AllDirectories))
        {
            var path = Path.GetRelativePath(SourceFolder, sourceFile);
            var language = Path.GetFileName(Path.GetDirectoryName(path))!;

            var destinationFile = language != "en" ? Path.Combine(OutputFolder, path)
                                                   : Path.GetFullPath(Path.Combine(OutputFolder, Path.GetDirectoryName(path)!, "..", Path.GetFileName(path)))
                                                   ;
            var destinationDir = Path.GetDirectoryName(destinationFile)!;
            Directory.CreateDirectory(destinationDir);

            Transform(sourceFile, destinationFile, default, language);
        }
    }
}
