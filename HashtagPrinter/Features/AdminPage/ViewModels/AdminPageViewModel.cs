using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FFImageLoading;
using Skarynagram.Features.AccountStorageService.Service;
using Skarynagram.Features.AdminPage.Views;
using Skarynagram.Features.AppSettingsService.Services;
using Skarynagram.Features.AuthService.Models;
using Skarynagram.Features.AuthService.Services;
using Skarynagram.Features.InstaViewer.Services;
using Skarynagram.Features.InstaViewer.Messages;
using Skarynagram.Features.Services;
using Skarynagram.Features.PrintService.Service;
using Skarynagram.Features.PrintStatistics;
using Skarynagram.Platforms.Windows.Features.PrintService.Service;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing.Printing;
using System.Reflection;
using Utils.PasswordPopup;


namespace Skarynagram.Features.AdminPage.ViewModels
{
    public partial class AdminPageViewModel : ObservableValidator
    {
        private readonly ILogger<AdminPageViewModel> _logger;
        private readonly IPopupService _popupService;
        private readonly AppSettings _appSettings;
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuthService _authService;
        private readonly IPrintService _printService;
        private readonly IAccountStorageService _accountStorageService;
        private readonly InstaViewerService _instaViewerService;
        private readonly IInstaMediaStoreService _instaMediaStoreService;
        private readonly ImagesService _imagesService;
        private readonly SettingsTransferService _settingsTransferService;
        private readonly IPrintStatisticsService _printStatistics;

        public string ApplicationVersion =>
            $"Версія {AppInfo.Current.VersionString} · зборка {AppInfo.Current.BuildString}";

        [ObservableProperty]
        public partial string PrintStatisticsDay { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int PrintedToday { get; set; }

        [ObservableProperty]
        public partial long PrintedTotal { get; set; }

        [ObservableProperty]
        public partial string PrintStatisticsFilePath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AppId { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SelectedHashtag { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SelectedPrinter { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string RedirectUrl { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ObservableCollection<string> PrintersList { get; set; } = new ObservableCollection<string>();

        [ObservableProperty]
        public partial string AppSecret { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int SelectedPrinterIndex { get; set; } = -1;

        [ObservableProperty]
        public partial ObservableCollection<Account> Accounts { get; set; } = new ObservableCollection<Account>();

        [ObservableProperty]
        [Required(ErrorMessage = "Обязательно нужно выбрать аккаунт")]
        public partial Account SelectedAccount { get; set; } = Account.Default();

        [ObservableProperty]
        public partial int InstaViewerRequestDelay { get; set; }

        [ObservableProperty]
        public partial string TemplatePath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string HeaderValue { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string DescriptionValue { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ClientPassword { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string OpenAiApiKey { get; set; } = string.Empty;
        [ObservableProperty]
        public partial PrintSettings? PSPrintSettings { get; set; } = null;
        [ObservableProperty]
        public partial int PSMarginLeft { get; set; } = 0;
        [ObservableProperty]
        public partial int PSMarginRight { get; set; } = 0;
        [ObservableProperty]
        public partial int PSMarginTop { get; set; } = 0;
        [ObservableProperty]
        public partial int PSMarginBottom { get; set; } = 0;

        private List<PaperSize> _psPaperSizes = new();
        public List<PaperSize> PSPaperSizes {
            get => _printService.GetPaperSizes();
        }

        [ObservableProperty]
        public partial PaperSize PSSelectedPaper { get; set; }

        public AdminPageViewModel(ILogger<AdminPageViewModel> logger, IPopupService popupService, IAccountStorageService accountStorageService, AppSettings appSettings, IServiceProvider serviceProvider, IAuthService authService, IPrintService printService, InstaViewerService instaViewer, IInstaMediaStoreService instaMediaStoreService, ImagesService imagesService, SettingsTransferService settingsTransferService, IPrintStatisticsService printStatistics)
        {
            _logger = logger;
            _popupService = popupService;
            _appSettings = appSettings;
            _serviceProvider = serviceProvider;
            _authService = authService;
            _accountStorageService = accountStorageService;
            _printService = printService;
            _instaViewerService = instaViewer;
            _instaMediaStoreService = instaMediaStoreService;
            _imagesService = imagesService;
            _settingsTransferService = settingsTransferService;
            _printStatistics = printStatistics;
            _printStatistics.Changed += OnPrintStatisticsChanged;
            try
            {
                ApplyPrintStatistics(_printStatistics.GetSnapshot());
                AppId = _appSettings.AppId ?? "662839146654820";
                AppSecret = _appSettings.AppSecret ?? AppSecret;
                RedirectUrl = _appSettings.RedirectUrl ?? "http://localhost:9876/";
                SelectedHashtag = _appSettings.SelectedHashtag ?? string.Empty;

                //SelectedAccount = _appSettings.SelectedAccount;

                //LoadAccounts();
                var accounts = Task.Run(async () => await _accountStorageService.LoadAccountsAsync()).Result;
                if (accounts != null && accounts.Count > 0)
                {
                    accounts.ForEach(a => Accounts.Add(a));
                }
                var savedAccountName = _appSettings.SelectedAccount?.Name;
                SelectedAccount = accounts?.FirstOrDefault(
                    a => !string.IsNullOrWhiteSpace(savedAccountName) && a.Name == savedAccountName,
                    Account.Default()) ?? Account.Default();

                HeaderValue = _appSettings.HeaderValue;
                DescriptionValue = _appSettings.DescriptionValue;
                TemplatePath = _appSettings.TemplatePath;
                InstaViewerRequestDelay = _appSettings.InstaViewerRequestDelay;
                ClientPassword = _appSettings.ClientPassword;
                OpenAiApiKey = _appSettings.OpenAiApiKey;

                PSPrintSettings = _appSettings.PrintSettings;
                _printService.ListPrinters().ForEach(item => PrintersList.Add(item));
                if (PSPrintSettings != null)
                {
                    SelectedPrinter = PrintersList.FirstOrDefault(s => s == PSPrintSettings?.PrinterName) ?? string.Empty;
                    PSMarginLeft = PSPrintSettings.Margins.Left;
                    PSMarginRight = PSPrintSettings.Margins.Right;
                    PSMarginTop = PSPrintSettings.Margins.Top;
                    PSMarginBottom = PSPrintSettings.Margins.Bottom;
                    PSSelectedPaper = _printService.GetPaperSizes().FirstOrDefault(s => s.PaperName.Contains(PSPrintSettings.PaperSize.PaperName));
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Ошибка при загрузки настроек:");

            }
        }

        private void OnPrintStatisticsChanged(object? sender, PrintStatisticsSnapshot snapshot) =>
            MainThread.BeginInvokeOnMainThread(() => ApplyPrintStatistics(snapshot));

        private void ApplyPrintStatistics(PrintStatisticsSnapshot snapshot)
        {
            PrintStatisticsDay = snapshot.Day.ToString("dd.MM.yyyy");
            PrintedToday = snapshot.DayCount;
            PrintedTotal = snapshot.TotalCount;
            PrintStatisticsFilePath = snapshot.FilePath;
        }

        [RelayCommand]
        private async Task ExportSettings()
        {
            var saved = await _settingsTransferService.ExportAsync();
            if (saved)
                await Shell.Current.DisplayAlertAsync("Рэзервовая копія", "Налады паспяхова захаваны.", "Добра");
        }

        [RelayCommand]
        private async Task ImportSettings()
        {
            if (!await _settingsTransferService.ImportAsync())
                return;
            await Shell.Current.DisplayAlertAsync(
                "Налады адноўлены",
                "Закрыйце і зноў запусціце Skarynagram, каб прымяніць усе налады.",
                "Добра");
        }

        partial void OnSelectedPrinterChanged(string oldValue, string newValue)
        {
            OnPropertyChanged(nameof(PSPaperSizes));
            _printService.PrintSettings.PrinterName = newValue;
        }

        

        partial void OnPSPrintSettingsChanged(PrintSettings? oldValue, PrintSettings? newValue)
        {
            if (newValue == null)
            {
                _printService.PrintSettings = PrintSettings.Default();
            }
            else
            {
                _printService.PrintSettings = newValue;
            }
        }

        private async void LoadAccounts()
        {
            var storedAccounts = await _accountStorageService.LoadAccountsAsync();
            storedAccounts.ForEach(account => Accounts.Add(account));
        }

        [RelayCommand]
        public async Task PSPaperSizesRefresh()
        {
            var ps = new System.Drawing.Printing.PrinterSettings();
            
        }

        public bool ValidateAll()
        {
            ValidateAllProperties();
            return !HasErrors;
        }


        [RelayCommand]
        public async Task ReloadPrintersList()
        {
            PrintersList.Clear();
            _printService.ListPrinters().ForEach(item => PrintersList.Add(item));
        }

        [RelayCommand]
        public async Task SelectTemplateFile()
        {
            TemplatePath = await PickFileAsync() ?? string.Empty;
        }

        [RelayCommand]
        public async Task OpenTemplateEditor()
        {
            var editor = _serviceProvider.GetRequiredService<TemplateEditorPage>();
            editor.Disappearing += (_, _) =>
            {
                TemplatePath = _appSettings.TemplatePath;
                var savedPrint = _appSettings.PrintSettings;
                if (savedPrint == null) return;
                PSPrintSettings = savedPrint;
                SelectedPrinter = savedPrint.PrinterName;
                PSSelectedPaper = savedPrint.PaperSize;
                PSMarginLeft = savedPrint.Margins.Left;
                PSMarginRight = savedPrint.Margins.Right;
                PSMarginTop = savedPrint.Margins.Top;
                PSMarginBottom = savedPrint.Margins.Bottom;
            };
            await Shell.Current.Navigation.PushModalAsync(editor);
        }

        async Task<string?> PickFileAsync()
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Выберите файл",
                    FileTypes = FilePickerFileType.Images
                });

                if (result == null)
                    return null; // пользователь отменил
                return result.FullPath;
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выборе файла шаблона:");
            }
            return null;
        }


        [RelayCommand]
        public async Task ReloadaccountsList()
        {
            string accessToken = _appSettings.AuthToken;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                await Shell.Current.DisplayAlertAsync("Памылка", "Вы не аўтарызаваныя. Калі ласка, аўтарызуйцеся перад працягам працы.", "Добра");
                return;
            }
            try
            {
                var accounts = await _authService.GetAccounts(accessToken);

                if (accounts == null || accounts.Count == 0)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Акаўнты не знойдзены",
                        "Facebook не вярнуў даступныя старонкі. Паўтарыце аўтарызацыю і дазвольце доступ да старонак і акаўнта Instagram.",
                        "Добра");
                    return;
                }
                await _accountStorageService.SaveAccountsAsync(accounts);
                Accounts.Clear();
                accounts.ForEach(a => Accounts.Add(a));
                SelectedAccount = accounts.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении списка аккаунтов");
                await Shell.Current.DisplayAlertAsync("Памылка", "Не ўдалося абнавіць спіс акаўнтаў. Паўтарыце аўтарызацыю.", "Добра");
            }
        }

        [RelayCommand]
        public async Task SaveSettings()
        {
            if (!ValidateAll())
            {
                string errors = string.Join(", ", GetInvalidProperties());
                await Shell.Current.DisplayAlertAsync("Памылка", $"Калі ласка, правільна запоўніце ўсе палі\n{errors}", "Добра");
                return;
            }

            try
            {
                if (PSPrintSettings == null)
                    PSPrintSettings = PrintSettings.Default();
                
                PSPrintSettings.PaperSize = PSSelectedPaper;
                PSPrintSettings.Margins = new(PSMarginLeft, PSMarginRight, PSMarginTop, PSMarginBottom);
                PSPrintSettings.PrinterName = SelectedPrinter;


                //PSPrintSettings.PaperKind = PaperKind.
                _appSettings.PrintSettings = PSPrintSettings ?? PrintSettings.Default();
                _appSettings.AppId = AppId;
                _appSettings.SelectedPrinter = SelectedPrinter;
                _appSettings.AppSecret = AppSecret;
                //_appSettings.SelectedHashtag = SelectedHashtag;
                _appSettings.RedirectUrl = RedirectUrl;
                _appSettings.SelectedAccount = SelectedAccount;
                // The layout editor owns the template. Do not overwrite its saved path
                // with the stale AdminPage value after the editor has been closed.
                TemplatePath = _appSettings.TemplatePath;
                _appSettings.HeaderValue = HeaderValue;
                _appSettings.DescriptionValue = DescriptionValue;
                _appSettings.ClientPassword = ClientPassword;
                _appSettings.OpenAiApiKey = OpenAiApiKey;

                _appSettings.SelectedPageId = await _authService.GetPageId(SelectedAccount.Id,
                        _appSettings.AuthToken) ?? string.Empty;
                _appSettings.InstaViewerRequestDelay = InstaViewerRequestDelay;
                var normalizedHashtag = SelectedHashtag.Trim().TrimStart('#');
                if (string.IsNullOrWhiteSpace(normalizedHashtag))
                    throw new ValidationException("Хэштэг не можа быць пустым");

                var savedHashtag = (_appSettings.SelectedHashtag ?? string.Empty).Trim().TrimStart('#');
                if (!string.Equals(normalizedHashtag, savedHashtag, StringComparison.OrdinalIgnoreCase))
                {
                    var selectedHashtagId = await _instaViewerService.GetHashtagId(
                        normalizedHashtag,
                        _appSettings.SelectedPageId,
                        _appSettings.AuthToken);
                    _logger.LogDebug("Атрыманы ID хэштэга: {HashtagId}", selectedHashtagId);
                    if (string.IsNullOrWhiteSpace(selectedHashtagId))
                        throw new InvalidOperationException("Instagram не вярнуў ID пазначанага хэштэга");

                    _appSettings.SelectedHashtag = normalizedHashtag;
                    _appSettings.SelectedHashtagId = selectedHashtagId;
                    await _instaMediaStoreService.ClearMediaElements();
                    _imagesService.ClearQueue();
                    _imagesService.ClearImages();
                    WeakReferenceMessenger.Default.Send(new ClearClientMediaMessage("Хэштэг зменены"));
                    await _instaViewerService.GetLastMedia();
                    await _imagesService.RefreshFromDB();
                    WeakReferenceMessenger.Default.Send(new FetchLatestMediaMessage("Хэштэг зменены"));
                }

                await Task.CompletedTask;
                _logger.LogDebug("Сохранены настройки");
                var toast = Toast.Make("Налады паспяхова захаваны", ToastDuration.Short, 1);
                await toast.Show();

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Произошла ошибка при сохранении настроек");
                var toast = Toast.Make("Падчас захавання налад адбылася памылка", ToastDuration.Long, 1);
                await toast.Show();

                await Shell.Current.DisplayAlertAsync("Памылка захавання", e.Message, "Добра");
            }
        }

        private bool _isOpeningClientWindow;

        [RelayCommand]
        public async Task OpenClientWindow()
        {
            if (_isOpeningClientWindow)
                return;

            _isOpeningClientWindow = true;
            try
            {
                var existingWindow = Application.Current?.Windows
                    .FirstOrDefault(window => window.Page is ClientPage.Views.ClientPage);

                if (existingWindow?.Handler?.PlatformView is MauiWinUIWindow existingNativeWindow)
                {
                    existingNativeWindow.AppWindow.Show();
                    Application.Current?.ActivateWindow(existingWindow);
                    return;
                }

                var clientPage = _serviceProvider.GetService<ClientPage.Views.ClientPage>();
                if (clientPage == null)
                    return;

                var newWindow = new Window(clientPage)
                {
                    Title = "Кліенцкае акно",
                    Width = 1600,
                    Height = 900
                };
                Application.Current?.OpenWindow(newWindow);
                await Task.Delay(500);
            }
            finally
            {
                _isOpeningClientWindow = false;
            }
        }

        [RelayCommand]
        public async Task CheckPassword()
        {
            if (ClientPassword == _appSettings.ClientPassword)
            {
                var toast = Toast.Make("Пароль правільны", ToastDuration.Long, 1);
                await toast.Show();
            }
            else
            {
                var toast = Toast.Make("Пароль няправільны", ToastDuration.Long, 1);
                await toast.Show();
            }
        }



        [RelayCommand]
        public async Task OpenAuthUrl()
        {
            try
            {
                var appId = AppId;
                var appSecret = AppSecret;
                var redirectUrl = RedirectUrl;

                string authToken = await _authService.Login(appId, appSecret, redirectUrl);
                if (string.IsNullOrWhiteSpace(authToken))
                    throw new InvalidOperationException("Facebook не вярнуў токен доступу.");

                _appSettings.AuthToken = authToken;
                await ReloadaccountsList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка авторизации Facebook");
                await Shell.Current.DisplayAlertAsync("Памылка аўтарызацыі", ex.Message, "Добра");
            }
        }

        [RelayCommand]
        public async Task ShowPopup()
        {
            var result = await _popupService.ShowPopupAsync<PasswordPopupViewModel,string>(Shell.Current);
            
            _logger.LogDebug($"{result.Result} {nameof(result)}");
        }

        

        public IEnumerable<string> GetInvalidProperties()
        {
            var invalid = new List<string>();
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var errors = GetErrors(prop.Name);
                if (errors is IEnumerable<string> errorList && errorList.Any())
                {
                    invalid.Add(prop.Name);
                }
            }

            return invalid;
        }

    }
}
