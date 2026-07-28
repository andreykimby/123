using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Skarynagram.Features.AppSettingsService.Services;
using Skarynagram.Features.InstaViewer.Entities;
using Skarynagram.Features.ModalNavigationService;
using Skarynagram.Features.PrintService.Service;
using Skarynagram.Features.PrintStatistics;
using Skarynagram.Features.ImageTransformation;
using Skarynagram.Features.Services;
using Skarynagram.Platforms.Windows.Features.PrintService.Service;
using Skarynagram.Utils;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;
using System.Diagnostics;
using Windows.Graphics.Printing;
using Windows.Storage.Pickers;
using WinRT.Interop;



namespace Skarynagram.Features.ViewModels
{
    public partial class PrintDialogViewModel : ObservableObject
    {
        private readonly ImageDownloader _imageDownloader;
        private readonly AppSettings _appSettings;
        private readonly IPrintService _printService;
        private readonly IPrintStatisticsService _printStatistics;
        private readonly HistoricalImageService _historicalImageService;
        private readonly ILogger _logger;

        [ObservableProperty]
        public partial MediaElement MediaElement { get; set; }

        [ObservableProperty]
        public partial string BackgroundImagePath { get; set; } = @"C:\Temp\drukar-template.png";

        [ObservableProperty]
        public partial string ForegroundImagePath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial SKBitmap? BgBitmap { get; set; }

        [ObservableProperty]
        public partial SKBitmap? FgBitmap { get; set; }

        [ObservableProperty]
        public partial SKImage? ResultImage { get; set; }

        [ObservableProperty]
        public partial double XPos { get; set; } = 0.5;

        [ObservableProperty]
        public partial double YPos { get; set; } = 0.5;

        [ObservableProperty]
        public partial double PhotoScale { get; set; } = 0.65;

        [ObservableProperty]
        public partial string PhotoOrientation { get; set; } = "Portrait";
        public IReadOnlyList<LayoutTextField> LayoutTextFields { get; private set; } = Array.Empty<LayoutTextField>();
        [ObservableProperty]
        public partial float FgTopOffset { get; set; } = 260;
        [ObservableProperty]
        public partial float FgBottomOffset { get; set; } = 1250;
        [ObservableProperty]
        public partial float FgLeftOffset { get; set; } = 80;
        private readonly IModalNavigationService _modalService;
        public float CanvasScale => (float)0.8;
        public float CanvasWidth => (float)(BgBitmap?.Width ?? 1920) * CanvasScale;
        public float CanvasHeight => (float)(BgBitmap?.Height ?? 1080) * CanvasScale;
        public event Action? RequestRedraw;
        public event Func<string, string, Task>? RequestAlert;
        public float XMax => (float)(BgBitmap?.Width ?? 1920);
        //public float XMax => (float)((BgBitmap?.Width ?? 1920) - (FgBitmap?.Width ?? 50));
        public float YMax => (float)(BgBitmap?.Height ?? 1080);
        //public float YMax => (float)((BgBitmap?.Height ?? 1080) - (FgBitmap?.Height ?? 50));
        [ObservableProperty]
        public partial bool VisibleSettings { get; set; } = false;
        [ObservableProperty]
        public partial string EnteredPassword { get; set; } = string.Empty;
        [ObservableProperty]
        public partial bool ShowPasswordEntry { get; set; } = false;
        [ObservableProperty]
        public partial string PasswordError { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddHistoryCommand))]
        public partial bool IsLoading { get; set; } = true;

        [ObservableProperty]
        public partial string LoadError { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsPrintingConfirmation { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddHistoryCommand))]
        public partial bool IsHistoricalPhoto { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddHistoryCommand))]
        public partial bool IsTransformingPhoto { get; set; }

        public string ShowSettingsTitle => VisibleSettings ? "Схаваць" : "Налады";
        

        public PrintDialogViewModel(MediaElement mediaElement, IModalNavigationService modalService, ILogger<PrintDialogViewModel> logger, ImageDownloader imageDownloader, IPrintService printService, IPrintStatisticsService printStatistics, AppSettings appSettings, HistoricalImageService historicalImageService)
        {
            MediaElement = mediaElement;
            _imageDownloader = imageDownloader;
            _appSettings = appSettings;
            _printService = printService;
            _printStatistics = printStatistics;
            _historicalImageService = historicalImageService;
            _logger = logger;
            _modalService = modalService;

            _ = LoadImages();
            LoadSettings();
        }

        partial void OnVisibleSettingsChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowSettingsTitle));
        }
        partial void OnBgBitmapChanged(SKBitmap value)
        {
            OnPropertyChanged(nameof(XMax));
            OnPropertyChanged(nameof(YMax));
            OnPropertyChanged(nameof(CanvasWidth));
            OnPropertyChanged(nameof(CanvasHeight));
            // Debug.WriteLine($"XMax: {XMax}; YMAx: {YMax}");
        }

        

        partial void OnFgLeftOffsetChanged(float oldValue, float newValue)
        {
            _appSettings.FgLeftOffset = newValue;
            RequestRedraw?.Invoke();
        }

        partial void OnFgTopOffsetChanged(float oldValue, float newValue)
        {
            _appSettings.FgTopOffset = newValue;
            RequestRedraw?.Invoke();
        }

        partial void OnFgBottomOffsetChanged(float oldValue, float newValue)
        {
            _appSettings.FgBottomOffset = newValue;
            RequestRedraw?.Invoke();
        }  

        partial void OnFgBitmapChanged(SKBitmap value)
        {
            OnPropertyChanged(nameof(XMax));
            OnPropertyChanged(nameof(YMax));
            OnPropertyChanged(nameof(CanvasWidth));
            OnPropertyChanged(nameof(CanvasHeight));
            // Debug.WriteLine($"XMax: {XMax}; YMAx: {YMax}");
            Debug.WriteLine($"XPos: {XPos}; YPos: {YPos}");
             //SaveSettings();
        }

        public void SaveSettings()
        {
            _appSettings.FgPhotoXPos = XPos;
            _appSettings.FgPhotoYPos = YPos;

            _appSettings.FgPhotoScale = PhotoScale;
        }

        [RelayCommand]
        public async Task CheckPassword()
        {
            if (EnteredPassword == _appSettings.ClientPassword)
            {
                EnteredPassword = string.Empty;
                PasswordError = string.Empty;
                VisibleSettings = true;
                ShowPasswordEntry = false;
            }
            else
            {
                EnteredPassword = string.Empty;
                PasswordError = "Няправільны пароль";
                VisibleSettings = false;
                ShowPasswordEntry = true;
            }
        }

        [RelayCommand]
        public async Task ShowSettings()
        {
            if (VisibleSettings)
            {
                VisibleSettings = false;
                ShowPasswordEntry = false;
                PasswordError = string.Empty;
                return;
            }
            EnteredPassword = string.Empty;
            PasswordError = string.Empty;
            ShowPasswordEntry = !ShowPasswordEntry;
        }

        [RelayCommand]
        private void AppendPasswordDigit(string digit)
        {
            if (digit.Length == 1 && char.IsDigit(digit[0]) && EnteredPassword.Length < 20)
                EnteredPassword += digit;
        }

        [RelayCommand]
        private void RemovePasswordDigit()
        {
            if (EnteredPassword.Length > 0)
                EnteredPassword = EnteredPassword[..^1];
        }

        public void LoadSettings()
        {
            var hasCurrentLayout = _appSettings.PhotoLayoutSaved && _appSettings.PhotoLayoutVersion >= 2;
            XPos = hasCurrentLayout ? _appSettings.FgPhotoXPos : 0.5;
            YPos = hasCurrentLayout ? _appSettings.FgPhotoYPos : 0.5;
            PhotoScale = _appSettings.FgPhotoScale;
            PhotoOrientation = _appSettings.FgPhotoOrientation;
            LayoutTextFields = _appSettings.LayoutTextFields;
            FgBottomOffset = _appSettings.FgBottomOffset;
            FgLeftOffset = _appSettings.FgLeftOffset;
            FgTopOffset = _appSettings.FgTopOffset;
        }

        [RelayCommand]
        public async Task LoadImages()
        {
            try
            {
                IsLoading = true;
                LoadError = string.Empty;

                ForegroundImagePath = await _imageDownloader.DownloadImageToTempAsync(MediaElement.MediaUrl);
                var foreground = SKBitmap.Decode(ForegroundImagePath)
                    ?? throw new InvalidOperationException("Не удалось прочитать загруженное изображение");

                BackgroundImagePath = string.IsNullOrWhiteSpace(_appSettings.TemplatePath)
                    ? @"C:\Temp\drukar-template.png"
                    : _appSettings.TemplatePath;

                var printSettings = _appSettings.PrintSettings ?? PrintSettings.Default();
                var paperWidth = Math.Max(1, printSettings.PaperSize.Width);
                var paperHeight = Math.Max(1, printSettings.PaperSize.Height);
                var portrait = _appSettings.FgPhotoOrientation != "Landscape";
                var shortSide = Math.Min(paperWidth, paperHeight);
                var longSide = Math.Max(paperWidth, paperHeight);
                var ratio = (double)shortSide / longSide;
                var targetWidth = portrait ? (int)(1800 * ratio) : 1800;
                var targetHeight = portrait ? 1800 : (int)(1800 * ratio);
                var background = new SKBitmap(targetWidth, targetHeight);
                background.Erase(SKColors.White);

                if (File.Exists(BackgroundImagePath))
                {
                    using var template = SKBitmap.Decode(BackgroundImagePath);
                    if (template != null)
                    {
                        var scale = Math.Min((float)targetWidth / template.Width, (float)targetHeight / template.Height);
                        var drawWidth = template.Width * scale;
                        var drawHeight = template.Height * scale;
                        var dest = new SKRect((targetWidth - drawWidth) / 2, (targetHeight - drawHeight) / 2,
                            (targetWidth + drawWidth) / 2, (targetHeight + drawHeight) / 2);
                        using var canvas = new SKCanvas(background);
                        canvas.DrawBitmap(template, dest);
                    }
                }
                else
                {
                    _logger.LogWarning("Шаблон не найден: {TemplatePath}. Используется белый фон", BackgroundImagePath);
                }

                IsHistoricalPhoto = false;
                FgBitmap = foreground;
                BgBitmap = background;
                RequestRedraw?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось подготовить изображение для печати");
                LoadError = "Не ўдалося загрузіць фота. Вярніцеся назад і паспрабуйце іншую выяву.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanAddHistory() =>
            !IsLoading && !IsTransformingPhoto && !IsHistoricalPhoto && FgBitmap != null;

        [RelayCommand(CanExecute = nameof(CanAddHistory))]
        public async Task AddHistory()
        {
            try
            {
                IsTransformingPhoto = true;
                LoadError = string.Empty;
                var result = await _historicalImageService.TransformPeopleAsync(
                    ForegroundImagePath,
                    _appSettings.OpenAiApiKey);
                var transformed = SKBitmap.Decode(result)
                    ?? throw new InvalidOperationException("Не ўдалося прачытаць апрацаванае фота.");
                var current = FgBitmap;
                FgBitmap = transformed;
                current?.Dispose();
                IsHistoricalPhoto = true;
                RequestRedraw?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не ўдалося дадаць старажытнасці да фота");
                LoadError = ex.Message;
                var alert = RequestAlert;
                if (alert != null)
                {
                    try
                    {
                        await alert("Апрацоўка не выканана", ex.Message);
                    }
                    catch (Exception alertException)
                    {
                        // A failure in the error dialog must never terminate the kiosk.
                        _logger.LogError(alertException, "Не ўдалося паказаць паведамленне пра памылку СКАРЫНАДРУК");
                    }
                }
            }
            finally
            {
                IsTransformingPhoto = false;
            }
        }

        [RelayCommand]
        public async Task SaveImage()
        {
#if WINDOWS
            var savePath = await ShowSaveFileDialogAsync();
            if (savePath != null && ResultImage != null)
            {
                using var stream = File.OpenWrite(savePath);
                using var data = ResultImage.Encode(SKEncodedImageFormat.Png, 100);
                data.SaveTo(stream);
            }
#endif
        }



        [RelayCommand]
        public async Task PrintImage()
        {
            SaveSettings();
            string fileName = $"{Guid.NewGuid()}.png";
            string folder = FileSystem.CacheDirectory; // временная папка
            string filePath = Path.Combine(folder, fileName);
            if (ResultImage == null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Друк недаступны",
                    "Выява яшчэ не падрыхтавана. Дачакайцеся перадпрагляду.",
                    "Добра");
                return;
            }

            if (string.IsNullOrWhiteSpace(_printService.PrintSettings.PrinterName))
            {
                await Shell.Current.DisplayAlertAsync(
                    "Прынтар не абраны",
                    "Вярніцеся ў налады і абярыце прынтар.",
                    "Добра");
                return;
            }

            if (ResultImage != null)
            {
                try
                {
                    
                    using (var stream = File.OpenWrite(filePath))
                    {
                        
                        //await _printService.PrintImageAsync(stream);
                        using (var data = ResultImage.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            data.SaveTo(stream);


                        }

                    }
                    using (Stream streamImage = new MemoryStream())
                    {
                        ResultImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(streamImage);
                        streamImage.Position = 0; // Сброс позиции потока перед чтением
                        _logger.LogDebug("Начало печати изображения");
                        _logger.LogDebug($"Путь к изображению: {filePath}");
                        _logger.LogDebug($"Размер изображения: {streamImage.Length} байт");
                        _logger.LogDebug($"Позиция потока перед печатью: {streamImage.Position}");
                        IsPrintingConfirmation = true;
                        var printingIndicatorStarted = Stopwatch.StartNew();
                        await _printService.PrintImageAsync(streamImage);
                        var copies = Math.Max(1, (int)_printService.PrintSettings.Copies);
                        await _printStatistics.RecordSuccessfulPrintAsync(copies);
                        await _printStatistics.SavePrintedPhotoAsync(filePath);
                        _logger.LogDebug("Печать изображения завершена успешно");
                        var remaining = TimeSpan.FromSeconds(5) - printingIndicatorStarted.Elapsed;
                        if (remaining > TimeSpan.Zero)
                            await Task.Delay(remaining);
                    }
                    IsPrintingConfirmation = false;
                    await _modalService.CloseModalAsync();
                    //var activeWindow = Application.Current?.Windows.LastOrDefault();
                    //if (activeWindow != null)
                    //{
                    //    await _printService.PrintAsync(filePath, activeWindow);
                    //}
                    //await _modalService.CloseModalAsync();

                    //if (App.Current?.Windows?.Count > 0)
                    //{
                    //    var window = App.Current.Windows[1].Handler.PlatformView as Microsoft.UI.Xaml.Window;
                    //    var hwnd = WindowNative.GetWindowHandle(window);
                    //    // дальше как выше...
                    //    if (window != null)
                    //        _printService.Print(filePath, _appSettings.SelectedPrinter, activeWindow);
                    //}


                }
                catch (Exception ex)
                {
                    IsPrintingConfirmation = false;
                    _logger.LogError(ex, "Ошибка при печати изображения");
                    await Shell.Current.DisplayAlertAsync(
                        "Памылка друку",
                        ex.Message,
                        "Добра");
                }
            }
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
