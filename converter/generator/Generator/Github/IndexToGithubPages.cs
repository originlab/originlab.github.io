using OriginLab.DocumentGeneration.Templates;
using OriginLab.DocumentGeneration.Transformers;

namespace OriginLab.DocumentGeneration.Generator.Github;

internal sealed class IndexToGithubPages : DocsToGithubPages
{
    public IndexToGithubPages(DocsToStaticPagesTransformationArgs args, DocumentToGithubPage transformer, ProblemRecorder problems)
        : base("", args.SourceFolder, args.OutputFolder, transformer, problems)
    {
    }

    protected override async Task TransformFilesAsync(string language)
    {
        await base.TransformFilesAsync(language);

        foreach (var sourceFile in Directory.EnumerateFiles(SourceFolder, "index.html", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(SourceFolder, sourceFile);

            if (Path.GetFileName(Path.GetDirectoryName(relativePath)) != language)
            {
                continue;
            }

            var destinationFile = language != "en" ? Path.Combine(OutputFolder, relativePath)
                                                   : Path.GetFullPath(Path.Combine(OutputFolder, Path.GetDirectoryName(relativePath)!, "..", Path.GetFileName(relativePath)))
                                                   ;

            Transform(sourceFile, destinationFile);
        }

        File.WriteAllText(Path.Combine(OutputFolder, language, "404.html"), await Template.Render404PageAsync(language));
    }
}
