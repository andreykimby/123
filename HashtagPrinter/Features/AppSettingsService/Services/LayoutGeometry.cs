namespace Skarynagram.Features.AppSettingsService.Services;

public readonly record struct LayoutRectangle(float Left, float Top, float Width, float Height);

public static class LayoutGeometry
{
    public static LayoutRectangle GetPhotoRect(float width, float height, double x, double y, double size)
    {
        var normalizedSize = Math.Clamp((float)size, .1f, 1f);
        var side = Math.Min(width, height) * normalizedSize;
        return new LayoutRectangle(
            (width - side) * Math.Clamp((float)x, 0f, 1f),
            (height - side) * Math.Clamp((float)y, 0f, 1f),
            side,
            side);
    }
}
