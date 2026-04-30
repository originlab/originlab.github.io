using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Razor.Templating.Core;

namespace OriginLab.DocumentGeneration.Templates;

public class Template
{
    private static IRazorTemplateEngine Engine => field ??= GetTemplateEngine();

    public static Task<string> RenderDocumentPageAsync(DocumentPageModel documentPageModel)
    {
        return Engine.RenderAsync("/DocumentPage.cshtml", documentPageModel);
    }

    public static Task<string> RenderApplyLayoutScriptsAsync(ApplyLayoutModel applyLayoutModel)
    {
        return Engine.RenderPartialAsync("/Partials/ApplyLayout.cshtml", applyLayoutModel);
    }

    public static Task<string> RenderEnglishFallbackBannerAsync(string language)
    {
        return Engine.RenderPartialAsync($"/Partials/{language}/EnFallbackBanner.cshtml");
    }

    public static async Task<string> Render404PageAsync(string? language = null)
    {
        if (String.IsNullOrEmpty(language))
        {
            return await Engine.RenderPartialAsync("/Partials/404.cshtml");
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
