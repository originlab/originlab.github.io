using Microsoft.Extensions.DependencyInjection;
using OriginLab.DocumentGeneration.Templates;

namespace OriginLab.DocumentGeneration;

class Program
{
    async static Task Main(string[] cmdArgs)
    {
        if (CommandLine.Parser.Default.ParseArguments<ProgramArgs>(cmdArgs).Value is not ProgramArgs args)
        {
            return;
        }

        var srcBookPath = Path.GetFullPath(args.SourceBookPath);
        if (!Directory.Exists(srcBookPath))
        {
            throw new ArgumentException("Expect book folder exists!", nameof(cmdArgs));
        }

        var bookUrlName = Path.GetFileName(srcBookPath);
        var isBuildingIndex = bookUrlName == "index";

        var booksXmlPath = Path.GetFullPath("../../../books", Template.WebRootPath);
        if (!Directory.Exists(booksXmlPath))
        {
            throw new ArgumentException("Expect the books folder exists!", nameof(cmdArgs));
        }

        var outputPath = Path.GetFullPath("../../../artifacts/public_html", Template.WebRootPath);

        if (args.Merge)
        {
            CopyContents(Template.WebRootPath, outputPath);

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
            services.AddSingleton<DocTransformerArgs>(sp => new(srcBookPath, outputPath, booksXmlPath, "", args.Webp));
            services.AddTransient<IDocTransformer, DocIndexTransformer>();
        }
        else
        {
            services.AddSingleton<DocTransformerArgs>(sp => new(srcBookPath, outputPath, booksXmlPath, Path.GetFileName(srcBookPath), args.Webp));
            services.AddTransient<IDocTransformer, DocBookTransformer>();
        }

        services.AddSingleton<ProblemRecorder>();

        var serviceProvider = services.BuildServiceProvider();
        var transformer = serviceProvider.GetRequiredService<IDocTransformer>();

        await transformer.TransformAsync();

        var problems = serviceProvider.GetRequiredService<ProblemRecorder>();
        ProblemSummarizer summarizer = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is not null
                                     ? new ProblemSummarizer.GithubActions()
                                     : new ProblemSummarizer.Local();

        summarizer.WriteSummary(problems, Console.Error);
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