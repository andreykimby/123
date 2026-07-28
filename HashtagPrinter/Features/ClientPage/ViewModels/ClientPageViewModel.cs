using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Skarynagram.Features.AppSettingsService.Services;
using Skarynagram.Features.InstaViewer.Entities;
using Skarynagram.Features.InstaViewer.Messages;
using Skarynagram.Features.InstaViewer.Services;
using Skarynagram.Features.ModalNavigationService;
using Skarynagram.Features.Services;
using Skarynagram.Features.Views;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
//using Microsoft.UI.Windowing;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using Utils.PasswordPopup;
using WinRT.Interop;

namespace Skarynagram.Features.ViewModels
{
    public partial class ClientPageViewModel : ObservableRecipient
    {
        private ILogger<ClientPageViewModel> _logger;
        private readonly IPopupService _popupService;
        private IInstaMediaStoreService _instaMediaStoreService;
        private readonly IServiceProvider _serviceProvider;
        private InstaViewerService _instaViewerService;
        private AppSettings _appSettings;
        [ObservableProperty]
        public partial ObservableCollection<MediaElement> Images { get; set; } = new();

        [ObservableProperty]
        public partial int MediaPerPage { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; } = false;

        private MediaSearchResponse? _mediaSearchResponse;
        [ObservableProperty]
        public partial AppUsage AppUsage { get; set; } = AppUsage.Empty;

        [ObservableProperty]
        public partial bool IsCooldown { get; set; } = false;

        [ObservableProperty]
        public partial double ProgressBarProgress { get; set; } = 1;

        [ObservableProperty]
        public partial bool ShowPrintForm { get; set; } = false;

        [ObservableProperty]
        public partial MediaElement? SelectedMedia { get; set; }

        [ObservableProperty]
        public partial bool RefreshIsEnabled { get; set; } = true;

        [ObservableProperty]
        public partial int MediaPage { get; set; } = 0;

        [ObservableProperty]
        public partial bool IsClosing { get; set; } = false;

        partial void OnIsClosingChanged(bool value)
        {
            OnPropertyChanged(nameof(HideClosingBtn));
        }

        [ObservableProperty]
        public partial string EnteredPassword { get; set; } = string.Empty;
        [ObservableProperty]
        public partial string PasswordError { get; set; } = string.Empty;
        public bool HideClosingBtn { get => !IsClosing; }
        public string HeaderValue { get; private set; } = "Header";
        public string DescriptionValue { get; private set; } = "Description";

        public Window? Window { get; private set; }
        public event Action? RequestCloseClientWindow;
        public View? View { get; set; }
        private readonly IModalNavigationService _modalService;
        public void SetWindow(Window window) => Window = window;
        
        private readonly ImagesService _imagesService;

        private CancellationTokenSource _cts = new();
        public ClientPageViewModel(IServiceProvider serviceProvider, IPopupService popupService, IModalNavigationService modalService,  ILogger<ClientPageViewModel> logger, InstaViewerService instaViewerService, IInstaMediaStoreService instaMediaStoreService, ImagesService imagesService, AppSettings appSettings)
        {
            var sw = Stopwatch.StartNew();
            _logger = logger;
            _popupService = popupService;
            _serviceProvider = serviceProvider;
            _imagesService = imagesService;
            _instaMediaStoreService = instaMediaStoreService;
            MediaPerPage = 21;
            _appSettings = appSettings;
            _instaViewerService = instaViewerService;
            HeaderValue = _appSettings.HeaderValue;
            DescriptionValue = _appSettings.DescriptionValue;
            _modalService = modalService;
            WeakReferenceMessenger.Default.Register<FetchLatestMediaMessage>(this,
                (r, m) => _ = RefreshClientImagesAsync());
            WeakReferenceMessenger.Default.Register<ClearClientMediaMessage>(this,
                (r, m) => MainThread.BeginInvokeOnMainThread(() => Images.Clear()));
            sw.Stop();
            _logger.LogDebug($"ClientPageViewModel инициализирован за {sw.ElapsedMilliseconds} мс");

            _ = StartBackgroundUpdateAsync();
        }

        private async Task RefreshClientImagesAsync()
        {
            try
            {
                IsBusy = true;
                _logger.LogDebug("Обновление клиентских изображений из базы данных");
                await _imagesService.RefreshFromDB();
                var snapshot = _imagesService.GetSnapshot();
                await MainThread.InvokeOnMainThreadAsync(() => ReplaceAll(snapshot));
            }
            finally
            {
                IsBusy = false;
            }
        }

        //private void OnImageCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        //{
        //    OnPropertyChanged(nameof(Images));
        //}

        [RelayCommand]
        public async Task CheckPassword()
        {
            if (EnteredPassword == _appSettings.ClientPassword)
            {
                EnteredPassword = string.Empty;
                PasswordError = string.Empty;
                IsClosing = false;
                RequestCloseClientWindow?.Invoke();
            } else
            {
                EnteredPassword = string.Empty;
                PasswordError = "Няправільны пароль";
                IsClosing = true;
            }
        }


        [RelayCommand]
        public async Task RefreshRecentMedia()
        {
            await LoadImages();
        }

        [RelayCommand]
        public async Task Close()
        {
            EnteredPassword = string.Empty;
            PasswordError = string.Empty;
            IsClosing = !IsClosing;
        }

        [RelayCommand]
        private void AppendPasswordDigit(string digit)
        {
            if (digit.Length == 1 && char.IsDigit(digit[0]) && EnteredPassword.Length < 20)
                EnteredPassword += digit;
        }

        [RelayCommand]
        private void RemovePasswordDigit()
        {
            if (EnteredPassword.Length > 0)
                EnteredPassword = EnteredPassword[..^1];
        }

        [RelayCommand]
        public async Task LoadNextPage()
        {
            Debug.WriteLine("Next page");
            var successLoaded = await _imagesService.LoadNexPage();
            
            await Task.Delay(1000);
        }

        //public void OnCollectionScrolled(ItemsViewScrolledEventArgs args)
        //{
        //    //if (IsLoading || !HasMorePages) return;

        //    // Логика та же, что выше
        //    if (args.LastVisibleItemIndex >= Images.Count - 10)
        //    {
        //        _ = LoadNextPage();  // _ = чтобы не предупреждало про async
        //    }
        //}

        private async Task SyncImagesFromService()
        {
            var dispatcher = Dispatcher.GetForCurrentThread();
            if (dispatcher != null)
            {
                dispatcher.StartTimer(
                    TimeSpan.FromMilliseconds(500),
                    () =>
                    {
                        // Fire and forget the async call, handle exceptions as needed
                        _ = SyncFromService();
                        return true;
                    });
            }
        }

        public async Task StartBackgroundUpdateAsync()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Фоновая тяжёлая работа
                    
                    
                    // Переключаемся в UI-поток один раз на итерацию
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Здесь можно использовать разные стратегии:
                        // Вариант А — полная замена (самый простой)
                        _ = SyncFromService();

                        // Вариант Б — умная разница (меньше мерцания и быстрее при большом объёме)
                        // UpdateCollectionDiff(Items, newData);
                    });

                    await Task.Delay(2000, token); // или другой интервал
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // логирование
                    _logger.LogError(ex, "Ошибка при обновлении данных в фоне");
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // показать ошибку пользователю
                    });
                }
            }
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        


        [RelayCommand]
        public async Task LoadImages()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                await _instaViewerService.GetLastMedia();
                await _imagesService.RefreshFromDB();
                await SyncFromService();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось загрузить изображения по хэштегу");
                await Shell.Current.DisplayAlertAsync("Памылка", "Не ўдалося загрузіць выявы. Праверце аўтарызацыю і налады хэштэга.", "Добра");
            }
            finally
            {
                IsBusy = false;
            }
            return;
            IsBusy = true;
            await LoadImagesFromStore(true);
            IsBusy = false;
            return;
            #region old code
#pragma warning disable CS0162 // Unreachable code detected
            IsBusy = IsCooldown = true;
#pragma warning restore CS0162 // Unreachable code detected
            ProgressBarProgress = 1;
            var stopwatch = Stopwatch.StartNew();
            if (_mediaSearchResponse != null && (_mediaSearchResponse.Paging != null && (_mediaSearchResponse.Paging.Next != string.Empty || _mediaSearchResponse.Paging.Next != null)))
            {
                _mediaSearchResponse = await _instaViewerService.GetRecentMediaNext(_mediaSearchResponse.Paging.Next);
            }
            else
            {
                _mediaSearchResponse = await _instaViewerService.GetRecentMediaByHashtagId(
                    _appSettings.SelectedHashtagId,
                    _appSettings.SelectedPageId,
                    _appSettings.AuthToken
                    );
            }
            stopwatch.Stop();

            AppUsage = _instaViewerService.AppUsage;
            int cooldownMs = (int)Math.Log(AppUsage.TotalTime + 1) * 10000;
            if (_mediaSearchResponse == null || _mediaSearchResponse.Data == null)
            {
                _logger.LogError("Не удалось загрузить медиа по хэштегу");
                IsBusy = false;
                return;
            }
            string[] allowedTypes = { "IMAGE", "CAROUSEL_ALBUM" };
            _mediaSearchResponse.Data.Where(me => allowedTypes.Contains(me.MediaType)).ToList().ForEach(
                me =>
                {
                    Images.Add(me);
                    _logger.LogDebug($"{me.Timestamp} - {me.Id}: {me.MediaType}: {me.Permalink}");
                });

            IsBusy = false;
            var delay = cooldownMs / 1000;
            for (int i = 1000; i > 0; i--)
            {
                await Task.Delay(delay);
                ProgressBarProgress = (double)(i - 1) / 1000;
            }
            //await Task.Delay(cooldownMs);
            IsCooldown = false;
            #endregion
        }

        private async Task SyncFromService()
        {
            IsBusy = true;
            var snapshot = _imagesService.GetSnapshot();
            await MainThread.InvokeOnMainThreadAsync(() => ReplaceAll(snapshot));
            IsBusy = false;
        }

        private void ReplaceAll(IReadOnlyList<MediaElement> snapshot)
        {
            bool equal = AreEqual(snapshot, Images);
            if (equal)
                return;
            Images.Clear();
            foreach (var item in snapshot)
                Images.Add(item);
        }

        bool AreEqual(
            IReadOnlyList<MediaElement> snapshot,
            ObservableCollection<MediaElement> ui)
        {
            if (snapshot.Count != ui.Count)
                return false;

            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].Id != ui[i].Id)
                    return false;
            }

            return true;
        }

        partial void OnIsClosingChanged(bool oldValue, bool newValue)
        {
            
            OnPropertyChanged(nameof(HideClosingBtn));
        }

        public async Task LoadImagesFromStore(bool refresh)
        {
            
            return;

            // Если мы хотим освежить список, то обнуляем номер страницы
            // для получения свежих данных из БД
            if (refresh)
            {
                MediaPage = 0;
            } else
            {
                MediaPage++;
            }

            var images = await _instaMediaStoreService.GetMediaElements(MediaPage);

            if (refresh)
            {
                Images.Clear();
                var tmpList = images.OrderBy(me => me.Timestamp).ToList();
                tmpList.ForEach(me => Images.Insert(0, me));
            }
            else
            {
                var tmpList = images.Where(im => !Images.Contains(im)).ToList();


                tmpList.ForEach(e => Images.Add(e));
                Images.OrderByDescending(e => e.Timestamp);
                
            }

            AppUsage = _instaViewerService.AppUsage ?? AppUsage.Empty;

        }



        [RelayCommand]
        public async Task HidePrintForm() => ShowPrintForm = false;

        [RelayCommand]
        public async Task MediaClick(MediaElement mediaElement)
        {
            //ShowPrintForm = true;
            SelectedMedia = mediaElement;


            var dialogVM = ActivatorUtilities.CreateInstance<PrintDialogViewModel>(_serviceProvider, mediaElement);
            var dialog = ActivatorUtilities.CreateInstance<PrintDialogView>(_serviceProvider, dialogVM);
            //var dialogVM = _serviceProvider.GetRequiredService<PrintDialogViewModel>();
            dialog.BindingContext = dialogVM;

            _logger.LogDebug($"Клик по медиа: {SelectedMedia.Id}: {SelectedMedia.Permalink}");
            dialogVM.MediaElement = SelectedMedia;
            dialog.BindingContext = dialogVM;
            _modalService.SetCurrentPage(dialog);
            if (Window == null)
            {
                _logger.LogError("Окно клиента не установлено, не могу показать диалог печати");
                return;
            }
            await Window.Navigation.PushModalAsync(dialog);
            //await Application.Current.MainPage.Navigation.PushModalAsync(dialog);
            //await Microsoft.UI.Xaml.Navigation.PushModalAsync(dialog);
            //await Shell.Current.GoToAsync(nameof(PrintDialogView), true);
            
        }
    }
}
