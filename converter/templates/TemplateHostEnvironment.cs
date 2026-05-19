using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace OriginLab.DocumentGeneration.Templates;

internal class TemplateHostEnvironment : IWebHostEnvironment
{
    public TemplateHostEnvironment()
    {
        ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name ?? "OriginLab.DocumentGeneration";
        ContentRootPath = Path.GetFullPath("../../../../converter/templates", AppContext.BaseDirectory);
        ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath);
        WebRootPath = Path.Combine(ContentRootPath, "wwwroot");
        WebRootFileProvider = new PhysicalFileProvider(WebRootPath);
        EnvironmentName = "";
    }

    public string ApplicationName { get; set; }
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
    public string WebRootPath { get; set; }
    public IFileProvider WebRootFileProvider { get; set; }
    public string EnvironmentName { get; set; } = "";
}