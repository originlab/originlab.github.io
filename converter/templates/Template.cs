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

    public static async Task<string> Render404PageAsync(string? language = null)
    {
        if (String.IsNullOrEmpty(language))
        {
            return await TemplateEngine.RenderPartialAsync("/Partials/404.cshtml");
        }

        throw new NotImplementedException();
    }

    static IRazorTemplateEngine GetTemplateEngine()
    {
        var services = new ServiceCollection();

        services.TryAddSingleton<IWebHostEnvironment, TemplateHostEnvironment>();
        services.AddRazorTemplating();

        return services.BuildServiceProvider().GetRequiredService<IRazorTemplateEngine>();
    }
}
