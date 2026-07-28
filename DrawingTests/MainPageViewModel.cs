using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if WINDOWS
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif
namespace DrawingTests
{
    public partial class MainPageViewModel: ObservableObject
    {
        [ObservableProperty]
        private string _backgroundImagePath = @"C:\Users\lukav\source\repos\HashtagPrinter\DrawingTests\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\background.scale-100.jpg";
        [ObservableProperty]
        private string _foregroundImagePath = @"C:\\Users\\lukav\\source\\repos\\HashtagPrinter\\DrawingTests\\bin\\Debug\\net9.0-windows10.0.19041.0\\win10-x64\\dotnet_bot.scale-100.png";
        [ObservableProperty]
        private SKBitmap _bgBitmap;
        [ObservableProperty]
        private SKBitmap _fgBitmap;
        [ObservableProperty]
        private SKImage _resultImage;

        [ObservableProperty]
        private float _xPos = 50;
        [ObservableProperty]
        private float _yPos = 50;
        [ObservableProperty]
        private float _photoScale = 1;

        public float CanvasScale => (float)0.8;
        public float CanvasWidth => (float)(BgBitmap?.Width ?? 1920) * CanvasScale;
        public float CanvasHeight => (float)(BgBitmap?.Height ?? 1080) * CanvasScale;

        public float XMax => (float)((BgBitmap?.Width ?? 1920) - (FgBitmap?.Width ?? 50)) ;
        public float YMax => (float)((BgBitmap?.Height ?? 1080) - (FgBitmap?.Height ?? 50));

        public MainPageViewModel()
        {
            Task.Run(async () => await LoadImages());
        }

        partial void OnBgBitmapChanged(SKBitmap value)
        {
            OnPropertyChanged(nameof(XMax));
            OnPropertyChanged(nameof(YMax));
            Debug.WriteLine($"XMax: {XMax}; YMAx: {YMax}");
        }

        partial void OnFgBitmapChanged(SKBitmap value)
        {             
            OnPropertyChanged(nameof(XMax));
            OnPropertyChanged(nameof(YMax));
            Debug.WriteLine($"XMax: {XMax}; YMAx: {YMax}");
        }

        [RelayCommand]
        public async Task LoadImages()
        {
            BgBitmap = SKBitmap.Decode(BackgroundImagePath);
            FgBitmap = SKBitmap.Decode(ForegroundImagePath);
            // Загружаем вторую картинку (накладываем сверху)

        }

        [RelayCommand]
        public async Task SaveImage()
        {
#if WINDOWS
            var savePath = await ShowSaveFileDialogAsync();
            if (savePath != null && ResultImage != null) {
                using var stream = File.OpenWrite(savePath);
                using var data = ResultImage.Encode(SKEncodedImageFormat.Png, 100);
                data.SaveTo(stream);
            }
#endif
        }

#if WINDOWS
    

        public async Task<string?> ShowSaveFileDialogAsync()
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("PNG Image", new List<string>() { ".png" });
            picker.SuggestedFileName = "result";

            // нужно привязать к окну MAUI
            var hwnd = WindowNative.GetWindowHandle(App.Current.Windows[0].Handler.PlatformView);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            return file?.Path;
        }
#endif
    }


}
