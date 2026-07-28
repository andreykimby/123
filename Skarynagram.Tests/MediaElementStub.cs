namespace Skarynagram.Features.InstaViewer.Entities;

public record MediaElement(
    string Id,
    string Caption,
    string MediaType,
    string MediaUrl,
    string Permalink,
    int LikeCount,
    DateTimeOffset Timestamp);
