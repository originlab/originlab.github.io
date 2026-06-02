using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Razor.Templating.Core;

namespace OriginLab.DocumentGeneration.Templates;

public class Template
{
    private static IServiceProvider ServiceProvider => field ??= BuildServiceProvider();

    private static IRazorTemplateEngine Engine => field ??= ServiceProvider.GetRequiredService<IRazorTemplateEngine>();

    public static string WebRootPath => field ??= ServiceProvider.GetRequiredService<IWebHostEnvironment>().WebRootPath;

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

    public static Task<string> Render404PageAsync(string? language = null)
    {
        return Engine.RenderAsync('/'.TryPrefixEach("Partials", language, "404.cshtml"));
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.TryAddSingleton<IWebHostEnvironment, TemplateHostEnvironment>();
        services.AddRazorTemplating();

        return services.BuildServiceProvider();
    }
}
