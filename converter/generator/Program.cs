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

        var isBuildingIndex = Path.GetFileName(srcBookPath) == "index";

        var booksXmlPath = Path.GetFullPath(isBuildingIndex ? "../wwwroot/books" : "../originlab.github.io/wwwroot/books", srcBookPath);
        if (!Directory.Exists(booksXmlPath))
        {
            throw new ArgumentException("Expect the books folder exists!", nameof(args));
        }

        var outputPath = Path.GetFullPath(isBuildingIndex ? "../out" : "out", srcBookPath);
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

        File.WriteAllText(Path.Combine(outputPath, "404.html"), """
            <script src="/static/gen_utils.js"></script>
            <script>tryRedirectToLower()</script>
            """);
    }
}