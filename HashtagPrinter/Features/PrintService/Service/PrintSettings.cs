using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Text;

namespace Skarynagram.Features.PrintService.Service
{

    public partial class PrintSettings: ObservableObject
    {
        [ObservableProperty]
        public partial string PrinterName { get; set; } = "Microsoft Print to PDF";
        [ObservableProperty]
        public partial Margins Margins { get; set; } = new Margins(0, 0, 0, 0);
        [ObservableProperty]
        public partial PaperKind PaperKind { get; set; } = PaperKind.A4;
        [ObservableProperty]
        public partial Duplex Duplex { get; set; } = Duplex.Default;
        [ObservableProperty]
        public partial short Copies { get; set; } = 0;
        [ObservableProperty]
        public partial bool IsColor { get; set; } = true;
        [ObservableProperty]
        public partial PaperSize PaperSize { get; set; } = new PaperSize();

        public static PrintSettings Default() => new PrintSettings() {

            PrinterName = "Microsoft Print to PDF",
            Margins = new Margins(0, 0, 0, 0),
            PaperKind = PaperKind.A4,
            Duplex = Duplex.Default,
            Copies = 1,
            IsColor = true,
            // PaperSize uses hundredths of an inch: A4 is 210 x 297 mm.
            PaperSize = new PaperSize("A4", 827, 1169)
        };



    }
       
}
