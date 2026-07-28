using Skarynagram.Mobile.Services;

namespace Skarynagram.Mobile;

public partial class MainPage : ContentPage
{
    private readonly DesktopApiClient _api;

    public MainPage(DesktopApiClient api)
    {
        InitializeComponent();
        _api = api;
        AddressEntry.Text = Preferences.Default.Get("computer_address", "192.168.1.10:9877");
        PinEntry.Text = Preferences.Default.Get("computer_pin", string.Empty);
    }

    private async void Connect_Clicked(object? sender, EventArgs e)
    {
        await RunSafely(async () =>
        {
            _api.Configure(AddressEntry.Text ?? string.Empty, PinEntry.Text ?? string.Empty);
            Preferences.Default.Set("computer_address", AddressEntry.Text ?? string.Empty);
            Preferences.Default.Set("computer_pin", PinEntry.Text ?? string.Empty);
            var status = await _api.GetStatusAsync()
                ?? throw new InvalidOperationException("Камп'ютар не вярнуў стан.");
            var options = await _api.GetPrintOptionsAsync()
                ?? throw new InvalidOperationException("Камп'ютар не вярнуў налады друку.");

            ConnectionStatus.Text = $"Злучана: {status.ComputerName}";
            HashtagLabel.Text = "#" + status.Hashtag.TrimStart('#');
            TodayLabel.Text = status.PrintedToday.ToString();
            TotalLabel.Text = status.PrintedTotal.ToString();
            PrinterPicker.ItemsSource = options.Printers.ToList();
            PrinterPicker.SelectedItem = options.SelectedPrinter;
            PaperPicker.ItemsSource = options.Papers.ToList();
            PaperPicker.SelectedItem = options.SelectedPaper;
        });
    }

    private async void SavePrintOptions_Clicked(object? sender, EventArgs e)
    {
        await RunSafely(async () =>
        {
            if (PrinterPicker.SelectedItem is not string printer
                || PaperPicker.SelectedItem is not string paper)
                throw new InvalidOperationException("Абярыце прынтар і фармат паперы.");
            await _api.UpdatePrintOptionsAsync(printer, paper);
        });
    }

    private async Task RunSafely(Func<Task> action)
    {
        ErrorLabel.Text = string.Empty;
        try { await action(); }
        catch (Exception exception) { ErrorLabel.Text = exception.Message; }
    }
}
