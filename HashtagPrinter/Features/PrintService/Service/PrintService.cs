using Skarynagram.Features.PrintService.Service;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics.Printing;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;
using Image = Microsoft.UI.Xaml.Controls.Image;

namespace Skarynagram.Platforms.Windows.Features.PrintService.Service
{
    public class PrintService : IPrintService
    {
        private static readonly IntPtr DpiAwarenessContextUnaware = new(-1);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        private readonly ILogger<PrintService> _logger;
        public PrintSettings PrintSettings { get; set; } = PrintSettings.Default();
        public PrinterSettings PrinterSettings { get; set; } = new PrinterSettings();
        private string _selectedPrinter = string.Empty;
        private List<PaperSize> _paperSizes = new();
        public PrintService(ILogger<PrintService> logger)
        {
            _logger = logger;
        }

        public void SetSettings(PrintSettings printSettings)
        {
            PrintSettings = printSettings;
        }

        public List<string> ListPrinters()
        {
            var printers = new List<string>();
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                printers.Add(printer);
            }
            return printers;
        }
        public void Print(string imagePath, string printerName, Microsoft.Maui.Controls.Window window)
        {


            using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
            {
                using (var image = System.Drawing.Image.FromStream(stream))
                {

                    //
                    try
                    {
                        //var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                        var windowX = (int)window.X;
                        var windowY = (int)window.Y;

                        var printHelperPath = Path.Combine(AppContext.BaseDirectory, "PrintHelper.exe");
                        if (!File.Exists(printHelperPath))
                        {
                            _logger.LogDebug($"PrintHelper.exe NOT FOUND! {printHelperPath}");
                            return;
                        }
                        if (!File.Exists(imagePath))
                        {
                            _logger.LogDebug($"Image file NOT FOUND! {imagePath}");
                            return;
                        }
                        //var psi = new ProcessStartInfo
                        //{
                        //    FileName = printHelperPath,
                        //    Arguments = $"\"{imagePath}\" --x={windowX} --y={windowY}",
                        //    UseShellExecute = true,
                        //};
                        var psi = new ProcessStartInfo
                        {
                            FileName = imagePath,
                            Verb = "print",           // именно "print" — без кавычек
                            UseShellExecute = true,             // обязательно!
                            //CreateNoWindow = true,             // скрыть окно, если приложение его открывает
                            //WindowStyle = ProcessWindowStyle.Hidden
                        };
                        _logger.LogDebug("Печать изображения {ImagePath} на принтере {PrinterName}", imagePath, printerName);
                        var process = Process.Start(psi);
                        process?.WaitForExit();
                        _logger.LogDebug("Процесс печати запущен. Id={ProcessId}", process?.Id);
#if WINDOWS
                        //NativePrint.ShowPrintDialog(WinRT.Interop.WindowNative.GetWindowHandle(window));
#endif
                    }
                    catch (Exception ex)
                    {

                        _logger.LogError(ex, "Ошибка при печати изображения");
                    }
                    
                    //
                    //_ = PrintAsync(imagePath, window);
                    //System.Drawing.Printing.PrintDocument pd = new System.Drawing.Printing.PrintDocument();
                    //using var printDialog = new PrintDialog
                    //{
                    //    UseEXDialog = true,
                    //    AllowSomePages = true,
                    //    ShowHelp = false,
                    //    Document = pd
                    //    //pd.PrinterSettings.PrinterName = printerName;

                    //    //pd.PrintPage += (sender, e) => e.Graphics?.DrawImage(image, e.MarginBounds);
                    //    //pd.Print();

                    //};
                    //if (printDialog.ShowDialog() == DialogResult.OK)
                    //{
                    //    // настройки из диалога копируются в Document.PrinterSettings
                    //    pd.PrinterSettings = printDialog.PrinterSettings;  // необязательно, но явно
                    //    pd.Print();
                    //}
                }

            }
        }

        public async Task PrintAsync(string imagePath, Microsoft.Maui.Controls.Window window)
        {
            if (window == null) {
                _logger.LogError("Ошибка печати, window не может быть null");
                return;
                }

            await PrintWithParamsAsync(imagePath, PrintSettings);
        }

        public async Task PrintImageAsync(Stream imageStream)
        {
#if WINDOWS
            if (PrintSettings == null)
            {
                throw new ArgumentNullException(nameof(PrintSettings));
            }
            using var imageBuffer = new MemoryStream();
            if (imageStream.CanSeek)
                imageStream.Position = 0;
            await imageStream.CopyToAsync(imageBuffer);
            var imageBytes = imageBuffer.ToArray();

            try
            {
                await Task.Run(() =>
                {
                    // MAUI runs PerMonitorV2-aware. System.Drawing.Printing then treats a
                    // 96-DPI bitmap as 192 DPI on a 200% display and sends only its upper-left
                    // quarter to GDI. Bitmap and PrintDocument must both be created while this
                    // worker thread is temporarily DPI-unaware.
                    var previousDpiContext = SetThreadDpiAwarenessContext(DpiAwarenessContextUnaware);
                    try
                    {
                        using var localStream = new MemoryStream(imageBytes, writable: false);
                        using var bitmap = new Bitmap(localStream);
                        using var printDoc = new PrintDocument();
                        printDoc.PrinterSettings.PrinterName = PrintSettings.PrinterName;
                        var requestedPaper = PrintSettings.PaperSize;
                        var driverPaper = printDoc.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(p =>
                            p.RawKind == requestedPaper.RawKind && p.Width == requestedPaper.Width && p.Height == requestedPaper.Height)
                            ?? printDoc.PrinterSettings.PaperSizes.Cast<PaperSize>().FirstOrDefault(p =>
                                string.Equals(p.PaperName, requestedPaper.PaperName, StringComparison.OrdinalIgnoreCase)
                                && p.Width == requestedPaper.Width && p.Height == requestedPaper.Height);
                        if (driverPaper == null)
                            throw new InvalidOperationException($"Принтер не поддерживает формат {requestedPaper.PaperName} ({requestedPaper.Width}x{requestedPaper.Height}).");

                        printDoc.PrinterSettings.Copies = PrintSettings.Copies;
                        printDoc.PrinterSettings.Duplex = PrintSettings.Duplex;
                        printDoc.DefaultPageSettings.Margins = PrintSettings.Margins;
                        printDoc.DefaultPageSettings.PaperSize = driverPaper;
                        printDoc.PrinterSettings.DefaultPageSettings.PaperSize = driverPaper;
                        printDoc.DefaultPageSettings.Landscape = bitmap.Width > bitmap.Height;
                        printDoc.DefaultPageSettings.Color = PrintSettings.IsColor;
                        printDoc.PrintPage += (_, args) =>
                        {
                            if (args.Graphics == null)
                            {
                                args.HasMorePages = false;
                                return;
                            }

                            var graphics = args.Graphics;
                            args.PageSettings.PaperSize = driverPaper;
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            var margins = PrintSettings.Margins;
                            var physicalWidth = printDoc.DefaultPageSettings.Landscape ? driverPaper.Height : driverPaper.Width;
                            var physicalHeight = printDoc.DefaultPageSettings.Landscape ? driverPaper.Width : driverPaper.Height;
                            var imageBounds = PrintGeometry.FitInsidePaper(
                                physicalWidth, physicalHeight,
                                margins.Left, margins.Right, margins.Top, margins.Bottom,
                                bitmap.Width, bitmap.Height);
                            graphics.DrawImage(bitmap, imageBounds,
                                0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel);
                            args.HasMorePages = false;
                        };

                        if (!printDoc.PrinterSettings.IsValid)
                            throw new InvalidOperationException($"Принтер '{PrintSettings.PrinterName}' не найден или недоступен.");

                        printDoc.Print();
                    }
                    finally
                    {
                        if (previousDpiContext != IntPtr.Zero)
                            SetThreadDpiAwarenessContext(previousDpiContext);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при печати изображения из потока");
                throw;
            }

            
#else
        throw new PlatformNotSupportedException("Печать реализована только для Windows.");
#endif
        }



        public async Task ShowSystemPrintDialogAsync(string imageFilePath, Microsoft.Maui.Controls.Window mauiWindow)
        {
            if (!File.Exists(imageFilePath))
            {
                _logger.LogError("Файл для печати не найден: {Path}", imageFilePath);
                return;
            }

            try
            {
                // Получаем HWND окна MAUI
                var platformWindow = mauiWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (platformWindow == null)
                {
                    _logger.LogError("Не удалось получить платформенное окно");
                    return;
                }

                var hwnd = WindowNative.GetWindowHandle(platformWindow);

                // Самый важный момент — регистрируем задачу печати
                PrintManager printMan = PrintManagerInterop.GetForWindow(hwnd);

                // Use the XAML/WinUI PrintDocument type so that Paginate, SetPreviewPageCount, etc. are available.
                var printDocument = new Microsoft.UI.Xaml.Printing.PrintDocument();
                IPrintDocumentSource documentSource = printDocument.DocumentSource;

                // Подписываемся на событие создания задания печати
                printDocument.Paginate += (sender, args) =>
                {
                    // Настройки опций (можно добавить/убрать)
                    var options = args.PrintTaskOptions;
                    var displayedOptions = options.DisplayedOptions;
                    displayedOptions.Clear();
                    displayedOptions.Add(StandardPrintTaskOptions.Copies);
                    displayedOptions.Add(StandardPrintTaskOptions.Orientation);
                    displayedOptions.Add(StandardPrintTaskOptions.MediaSize);
                    displayedOptions.Add(StandardPrintTaskOptions.Collation);
                    displayedOptions.Add(StandardPrintTaskOptions.Bordering);
                    displayedOptions.Add(StandardPrintTaskOptions.Staple);

                    // Указываем, что одна страница
                    printDocument.SetPreviewPageCount(1, Microsoft.UI.Xaml.Printing.PreviewPageCountType.Final);
                };

                printDocument.GetPreviewPage += async (sender, args) =>
                {
                    var pageVisual = await CreatePrintPageAsync(imageFilePath);
                    printDocument.SetPreviewPage(args.PageNumber, pageVisual);
                };

                printDocument.AddPages += async (sender, args) =>
                {
                    var pageVisual = await CreatePrintPageAsync(imageFilePath);
                    printDocument.AddPage(pageVisual);
                    printDocument.AddPagesComplete();
                };

                // Подписываемся на PrintTaskRequested
                TypedEventHandler<PrintManager, PrintTaskRequestedEventArgs> printTaskRequestedHandler = null!;
                printTaskRequestedHandler = (sender, args) =>
                {
                    args.Request.CreatePrintTask("Печать изображения", sourceRequestedArgs =>
                    {
                        sourceRequestedArgs.SetSource(documentSource);
                    });

                    // Отписываемся после создания задачи (одноразово)
                    printMan.PrintTaskRequested -= printTaskRequestedHandler;
                };
                printMan.PrintTaskRequested += printTaskRequestedHandler;

                // Показываем системный диалог печати
                await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);

                _logger.LogInformation("Диалог печати показан успешно");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при показе диалога печати");
                // Можно показать пользователю сообщение через DisplayAlert
            }
        }

        public async Task PrintWithParamsAsync(string imagePath, PrintSettings printSettings = null)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    _logger.LogError("Файл для печати не найден: {Path}", imagePath);
                    return;
                }

                using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    using (var image = System.Drawing.Image.FromStream(stream))
                    {
                        if (printSettings == null)
                        {
                            printSettings = PrintSettings.Default();
                        }

                        var pd = new System.Drawing.Printing.PrintDocument();
                        pd.PrinterSettings.PrinterName = printSettings.PrinterName;
                        pd.PrinterSettings.Duplex = printSettings.Duplex;
                        pd.PrinterSettings.Copies = printSettings.Copies;
                        pd.DefaultPageSettings.Margins = printSettings.Margins;
                        pd.DefaultPageSettings.PaperSize = printSettings.PaperSize;
                        pd.DefaultPageSettings.Landscape = image.Width > image.Height;
                        pd.DefaultPageSettings.Color = printSettings.IsColor;
                        
                        pd.OriginAtMargins = false;
                        pd.PrintPage += (sender, e) => {
                            e.PageSettings.Color = printSettings.IsColor;
                            var resol = new PrinterResolution();
                            resol.Kind = PrinterResolutionKind.High;
                            e.PageSettings.PrinterResolution = new PrinterResolution() { 
                                Kind    = PrinterResolutionKind.High,
                                X = 300,
                                Y = 300,
                            };
                            e.PageSettings.Margins = printSettings.Margins;
                            e.PageSettings.PaperSize = printSettings.PaperSize;
                            e.Graphics.DrawImage(image, e.PageBounds);
                            };

                        pd.Print();
                    }
                    GetPaperSizes();

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при показе диалога печати");
                // Можно показать пользователю сообщение через DisplayAlert
            }
        }

        public List<PaperSize> GetPaperSizes()
        {
            if (_selectedPrinter != PrintSettings.PrinterName)
            {
                _selectedPrinter = PrintSettings.PrinterName;
                using (var pd = new System.Drawing.Printing.PrintDocument())
                {

                    _logger.LogDebug($"System Default printer: {pd.DefaultPageSettings.PrinterSettings.PrinterName}");
                    foreach (PaperSize paperSize in pd.DefaultPageSettings.PrinterSettings.PaperSizes)
                    {
                        _logger.LogDebug($"{paperSize.PaperName}: {paperSize.Width}x{paperSize.Height} (сотых дюйма) {paperSize.Kind.ToString()}");
                    }
                    pd.PrinterSettings.PrinterName = PrintSettings.PrinterName;
                    _logger.LogDebug($"Selected printer: {pd.PrinterSettings.PrinterName}");
                    _paperSizes = new List<PaperSize>();
                    foreach (PaperSize paperSize in pd.PrinterSettings.PaperSizes)
                    {
                        _logger.LogDebug($"{paperSize.PaperName}: {paperSize.Width}x{paperSize.Height} (сотых дюйма) {paperSize.Kind.ToString()}");
                        _paperSizes.Add(paperSize);
                    }
                    return _paperSizes;
                }

            } else
            {
                return _paperSizes;
            }

            
        }

        public List<PrinterResolution> GetPrinterResolutions()
        {
            using (var pd = new System.Drawing.Printing.PrintDocument())
            {
                _logger.LogDebug($"Selected printer: {pd.PrinterSettings.PrinterName}");
                List<PrinterResolution> sizes = new List<PrinterResolution>();
                foreach (PrinterResolution paperSize in pd.PrinterSettings.PrinterResolutions)
                {
                    _logger.LogDebug($"Разрешение: {paperSize.Kind.ToString()}: {paperSize.X}x{paperSize.Y}");
                    sizes.Add(paperSize);
                }
                return sizes;
            }
        }

        

        // Вспомогательный метод для создания UIElement с изображением
        private async Task<UIElement> CreatePrintPageAsync(string imageFilePath, Microsoft.UI.Xaml.Media.Stretch stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill)
        {
            // Загружаем файл как StorageFile
            StorageFile file = await StorageFile.GetFileFromPathAsync(imageFilePath);

            // Создаём поток
            using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);

            // Создаём BitmapImage
            BitmapImage bitmapImage = new BitmapImage();
            await bitmapImage.SetSourceAsync(fileStream);

            // Создаём Image контроль
            Image imageControl = new Image
            {
                Source = bitmapImage,
                Stretch = stretch // Или UniformToFill, в зависимости от нужд
            };

            // Оборачиваем в Viewbox для авто-масштабирования под страницу
            Viewbox viewbox = new Viewbox
            {
                Child = imageControl,
                Stretch = stretch
            };

            return viewbox;
        }

        private async Task<UIElement> CreatePrintPageAsync(string imageFilePath, PrintPageDescription pageDesc)
        {
            // Загружаем файл как StorageFile
            StorageFile file = await StorageFile.GetFileFromPathAsync(imageFilePath);

            // Создаём поток
            using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);

            // Создаём BitmapImage
            BitmapImage bitmapImage = new BitmapImage();
            await bitmapImage.SetSourceAsync(fileStream);

            // Создаём Image контроль
            Image imageControl = new Image
            {
                Source = bitmapImage,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform // Или UniformToFill, в зависимости от нужд
            };

            // Оборачиваем в Viewbox для авто-масштабирования под страницу
            Viewbox viewbox = new Viewbox
            {
                Child = imageControl,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform
            };

            return viewbox;
        }


    }
}
