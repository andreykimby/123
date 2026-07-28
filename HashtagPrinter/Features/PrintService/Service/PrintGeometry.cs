using System.Drawing;

namespace Skarynagram.Features.PrintService.Service;

public static class PrintGeometry
{
    public static Rectangle FitInsidePaper(
        int paperWidth,
        int paperHeight,
        int marginLeft,
        int marginRight,
        int marginTop,
        int marginBottom,
        int imageWidth,
        int imageHeight)
    {
        var target = new Rectangle(
            marginLeft,
            marginTop,
            Math.Max(1, paperWidth - marginLeft - marginRight),
            Math.Max(1, paperHeight - marginTop - marginBottom));
        var ratio = Math.Min(target.Width / (float)imageWidth, target.Height / (float)imageHeight);
        var width = Math.Max(1, (int)(imageWidth * ratio));
        var height = Math.Max(1, (int)(imageHeight * ratio));
        return new Rectangle(
            target.X + (target.Width - width) / 2,
            target.Y + (target.Height - height) / 2,
            width,
            height);
    }
}
