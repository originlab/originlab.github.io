namespace OriginLab.DocumentGeneration.Templates;

public class ApplyLayoutModel
{
    public required string LayoutPageUrl { get; init; }

    public required string ContainerId { get; init; }

    public required string MainContentId { get; set; }
}
