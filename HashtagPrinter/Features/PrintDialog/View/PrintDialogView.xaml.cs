using ExCSS;
using Skarynagram.Features.AdminPage.ViewModels;
using Skarynagram.Features.InstaViewer.Entities;
using Skarynagram.Features.AppSettingsService.Services;
using Skarynagram.Features.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Diagnostics;

namespace Skarynagram.Features.Views;

public struct RectInfo
{
    public float Scale { get; set; }
    public SKRect Rect { get; set; }

    public RectInfo(float scale, SKRect sKRect)
    {
        Scale = scale;
        Rect = sKRect;
    }
}

public partial class PrintDialogView : ContentPage
{
    private PrintDialogViewModel _vm;
    private ILogger<PrintDialogView> _logger;
    private const double SliderThreshold = 0.01;

    public PrintDialogView(PrintDialogViewModel printDialogViewModel, ILogger<PrintDialogView> logger)
    {
        InitializeComponent();

        _logger = logger;
        BindingContext = _vm = printDialogViewModel;
        _vm.RequestRedraw += canvasView.InvalidateSurface;
        _vm.RequestAlert += ShowAlertAsync;
        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_vm.FgBitmap))
            {
                _logger.LogDebug("FgBitmap set");
                canvasView.InvalidateSurface();
            }
            if (e.PropertyName == nameof(_vm.IsPrintingConfirmation) && _vm.IsPrintingConfirmation)
                _ = AnimatePrintStatusAsync();
        };
        
        canvasView.Touch += CanvasView_Touch;
    }

    private Task ShowAlertAsync(string title, string message) =>
        DisplayAlertAsync(title, message, "Добра");

    private async Task AnimatePrintStatusAsync()
    {
        while (_vm.IsPrintingConfirmation)
        {
            await Task.WhenAll(
                PressHandle.RotateTo(22, 380, Easing.CubicInOut),
                PressPlaten.TranslateTo(0, 12, 380, Easing.CubicInOut));
            await Task.Delay(180);
            await Task.WhenAll(
                PressHandle.RotateTo(-12, 430, Easing.CubicInOut),
                PressPlaten.TranslateTo(0, 0, 430, Easing.CubicInOut));
            await PressHandle.RotateTo(0, 180, Easing.CubicOut);
        }
    }

    private void CanvasView_Touch(object? sender, SKTouchEventArgs e)
    {
        
    }

    private const int DecimalPlaces = 2; // или 3
    private double Round(double value) => Math.Round(value, DecimalPlaces);

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (_vm.FgBitmap == null || _vm.BgBitmap == null)
        {
            e.Surface.Canvas.Clear(SKColors.LightGray);
            return;
        }

        // Always compose in paper pixels. The same image is used for preview and printing,
        // so the editor coordinates cannot be changed by the on-screen Canvas size.
        using var paperSurface = SKSurface.Create(new SKImageInfo(_vm.BgBitmap.Width, _vm.BgBitmap.Height));
        if (paperSurface == null) return;
        var paperCanvas = paperSurface.Canvas;
        LayoutRenderer.Draw(paperCanvas, _vm.BgBitmap.Width, _vm.BgBitmap.Height,
            _vm.BgBitmap, _vm.FgBitmap, _vm.XPos, _vm.YPos, _vm.PhotoScale, _vm.LayoutTextFields);
        paperCanvas.Flush();

        _vm.ResultImage?.Dispose();
        _vm.ResultImage = paperSurface.Snapshot();

        var previewCanvas = e.Surface.Canvas;
        previewCanvas.Clear(SKColors.LightGray);
        var scale = Math.Min((float)e.Info.Width / _vm.ResultImage.Width, (float)e.Info.Height / _vm.ResultImage.Height);
        var previewWidth = _vm.ResultImage.Width * scale;
        var previewHeight = _vm.ResultImage.Height * scale;
        var previewRect = new SKRect((e.Info.Width - previewWidth) / 2, (e.Info.Height - previewHeight) / 2,
            (e.Info.Width + previewWidth) / 2, (e.Info.Height + previewHeight) / 2);
        previewCanvas.DrawImage(_vm.ResultImage, previewRect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
    }

    private static SKRect GetPhotoRect(float width, float height, float x, float y, float size)
    {
        size = Math.Clamp(size, .1f, 1f);
        var side = Math.Min(width, height) * size;
        var left = (width - side) * Math.Clamp(x, 0f, 1f);
        var top = (height - side) * Math.Clamp(y, 0f, 1f);
        return new SKRect(left, top, left + side, top + side);
    }

    private static void DrawPhotoAspectFill(SKCanvas canvas, SKBitmap photo, SKRect target)
    {
        var scale = Math.Max(target.Width / photo.Width, target.Height / photo.Height);
        var width = photo.Width * scale;
        var height = photo.Height * scale;
        var destination = new SKRect(target.MidX - width / 2, target.MidY - height / 2,
            target.MidX + width / 2, target.MidY + height / 2);
        canvas.Save();
        canvas.ClipRect(target);
        canvas.DrawBitmap(photo, destination, new SKPaint { IsAntialias = true });
        canvas.Restore();
    }

    /// <summary>
    /// Рисует фон с учетом масштабирования и размеров CanvasView
    /// </summary>
    /// <param name="sKCanvasView">компонент SKCanvasView</param>
    /// <param name="sKCanvas">canvas компонента</param>
    /// <param name="sKBitmap">картинка фона</param>
    private static RectInfo DrawBg(SKCanvasView sKCanvasView, SKCanvas sKCanvas, SKBitmap sKBitmap, float normalizedX, float normalizedY, float normalizedSize)
    {
        float bgWidth = (float)sKBitmap.Width;
        float bgHeight = (float)sKBitmap.Height;
        float bgScale = 1;

        if (sKCanvasView.Height > 0)
        {
            if (sKBitmap.Height > sKCanvasView.Height && sKBitmap.Width < sKBitmap.Height)
            {
                bgScale = (float)(sKCanvasView.Height / sKBitmap.Height);
                bgWidth = (float)(bgWidth * bgScale);
                bgHeight = (float)(bgHeight * bgScale);
                sKCanvasView.WidthRequest = bgWidth;
            }

        }
        var fullRect = new SKRect(0, 0, bgWidth, bgHeight);



        using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High }) // Для лучшего качества масштабирования
        {
            sKCanvas.DrawBitmap(sKBitmap, fullRect, paint);

        }

        

        normalizedX = Math.Clamp(normalizedX, 0f, 1f);
        normalizedY = Math.Clamp(normalizedY, 0f, 1f);
        normalizedSize = Math.Clamp(normalizedSize, 0.1f, 1f);
        var squareSize = Math.Min(bgWidth, bgHeight) * normalizedSize;
        var left = (bgWidth - squareSize) * normalizedX;
        var top = (bgHeight - squareSize) * normalizedY;
        var fgRect = new SKRect(left, top, left + squareSize, top + squareSize);

        return new RectInfo(bgScale, fgRect);
    }

    private static void DrawFgRect(SKCanvas sKCanvas, SKRect fgRect, bool draw)
    {
        using (var paint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
        {
            if (draw)
                sKCanvas.DrawRect(fgRect.Left + paint.StrokeWidth, fgRect.Top + paint.StrokeWidth, fgRect.Width - paint.StrokeWidth*2, fgRect.Height- paint.StrokeWidth*2, paint);
                //sKCanvas.DrawRect(fgRect, paint);
        }
    }

    private static void DrawTextFields(SKCanvas canvas, IReadOnlyList<Features.AppSettingsService.Services.LayoutTextField> fields, float width, float height)
    {
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Text)) continue;
            using var typeface = SKTypeface.FromFamilyName(field.FontFamily) ?? SKTypeface.Default;
            using var font = new SKFont(typeface, (float)(field.FontSize * width / 600d));
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            var x = (float)(Math.Clamp(field.X, 0, 1) * width);
            // Stored Y is the top of the text box, while Skia draws from the baseline.
            var y = (float)(Math.Clamp(field.Y, 0, 1) * height) - font.Metrics.Ascent;
            canvas.DrawText(field.Text, x, y, SKTextAlign.Left, font, paint);
        }
    }


    private static void DrawFg(SKCanvasView sKCanvasView, SKCanvas sKCanvas, SKBitmap sKBitmap, RectInfo rectInfo)
    {
        float scale = 1;
        float left = rectInfo.Rect.Left;
        float top = rectInfo.Rect.Top;

        float newWidth = sKBitmap.Width;
        float newHeight = sKBitmap.Height;


        if (sKBitmap.Width > rectInfo.Rect.Right && sKBitmap.Height < sKBitmap.Width)
        {
            scale = (rectInfo.Rect.Right- rectInfo.Rect.Left) / sKBitmap.Width;
            newWidth = sKBitmap.Width * scale;
            newHeight = sKBitmap.Height * scale;
            top = (float)(rectInfo.Rect.Height  / 2) - (newHeight / 2) + rectInfo.Rect.Top;

        } else if (sKBitmap.Height > rectInfo.Rect.Height && sKBitmap.Height > sKBitmap.Width)
        {
            scale = (rectInfo.Rect.Bottom - rectInfo.Rect.Top) / sKBitmap.Height;
            newWidth = sKBitmap.Width * scale;
            newHeight = sKBitmap.Height * scale;
            left = (float)(rectInfo.Rect.Width / 2) - (newWidth / 2) + rectInfo.Rect.Left;
        } else 
        {
            scale = (rectInfo.Rect.Bottom - rectInfo.Rect.Top) / sKBitmap.Height;
            newWidth = sKBitmap.Width * scale;
            newHeight = sKBitmap.Height * scale;
            left = (float)(rectInfo.Rect.Width / 2) - (newWidth / 2) + rectInfo.Rect.Left;
        }


        var destRect = new SKRect(left, top, left + newWidth, top + newHeight);
        //Debug.WriteLine(destRect);
        sKCanvas.DrawBitmap(sKBitmap, destRect);
    }


    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    
    private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
        //double roundedNew = Round(e.NewValue);
        //double roundedOld = Round(e.OldValue);
        //ScaleSlider.Value = roundedNew;
        //if (roundedNew == roundedOld)
        //{
        //    return;

        //}
        //_logger.LogDebug($"Old: {e.OldValue}, New: {e.NewValue}" );
        canvasView.InvalidateSurface();
    }

    private void Button_Clicked(object sender, EventArgs e)
    {
       Navigation.PopModalAsync();
    }

    
}
