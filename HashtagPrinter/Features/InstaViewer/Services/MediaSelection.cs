using Skarynagram.Features.InstaViewer.Entities;

namespace Skarynagram.Features.InstaViewer.Services;

public static class MediaSelection
{
    private static readonly HashSet<string> PrintableTypes = ["IMAGE", "CAROUSEL_ALBUM"];

    public static void AppendPrintable(
        ICollection<MediaElement> destination,
        ISet<string> knownIds,
        IEnumerable<MediaElement> candidates,
        int maximum)
    {
        foreach (var media in candidates)
        {
            if (destination.Count >= maximum)
                return;
            if (PrintableTypes.Contains(media.MediaType) && knownIds.Add(media.Id))
                destination.Add(media);
        }
    }
}
