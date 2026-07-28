namespace Skarynagram.Mobile.Models;

public sealed record RemoteStatus(
    bool Connected,
    string ComputerName,
    string Hashtag,
    int PrintedToday,
    long PrintedTotal,
    string SelectedPrinter,
    string SelectedPaper);

public sealed record PrintOptions(
    IReadOnlyList<string> Printers,
    IReadOnlyList<string> Papers,
    string SelectedPrinter,
    string SelectedPaper);

public sealed record PrintOptionsUpdate(string Printer, string Paper);
