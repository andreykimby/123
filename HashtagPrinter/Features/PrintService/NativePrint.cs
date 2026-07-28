using System;
using System.Collections.Generic;
using System.Text;

namespace Skarynagram.Features.PrintService
{
#if WINDOWS
    using System.Runtime.InteropServices;
    using static Skarynagram.Features.PrintService.NativePrint;

    public static class NativePrint
    {
        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PrintDlg(ref PRINTDLG pd);

        

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PRINTDLG
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hDevMode;
            public IntPtr hDevNames;
            public IntPtr hdc;
            public uint Flags;
            public short nFromPage;
            public short nToPage;
            public short nMinPage;
            public short nMaxPage;
            public IntPtr nCopies;
            public IntPtr hInstance;
            public IntPtr lCustData;
            public IntPtr lpfnPrintHook;
            public IntPtr lpfnSetupHook;
            public string lpPrintTemplateName;
            public IntPtr hPrintTemplate;
            public IntPtr lpCallback;
            public short nPageType;
            public int nSize;
            public IntPtr lpMinMargin;
            public IntPtr lpMargin;
            public IntPtr hSetupTemplate;
        }

        public static bool ShowPrintDialog(IntPtr hwnd)
        {
            var pd = new PRINTDLG
            {
                lStructSize = Marshal.SizeOf<PRINTDLG>(),
                hwndOwner = hwnd,
                Flags = 0x8000 // PD_USEDEVMODECOPIES
            };
            return PrintDlg(ref pd);
        }
    }
}
#endif