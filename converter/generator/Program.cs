using Microsoft.Extensions.DependencyInjection;
using OriginLab.DocumentGeneration.Templates;
using OriginLab.DocumentGeneration.Transformers.Github;
using OriginLab.DocumentGeneration.Generator.Github;
using OriginLab.DocumentGeneration.Generator;

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
            if (isBuildingIndex)
            {
                CopyContents(Template.WebRootPath, outputPath);
            }
            else
            {
                outputPath = Path.Combine(outputPath, bookUrlName);
            }
        }

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        var services = new ServiceCollection();

        services.AddTransient<IDocResourceResolver, DocsResourceGithubPagesResolver>();
        services.AddTransient<DocumentToGithubPage>();

        services.AddSingleton(sp => new DocsToStaticPagesTransformationArgs()
        {
            SourceFolder = srcBookPath,
            OutputFolder = outputPath,
            BooksXmlFolder = booksXmlPath,
            SharedImagesFolder = Path.Combine(Template.WebRootPath, "books/images"),
            BaseUrl = isBuildingIndex ? "" : Path.GetFileName(srcBookPath),
            UseWebp = args.Webp
        });

        services.AddSingleton<DocsTransformationArgs>(sp => sp.GetRequiredService<DocsToStaticPagesTransformationArgs>());
        services.AddSingleton<IOutputOperations, SystemOutputOperations>();
        services.AddSingleton<ProblemRecorder>();

        if (isBuildingIndex)
        {
            services.AddTransient<IDocsGenerator, IndexToGithubPages>();
        }
        else
        {
            services.AddTransient<IDocsGenerator, BookToGithubPages>();
        }

        var serviceProvider = services.BuildServiceProvider();
        var driver = serviceProvider.GetRequiredService<IDocsGenerator>();

        await driver.RunAsync();

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