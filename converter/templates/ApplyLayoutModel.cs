namespace OriginLab.DocumentGeneration.Templates;

public class ApplyLayoutModel
{
    public required string LayoutPageUrl { get; init; }

    public required string PlaceHolderId { get; init; }

    public required string MainContentId { get; set; }
}
