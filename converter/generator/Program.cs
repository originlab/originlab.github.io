namespace OriginLab.DocumentGeneration;

class Program
{
    async static Task Main(string[] args)
    {
        var srcBookPath = Path.GetFullPath(args[0]);
        if (!Directory.Exists(srcBookPath))
        {
            throw new ArgumentException("Expect book folder exists!", nameof(args));
        }

        var bookUrlName = Path.GetFileName(srcBookPath);
        var isBuildingIndex = bookUrlName == "index";

        var booksXmlPath = Path.GetFullPath(isBuildingIndex ? "../wwwroot/books" : "../originlab.github.io/wwwroot/books", srcBookPath);
        if (!Directory.Exists(booksXmlPath))
        {
            throw new ArgumentException("Expect the books folder exists!", nameof(args));
        }

        var outputPath = Path.GetFullPath(isBuildingIndex ? "../out" : "out", srcBookPath);

        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is null)
        {
            CopyContents(Path.GetFullPath(isBuildingIndex ? "../wwwroot" : "../originlab.github.io/wwwroot", srcBookPath), outputPath);

            if (!isBuildingIndex)
            {
                outputPath = Path.Combine(outputPath, bookUrlName);
            }
        }

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        Transformer transformer = isBuildingIndex
            ? new IndexTransformer(booksXmlPath, srcBookPath, outputPath)
            : new BookTransformer(booksXmlPath, srcBookPath, outputPath)
            ;
        await transformer.TransformAsync();

        transformer.PrintProblems();
    }

    private static void CopyContents(string srcDir, string dstDir)
    {
        foreach (var srcFile in Directory.EnumerateFiles(srcDir, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(srcDir, srcFile);
            var relativeDir = Path.GetDirectoryName(relativePath)!;

            Directory.CreateDirectory(Path.Combine(dstDir, relativeDir));
            File.Copy(srcFile, Path.Combine(dstDir, relativePath));
        }
    }
}