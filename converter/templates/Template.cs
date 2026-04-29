using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Razor.Templating.Core;

namespace OriginLab.DocumentGeneration.Templates;

public class Template
{
    private static IRazorTemplateEngine TemplateEngine => field ??= GetTemplateEngine();

    public static Task<string> RenderDocumentPageAsync(DocumentPageModel documentPageModel)
    {
        return TemplateEngine.RenderAsync("/DocumentPage.cshtml", documentPageModel);
    }

    public static Task<string> RenderApplyLayoutScriptsAsync(ApplyLayoutModel applyLayoutModel)
    {
        return TemplateEngine.RenderPartialAsync("/Partials/ApplyLayout.cshtml", applyLayoutModel);
    }

    static IRazorTemplateEngine GetTemplateEngine()
    {
        var services = new ServiceCollection();
        var baseDirectory = AppContext.BaseDirectory;

        services.TryAddSingleton<IWebHostEnvironment>(new TemplateHostEnvironment
        {
            ContentRootFileProvider = new PhysicalFileProvider(baseDirectory),
        });
        services.AddRazorTemplating();

        return services.BuildServiceProvider().GetRequiredService<IRazorTemplateEngine>();
    }
}
