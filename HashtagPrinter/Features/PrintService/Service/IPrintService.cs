
using Skarynagram.Features.PrintService.Service;
using Microsoft.UI.Xaml;
using System.Drawing.Printing;

namespace Skarynagram.Platforms.Windows.Features.PrintService.Service
{
    public interface IPrintService
    {
        PrintSettings PrintSettings { get; set; }
        List<string> ListPrinters();
        List<PaperSize> GetPaperSizes();
        void Print(string imagePath, string printerName, Microsoft.Maui.Controls.Window window);
        Task PrintAsync(string imageFilePath, Microsoft.Maui.Controls.Window mauiWindow);
        Task PrintImageAsync(Stream imageStream);
    }
}