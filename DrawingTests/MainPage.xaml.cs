

using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Diagnostics;

namespace DrawingTests
{
    public partial class MainPage : ContentPage
    {
        int count = 0;
        private MainPageViewModel _vm;
        public MainPage(MainPageViewModel vm)
        {
            InitializeComponent();
            BindingContext = _vm = vm;
            
        }

        private void OnCounterClicked(object? sender, EventArgs e)
        {
            
            canvasView.InvalidateSurface();
            Debug.WriteLine("OnCounterClicked");
        }

        private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            //Debug.WriteLine("OnPaintSurface called");
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.White);
            canvas.Scale(_vm.CanvasScale);
            // Загружаем первую картинку (фон)
            //using var background = SKBitmap.Decode(@"C:\Users\lukav\source\repos\HashtagPrinter\DrawingTests\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\background.scale-100.jpg");

            // Загружаем вторую картинку (накладываем сверху)
            //using var overlay = SKBitmap.Decode(@"C:\\Users\\lukav\\source\\repos\\HashtagPrinter\\DrawingTests\\bin\\Debug\\net9.0-windows10.0.19041.0\\win10-x64\\dotnet_bot.scale-100.png");

            // Рисуем фон
            canvas.DrawBitmap(_vm.BgBitmap, new SKPoint(0, 0));

            // Рисуем наложение (например, в центре)
            var x = _vm.XPos;
            var y = _vm.YPos;


            var newWidth = _vm.FgBitmap.Width * _vm.PhotoScale;
            var newHeight = _vm.FgBitmap.Height * _vm.PhotoScale;
            
            var destRect = new SKRect(x, y, x + newWidth, y + newHeight);

            //canvas.DrawBitmap(_vm.FgBitmap, new SKPoint(x, y));
            canvas.DrawBitmap(_vm.FgBitmap, destRect);

            if (canvas.Surface != null)
                _vm.ResultImage = canvas.Surface.Snapshot();
        }

        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            canvasView.InvalidateSurface();
        }
    }
}
