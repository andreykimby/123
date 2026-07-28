using SkiaSharp;

namespace Skarynagram.Features.AppSettingsService.Services;

public static class LayoutRenderer
{
    public static void Draw(SKCanvas canvas, float width, float height, SKBitmap? background,
        SKBitmap? photo, double photoX, double photoY, double photoScale,
        IReadOnlyList<LayoutTextField> texts, bool showPhotoPlaceholder = false)
    {
        canvas.Clear(SKColors.White);
        if (background != null)
        {
            var backgroundScale = Math.Min(width / background.Width, height / background.Height);
            var backgroundWidth = background.Width * backgroundScale;
            var backgroundHeight = background.Height * backgroundScale;
            var destination = new SKRect((width - backgroundWidth) / 2, (height - backgroundHeight) / 2,
                (width + backgroundWidth) / 2, (height + backgroundHeight) / 2);
            using var backgroundPaint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
            canvas.DrawBitmap(background, destination, backgroundPaint);
        }

        var photoRect = GetPhotoRect(width, height, photoX, photoY, photoScale);
        if (photo != null)
            DrawPhotoAspectFill(canvas, photo, photoRect);
        else if (showPhotoPlaceholder)
        {
            using var fill = new SKPaint { Color = new SKColor(63, 124, 255, 70), IsAntialias = true };
            using var stroke = new SKPaint { Color = new SKColor(255, 49, 88), Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(2, width / 250), IsAntialias = true };
            canvas.DrawRect(photoRect, fill);
            canvas.DrawRect(photoRect, stroke);
        }

        DrawTexts(canvas, texts, width, height);
    }

    public static SKRect GetPhotoRect(float width, float height, double x, double y, double size)
    {
        var rect = LayoutGeometry.GetPhotoRect(width, height, x, y, size);
        return new SKRect(rect.Left, rect.Top, rect.Left + rect.Width, rect.Top + rect.Height);
    }

    private static void DrawPhotoAspectFill(SKCanvas canvas, SKBitmap photo, SKRect target)
    {
        var scale = Math.Max(target.Width / photo.Width, target.Height / photo.Height);
        var drawWidth = photo.Width * scale;
        var drawHeight = photo.Height * scale;
        var destination = new SKRect(target.MidX - drawWidth / 2, target.MidY - drawHeight / 2,
            target.MidX + drawWidth / 2, target.MidY + drawHeight / 2);
        canvas.Save();
        canvas.ClipRect(target);
        canvas.DrawBitmap(photo, destination, new SKPaint { IsAntialias = true });
        canvas.Restore();
    }

    private static void DrawTexts(SKCanvas canvas, IReadOnlyList<LayoutTextField> fields, float width, float height)
    {
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Text)) continue;
            using var typeface = SKTypeface.FromFamilyName(field.FontFamily) ?? SKTypeface.Default;
            using var font = new SKFont(typeface, (float)(field.FontSize * width / 600d));
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            var x = (float)(Math.Clamp(field.X, 0, 1) * width);
            var y = (float)(Math.Clamp(field.Y, 0, 1) * height) - font.Metrics.Ascent;
            canvas.DrawText(field.Text, x, y, SKTextAlign.Left, font, paint);
        }
    }
}
