namespace OriginLab.DocumentGeneration.Templates;

public class WebPageModel
{
    public string? RootUrlPrefix { get; set; }

    public required string Language { get; set; }

    public string[] AvailableLanguages { get; set; } = ["en", "de", "ja"];
}
