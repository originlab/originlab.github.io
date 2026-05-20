using Microsoft.Extensions.DependencyInjection;
using OriginLab.DocumentGeneration.Templates;

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
        var rootRepoPath = isBuildingIndex ? ".." : "../originlab.github.io";

        var booksXmlPath = Path.GetFullPath(Path.Combine(rootRepoPath, "books"), srcBookPath);
        if (!Directory.Exists(booksXmlPath))
        {
            throw new ArgumentException("Expect the books folder exists!", nameof(args));
        }

        var outputPath = Path.GetFullPath(isBuildingIndex ? "../../public_html" : "../public_html", srcBookPath);

        if (args.Length > 1 && args[1] == "merge")
        {
            CopyContents(Template.WebRootPath, outputPath);
            CopyContents(booksXmlPath, Path.Combine(outputPath, "books"));

            if (!isBuildingIndex)
            {
                outputPath = Path.Combine(outputPath, bookUrlName);
            }
        }

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        var services = new ServiceCollection();

        if (isBuildingIndex)
        {
            services.AddTransient<DocTransformerArgs>(sp => new(srcBookPath, outputPath, booksXmlPath, ""));
            services.AddTransient<IDocTransformer, DocIndexTransformer>();
        }
        else
        {
            services.AddTransient<DocTransformerArgs>(sp => new(srcBookPath, outputPath, booksXmlPath, Path.GetFileName(srcBookPath).ToLowerInvariant()));
            services.AddTransient<IDocTransformer, DocBookTransformer>();
        }

        var serviceProvider = services.BuildServiceProvider();
        var transformer = serviceProvider.GetRequiredService<IDocTransformer>();

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
            File.Copy(srcFile, Path.Combine(dstDir, relativePath), overwrite: true);
        }
    }
}