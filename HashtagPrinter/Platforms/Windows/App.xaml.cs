using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Skarynagram.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            UnhandledException += (_, args) => WriteStartupFailure(args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                WriteStartupFailure(args.ExceptionObject as Exception);
            this.InitializeComponent();
        }

        private static void WriteStartupFailure(Exception? exception)
        {
            try
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Skarynagram", "logs");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "startup-crash.log"),
                    $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                // Never mask the original startup failure.
            }
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
