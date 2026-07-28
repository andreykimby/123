using Skarynagram.Features.InstaViewer.Services;
using Skarynagram.Features.AppSettingsService.Services;
using NLog;
using Skarynagram.Features.RemoteControl;

namespace Skarynagram
{
    public partial class App : Application
    {
        private readonly InstaViewerService _instaViewerService;
        private readonly IInstaMediaStoreService _mediaStoreService;
        private readonly Features.Services.ImagesService _imagesService;
        private readonly RemoteControlServer _remoteControlServer;
        private bool _isShuttingDown;

        public App(
            InstaViewerService instaViewerService,
            IInstaMediaStoreService mediaStoreService,
            Features.Services.ImagesService imagesService,
            RemoteControlServer remoteControlServer)
        {
            _instaViewerService = instaViewerService;
            _mediaStoreService = mediaStoreService;
            _imagesService = imagesService;
            _remoteControlServer = remoteControlServer;
            InitializeComponent();

            // Migration is best-effort: old or locked files must never prevent the UI from starting.
            try
            {
                LegacyDataMigration.MigrateIfNeeded();
            }
            catch (Exception exception)
            {
                LogManager.GetCurrentClassLogger().Warn(exception, "Не атрымалася перанесці старыя налады");
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            _instaViewerService.Start();
            _remoteControlServer.Start();
            Sharpnado.CollectionView.Initializer.Initialize(true, false);
            var adminWindow = new Window(new AppShell());
            adminWindow.Destroying += OnAdminWindowDestroying;
            return adminWindow;
        }

        private void OnAdminWindowDestroying(object? sender, EventArgs e)
            => ShutDownApplication();

        internal void ShutDownApplication()
        {
            if (_isShuttingDown)
                return;

            _isShuttingDown = true;
            _instaViewerService.Stop();
            _imagesService.Dispose();
            _remoteControlServer.Stop();
            (_mediaStoreService as IDisposable)?.Dispose();

#if WINDOWS
            // The hidden client window must not retain the process or LiteDB lock.
            Microsoft.UI.Xaml.Application.Current.Exit();
#endif
        }
    }
}
