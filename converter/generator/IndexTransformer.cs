namespace OriginLab.DocumentGeneration;

internal class IndexTransformer : Transformer
{
    public IndexTransformer(string booksXmlFolder, string sourceFolder, string outputFolder)
        : base(booksXmlFolder, sourceFolder, outputFolder)
    {
    }

    protected override string GetBookUrlName() => "";

    public override async Task TransformFilesAsync()
    {
        var layoutScripts = new Dictionary<string, string>();

        foreach (var language in AvailableLanguages)
        {
            layoutScripts.Add(language, await GenerateLayoutAsync(language));
        }

        foreach (var sourceFile in Directory.EnumerateFiles(SourceFolder, "index.html", SearchOption.AllDirectories))
        {
            var path = Path.GetRelativePath(SourceFolder, sourceFile);
            var language = Path.GetFileName(Path.GetDirectoryName(path))!;

            var destinationFile = language != "en" ? Path.Combine(OutputFolder, path)
                                                   : Path.GetFullPath(Path.Combine(OutputFolder, Path.GetDirectoryName(path)!, "..", Path.GetFileName(path)))
                                                   ;
            var destinationDir = Path.GetDirectoryName(destinationFile)!;
            Directory.CreateDirectory(destinationDir);

            Transform(sourceFile, destinationFile, language, layoutScripts[language]);
        }
    }
}
