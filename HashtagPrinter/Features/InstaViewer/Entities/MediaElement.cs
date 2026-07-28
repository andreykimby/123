using Skarynagram.Utils;
using System.Text.Json.Serialization;

namespace Skarynagram.Features.InstaViewer.Entities
{
    public record MediaElement(
        string Id,
        string Caption,
        string MediaType,
        string MediaUrl,
        string Permalink,
        int LikeCount,
        [property: JsonConverter(typeof(CustomDateTimeOffsetConverter))]
        DateTimeOffset Timestamp
        );

}
