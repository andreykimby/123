using Skarynagram.Features.AppSettingsService.Services;
using Skarynagram.Features.PrintService.Service;
using Skarynagram.Platforms.Windows.Features.PrintService.Service;
using System.Diagnostics;
using System.Drawing.Printing;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Skarynagram.Features.AdminPage.Views;

public partial class TemplateEditorPage : ContentPage
{
    private readonly AppSettings _settings;
    private readonly IPrintService _printService;
    private double _x, _y, _size, _panStartX, _panStartY;
    private string _templatePath;
    private bool _portrait;
    private readonly List<LayoutTextField> _texts;
    private LayoutTextField? _selectedText;
    private readonly Dictionary<LayoutTextField, Label> _textLabels = new();
    private double _textPanStartX, _textPanStartY;
    private bool _updatingControls;
    private bool _loadingPrintOptions;
    private SKBitmap? _templateBitmap;

    public TemplateEditorPage(AppSettings settings, IPrintService printService)
    {
        InitializeComponent();
        _settings = settings;
        _printService = printService;
        var hasCurrentLayout = settings.PhotoLayoutSaved && settings.PhotoLayoutVersion >= 2;
        _x = hasCurrentLayout ? Math.Clamp(settings.FgPhotoXPos, 0, 1) : 0.5;
        _y = hasCurrentLayout ? Math.Clamp(settings.FgPhotoYPos, 0, 1) : 0.5;
        _size = Math.Clamp(settings.FgPhotoScale, 0.1, 1);
        _templatePath = settings.TemplatePath ?? string.Empty;
        _portrait = settings.FgPhotoOrientation != "Landscape";
        _texts = settings.LayoutTextFields.Select(CloneText).ToList();
        FontPicker.SelectedIndex = 0;
        LoadPrintOptions();
        ShowTemplate();
        BuildTextLabels();
    }

    private void LoadPrintOptions()
    {
        var current = _settings.PrintSettings ?? PrintSettings.Default();
        _loadingPrintOptions = true;
        _printService.PrintSettings = current;
        PrinterPicker.ItemsSource = _printService.ListPrinters();
        PrinterPicker.SelectedItem = current.PrinterName;
        _printService.PrintSettings.PrinterName = current.PrinterName;
        ReloadPaperOptions(current.PaperSize);
        _loadingPrintOptions = false;
        UpdateSelectedPaperInfo();
    }

    private void ReloadPaperOptions(PaperSize? preferred = null)
    {
        var papers = _printService.GetPaperSizes();
        PaperPicker.ItemsSource = papers;
        PaperPicker.SelectedItem = papers.FirstOrDefault(p =>
            preferred != null && (p.PaperName == preferred.PaperName || (p.Width == preferred.Width && p.Height == preferred.Height)))
            ?? papers.FirstOrDefault();
    }

    private void PrinterPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_loadingPrintOptions) return;
        if (PrinterPicker.SelectedItem is not string printer) return;
        var previousPaper = PaperPicker.SelectedItem as PaperSize ?? _printService.PrintSettings.PaperSize;
        _printService.PrintSettings.PrinterName = printer;
        _loadingPrintOptions = true;
        ReloadPaperOptions(previousPaper);
        _loadingPrintOptions = false;
        UpdateSelectedPaperInfo();
        PreviewHost_SizeChanged(PreviewHost, EventArgs.Empty);
    }

    private async void OpenPrinterSettings_Clicked(object sender, EventArgs e)
    {
        if (PrinterPicker.SelectedItem is not string printer || string.IsNullOrWhiteSpace(printer))
        {
            await DisplayAlert("Налады прынтара", "Спачатку абярыце прынтар.", "Добра");
            return;
        }

        try
        {
            _printService.PrintSettings.PrinterName = printer;
            var startInfo = new ProcessStartInfo("rundll32.exe")
            {
                UseShellExecute = true
            };
            startInfo.ArgumentList.Add("printui.dll,PrintUIEntry");
            startInfo.ArgumentList.Add("/e");
            startInfo.ArgumentList.Add("/n");
            startInfo.ArgumentList.Add(printer);
            Process.Start(startInfo);
        }
        catch (Exception exception)
        {
            await DisplayAlert(
                "Налады прынтара",
                $"Не атрымалася адкрыць налады прынтара: {exception.Message}",
                "Добра");
        }
    }

    private void PaperPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_loadingPrintOptions) return;
        if (PaperPicker.SelectedItem is not PaperSize paper) return;
        _printService.PrintSettings.PaperSize = paper;
        UpdateSelectedPaperInfo();
        PreviewHost_SizeChanged(PreviewHost, EventArgs.Empty);
    }

    private void UpdateSelectedPaperInfo()
    {
        if (PaperPicker.SelectedItem is PaperSize paper)
            SelectedPaperInfo.Text = $"{paper.PaperName}: {paper.Width / 100d:0.##} × {paper.Height / 100d:0.##} дюйма";
        else
            SelectedPaperInfo.Text = "Формат не выбран";
    }

    private static LayoutTextField CloneText(LayoutTextField value) => new()
    { Text = value.Text, FontFamily = NormalizeFont(value.FontFamily), FontSize = value.FontSize, X = value.X, Y = value.Y };

    private static string NormalizeFont(string? fontFamily) =>
        string.IsNullOrWhiteSpace(fontFamily) || fontFamily.Equals("Arial", StringComparison.OrdinalIgnoreCase)
            ? "OpenSansRegular"
            : fontFamily;

    private void ShowTemplate()
    {
        TemplatePathLabel.Text = string.IsNullOrWhiteSpace(_templatePath) ? "Шаблон не абраны — будзе белы фон" : _templatePath;
        _templateBitmap?.Dispose();
        _templateBitmap = File.Exists(_templatePath) ? SKBitmap.Decode(_templatePath) : null;
        UpdateLayout();
    }

    private async void ChooseTemplate_Clicked(object sender, EventArgs e)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Абярыце выяву шаблона", FileTypes = FilePickerFileType.Images });
        if (result == null) return;
        var extension = Path.GetExtension(result.FileName);
        var savedPath = Path.Combine(FileSystem.AppDataDirectory, "saved-layout-template" + extension);
        await using (var input = await result.OpenReadAsync())
        await using (var output = File.Create(savedPath))
            await input.CopyToAsync(output);
        _templatePath = savedPath;
        ShowTemplate();
    }

    private void ClearTemplate_Clicked(object sender, EventArgs e) { _templatePath = string.Empty; ShowTemplate(); }

    private void PreviewHost_SizeChanged(object sender, EventArgs e)
    {
        if (PreviewHost.Width <= 0 || PreviewHost.Height <= 0) return;
        var printSettings = _printService.PrintSettings;
        var paperWidth = Math.Max(1, printSettings.PaperSize.Width);
        var paperHeight = Math.Max(1, printSettings.PaperSize.Height);
        var ratio = (double)Math.Min(paperWidth, paperHeight) / Math.Max(paperWidth, paperHeight);
        var maxWidth = PreviewHost.Width * 0.94;
        var maxHeight = PreviewHost.Height * 0.94;
        var surfaceWidth = _portrait ? maxHeight * ratio : maxWidth;
        var surfaceHeight = _portrait ? maxHeight : maxWidth * ratio;
        if (surfaceWidth > maxWidth) { surfaceWidth = maxWidth; surfaceHeight = _portrait ? maxWidth / ratio : maxWidth * ratio; }
        if (surfaceHeight > maxHeight) { surfaceHeight = maxHeight; surfaceWidth = _portrait ? maxHeight * ratio : maxHeight / ratio; }
        PreviewArea.WidthRequest = surfaceWidth;
        PreviewArea.HeightRequest = surfaceHeight;
        UpdateLayout();
    }

    private void PreviewArea_SizeChanged(object sender, EventArgs e) => UpdateLayout();

    private void UpdateLayout()
    {
        if (PreviewArea.Width <= 0 || PreviewArea.Height <= 0) return;
        var squareSize = Math.Min(PreviewArea.Width, PreviewArea.Height) * _size;
        AbsoluteLayout.SetLayoutBounds(PhotoFrame, new Rect((PreviewArea.Width - squareSize) * _x, (PreviewArea.Height - squareSize) * _y, squareSize, squareSize));
        foreach (var pair in _textLabels)
        {
            // 600 logical units is the common reference used by the print renderer.
            // Scaling here makes editor text match the final composed page.
            pair.Value.FontSize = pair.Key.FontSize * PreviewArea.Width / 600d;
            pair.Value.FontFamily = pair.Key.FontFamily;
            pair.Value.Text = pair.Key.Text;
            AbsoluteLayout.SetLayoutBounds(pair.Value, new Rect(pair.Key.X * PreviewArea.Width, pair.Key.Y * PreviewArea.Height, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        }
        OrientationButton.Text = _portrait ? "Змяніць арыентацыю (вертыкальны аркуш)" : "Змяніць арыентацыю (гарызантальны аркуш)";
        LayoutCanvas.InvalidateSurface();
    }

    private void LayoutCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        LayoutRenderer.Draw(e.Surface.Canvas, e.Info.Width, e.Info.Height, _templateBitmap,
            null, _x, _y, _size, _texts, showPhotoPlaceholder: true);
    }

    private void PhotoFrame_PanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Started) { _panStartX = _x; _panStartY = _y; }
        else if (e.StatusType == GestureStatus.Running)
        {
            var squareSize = Math.Min(PreviewArea.Width, PreviewArea.Height) * _size;
            _x = Math.Clamp(_panStartX + e.TotalX / Math.Max(1, PreviewArea.Width - squareSize), 0, 1);
            _y = Math.Clamp(_panStartY + e.TotalY / Math.Max(1, PreviewArea.Height - squareSize), 0, 1);
            UpdateLayout();
        }
    }

    private void BuildTextLabels()
    {
        foreach (var label in _textLabels.Values) PreviewArea.Remove(label);
        _textLabels.Clear();
        foreach (var field in _texts)
        {
            var label = new Label { TextColor = Colors.Transparent, BackgroundColor = Colors.Transparent, Padding = 4 };
            var tap = new TapGestureRecognizer(); tap.Tapped += (_, _) => SelectText(field);
            var pan = new PanGestureRecognizer(); pan.PanUpdated += (_, e) => TextPanUpdated(field, e);
            label.GestureRecognizers.Add(tap); label.GestureRecognizers.Add(pan);
            PreviewArea.Add(label); _textLabels[field] = label;
        }
        UpdateLayout();
    }

    private void SelectText(LayoutTextField field)
    {
        _selectedText = field; _updatingControls = true;
        TextEntry.Text = field.Text;
        FontPicker.SelectedItem = field.FontFamily;
        _updatingControls = false;
        foreach (var pair in _textLabels) pair.Value.BackgroundColor = pair.Key == field ? Color.FromArgb("#55FFD54F") : Colors.Transparent;
    }

    private void TextPanUpdated(LayoutTextField field, PanUpdatedEventArgs e)
    {
        SelectText(field);
        if (e.StatusType == GestureStatus.Started) { _textPanStartX = field.X; _textPanStartY = field.Y; }
        else if (e.StatusType == GestureStatus.Running)
        {
            field.X = Math.Clamp(_textPanStartX + e.TotalX / Math.Max(1, PreviewArea.Width), 0, .95);
            field.Y = Math.Clamp(_textPanStartY + e.TotalY / Math.Max(1, PreviewArea.Height), 0, .95);
            UpdateLayout();
        }
    }

    private void AddText_Clicked(object sender, EventArgs e) { var field = new LayoutTextField(); _texts.Add(field); BuildTextLabels(); SelectText(field); }
    private void DeleteText_Clicked(object sender, EventArgs e) { if (_selectedText == null) return; _texts.Remove(_selectedText); _selectedText = null; BuildTextLabels(); TextEntry.Text = string.Empty; }
    private void TextEntry_TextChanged(object sender, TextChangedEventArgs e) { if (!_updatingControls && _selectedText != null) { _selectedText.Text = e.NewTextValue ?? string.Empty; UpdateLayout(); } }
    private void FontPicker_SelectedIndexChanged(object sender, EventArgs e) { if (!_updatingControls && _selectedText != null && FontPicker.SelectedItem is string font) { _selectedText.FontFamily = font; UpdateLayout(); } }
    private void TextSizeDown_Clicked(object sender, EventArgs e) { if (_selectedText != null) { _selectedText.FontSize = Math.Max(8, _selectedText.FontSize - 2); UpdateLayout(); } }
    private void TextSizeUp_Clicked(object sender, EventArgs e) { if (_selectedText != null) { _selectedText.FontSize = Math.Min(120, _selectedText.FontSize + 2); UpdateLayout(); } }
    private void MoveText(double dx, double dy) { if (_selectedText != null) { _selectedText.X = Math.Clamp(_selectedText.X + dx, 0, .95); _selectedText.Y = Math.Clamp(_selectedText.Y + dy, 0, .95); UpdateLayout(); } }
    private void TextLeft_Clicked(object sender, EventArgs e) => MoveText(-.02, 0);
    private void TextRight_Clicked(object sender, EventArgs e) => MoveText(.02, 0);
    private void TextUp_Clicked(object sender, EventArgs e) => MoveText(0, -.02);
    private void TextDown_Clicked(object sender, EventArgs e) => MoveText(0, .02);

    private void MoveLeft_Clicked(object sender, EventArgs e) { _x = Math.Clamp(_x - 0.02, 0, 1); UpdateLayout(); }
    private void MoveRight_Clicked(object sender, EventArgs e) { _x = Math.Clamp(_x + 0.02, 0, 1); UpdateLayout(); }
    private void MoveUp_Clicked(object sender, EventArgs e) { _y = Math.Clamp(_y - 0.02, 0, 1); UpdateLayout(); }
    private void MoveDown_Clicked(object sender, EventArgs e) { _y = Math.Clamp(_y + 0.02, 0, 1); UpdateLayout(); }
    private void SizeDown_Clicked(object sender, EventArgs e) { _size = Math.Clamp(_size - 0.05, 0.1, 1); UpdateLayout(); }
    private void SizeUp_Clicked(object sender, EventArgs e) { _size = Math.Clamp(_size + 0.05, 0.1, 1); UpdateLayout(); }
    private void Orientation_Clicked(object sender, EventArgs e) { _portrait = !_portrait; PreviewHost_SizeChanged(PreviewHost, EventArgs.Empty); }

    private async void Save_Clicked(object sender, EventArgs e)
    {
        if (PrinterPicker.SelectedItem is not string || PaperPicker.SelectedItem is not PaperSize)
        {
            await DisplayAlert("Налады друку", "Абярыце прынтар і фармат паперы.", "Добра");
            return;
        }
        _settings.TemplatePath = _templatePath;
        _settings.FgPhotoXPos = _x; _settings.FgPhotoYPos = _y; _settings.FgPhotoScale = _size;
        _settings.FgPhotoOrientation = _portrait ? "Portrait" : "Landscape";
        _settings.LayoutTextFields = _texts.Select(CloneText).ToList();
        _settings.PhotoLayoutSaved = true;
        _settings.PhotoLayoutVersion = 2;
        if (PrinterPicker.SelectedItem is string printer)
            _printService.PrintSettings.PrinterName = printer;
        if (PaperPicker.SelectedItem is PaperSize paper)
            _printService.PrintSettings.PaperSize = paper;
        _settings.SelectedPrinter = _printService.PrintSettings.PrinterName;
        _settings.PrintSettings = _printService.PrintSettings;
        await Navigation.PopModalAsync();
    }
    private async void Cancel_Clicked(object sender, EventArgs e) => await Navigation.PopModalAsync();
}
