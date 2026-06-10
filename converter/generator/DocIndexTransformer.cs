using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

internal class DocIndexTransformer : DocToStaticPagesTransformer
{
    public DocIndexTransformer(DocToStaticPagesTransformerArgs args, IDocResourceResolver resourceResolver, ProblemRecorder problems) : base(args, resourceResolver, problems)
    {
    }

    protected override async Task TransformAsync(string language)
    {
        await base.TransformAsync(language);

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

        File.WriteAllText(Path.Combine(OutputFolder, language, "404.html"), await Template.Render404PageAsync(language));
    }
}
