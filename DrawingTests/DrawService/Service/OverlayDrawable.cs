using SkiaSharp;
using SkiaSharp.Views.Maui;
using System.IO;
using System.Reflection;

namespace DrawingTests.DrawService.Service
{
    public class OverlayDrawable : IDrawable
    {
        public SKImage? BackgroundImage { get; set; }
        public SKImage? OverlayImage { get; set; }
        public SKPoint OverlayPosition { get; set; } = new SKPoint(50, 50); // Позиция наложения
        public float OverlayOpacity { get; set; } = 1.0f; // Прозрачность (0–1)

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var skCanvas = canvas as SKCanvas; // Приведение для SkiaSharp
            if (skCanvas == null)
            {
                return;
            }
            if (BackgroundImage != null)
            {
                
                skCanvas.DrawImage(BackgroundImage, new SKRect(0, 0, (float)dirtyRect.Width, (float)dirtyRect.Height));
            }

            if (OverlayImage != null)
            {
                var alpha = (byte)(255 * OverlayOpacity); // Вычисление альфы (0–255)
                using var paint = new SKPaint { Color = new SKColor(255, 255, 255, alpha) };
                skCanvas.DrawImage(OverlayImage, OverlayPosition, paint);
            }
        }

        // Метод для загрузки изображений (пример из ресурсов)
        public async Task LoadImagesAsync(string backgroundResource, string overlayResource)
        {
            BackgroundImage = await LoadImageFromResource(backgroundResource);
            OverlayImage = await LoadImageFromResource(overlayResource);
        }

        private async Task<SKImage?> LoadImageFromResource(string resourceId)
        {
            var assembly = typeof(OverlayDrawable).GetTypeInfo().Assembly;
            // using var stream = assembly.GetManifestResourceStream(resourceId);
            using (var stream = assembly.GetManifestResourceStream(resourceId))
            {
                if (stream != null)
                    return SKImage.FromEncodedData(await ReadStreamAsync(stream));
            }
            return null;
        }

        private static async Task<byte[]> ReadStreamAsync(Stream stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
    }
}
