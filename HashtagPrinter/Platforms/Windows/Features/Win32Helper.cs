#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace Skarynagram.Platforms.Windows
{
    public static class Win32Helper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

        private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        const uint MONITOR_DEFAULTTONEAREST = 2;

        /// <summary>
        /// Возвращает индекс монитора (0, 1, 2...) для указанного окна
        /// </summary>
        public static int GetMonitorIndexFromWindow(Microsoft.Maui.Controls.Window mauiWindow)
        {
            if (mauiWindow?.Handler?.PlatformView is Microsoft.Maui.Controls.Window nativeWindow)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

                // Перечисляем все мониторы и находим индекс
                int index = 0;
                bool found = false;
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                    (hMonitor, hdcMonitor, ref rect, data) =>
                    {
                        if (hMonitor == monitor)
                        {
                            found = true;
                            return false; // остановить перечисление
                        }
                        index++;
                        return true;
                    }, IntPtr.Zero);

                return found ? index : 0;
            }
            return 0; // primary monitor
        }
    }
}
#endif
