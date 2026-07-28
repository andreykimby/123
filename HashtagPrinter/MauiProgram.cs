using CommunityToolkit.Maui;
using FFImageLoading.Maui;
using Skarynagram.Features.AccountStorageService.Service;
using Skarynagram.Features.AdminPage.ViewModels;
using Skarynagram.Features.AdminPage.Views;
using Skarynagram.Features.AppSettingsService.Repositories;
using Skarynagram.Features.AppSettingsService.Services;
using Skarynagram.Features.AuthService.Services;
using Skarynagram.Features.ClientPage.Views;
using Skarynagram.Features.InstaViewer.Services;
using Skarynagram.Features.LoggingHandler.LoggingHandlerService;
using Skarynagram.Features.ModalNavigationService;
using Skarynagram.Features.PrintStatistics;
using Skarynagram.Features.ImageTransformation;
using Skarynagram.Features.RemoteControl;
using Skarynagram.Features.Services;
using Skarynagram.Features.ViewModels;
using Skarynagram.Features.Views;
using Skarynagram.Platforms.Windows.Features.PrintService.Service;
using MauiPageFullScreen;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Handlers;
using NLog;
using NLog.Extensions.Logging;
using Sharpnado.CollectionView;
using SkiaSharp.Views.Maui.Controls.Hosting;
using System.Net;
using Utils.PasswordPopup;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Skarynagram
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
#if WINDOWS
            System.Net.ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
#endif
            var builder = MauiApp.CreateBuilder();
            ButtonHandler.Mapper.AppendToMapping("SkarynahramButtonMotion", (_, view) =>
            {
                if (view is not Button button)
                    return;
                button.Pressed -= AnimateButtonPressed;
                button.Released -= AnimateButtonReleased;
                button.Pressed += AnimateButtonPressed;
                button.Released += AnimateButtonReleased;
            });
            // Настройка NLog
            LogManager.Setup().LoadConfigurationFromFile("NLog.config");

            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            //builder.Logging.AddFilter("Microsoft.Maui.Controls.UriImageSource", LogLevel.Error);
            //builder.Logging.AddFilter("Microsoft.Maui.UriImageSourceService", LogLevel.Error);
            
            builder.Logging.AddNLog();


            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .UseFullScreen()
                .UseFFImageLoading()
                // The admin window owns application shutdown in App.CreateWindow.
                // A global OnClosed handler would also terminate the process when
                // the secondary client window is closed.
                .UseMauiCommunityToolkit(options =>
                    options.SetShouldEnableSnackbarOnWindows(false))
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .UseSharpnadoCollectionView(loggerEnable: true);


#if DEBUG
            builder.Logging.AddDebug();
#endif
            builder.Services.AddTransient<LoggingHandlerService>();
            builder.Services.AddHttpClient<IAuthService, AuthService>(
                client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                });
            builder.Services.AddHttpClient<InstaViewerService>(
                client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                })
                .AddHttpMessageHandler<LoggingHandlerService>();

            builder.Services.AddSingleton<IModalNavigationService, ModalNavigationService>();
            builder.Services.AddSingleton<IAccountStorageService, JsonAccountStorageService>();
            builder.Services.AddSingleton<IPrintService, PrintService>();
            builder.Services.AddSingleton<IPrintStatisticsService, PrintStatisticsService>();
            builder.Services.AddSingleton<RemoteControlServer>();
            builder.Services.AddSingleton<IInstaMediaStoreService>(sp =>
            {
                    var logger = sp.GetRequiredService<ILogger<InstaMediaStoreService>>();
                    var perPage = 27;
                    return new InstaMediaStoreService(logger, perPage);
            });
            builder.Services.AddSingleton<ImageDownloader>();
            builder.Services.AddHttpClient<HistoricalImageService>();

            builder.Services.AddSingleton<ImagesService>();

            builder.Services.AddTransient<AdminPage, AdminPageViewModel>();
            builder.Services.AddTransient<TemplateEditorPage>();
            builder.Services.AddTransient<PrintDialogView, PrintDialogViewModel>() ;
            builder.Services.AddTransient<ClientPage, ClientPageViewModel>();
            builder.Services.AddTransientPopup<PasswordPopup, PasswordPopupViewModel>();
            builder.Services.AddSingleton<AppSettings>();
            builder.Services.AddSingleton<IAppSettingsRepository, JsonAppSettingsRepository>();
            builder.Services.AddSingleton<SettingsTransferService>();

            return builder.Build();
        }

        private static async void AnimateButtonPressed(object? sender, EventArgs e)
        {
            if (sender is Button button)
                await button.ScaleTo(0.965, 65, Easing.CubicOut);
        }

        private static async void AnimateButtonReleased(object? sender, EventArgs e)
        {
            if (sender is Button button)
                await button.ScaleTo(1, 140, Easing.CubicOut);
        }
    }
}
