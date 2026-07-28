using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Skarynagram.Features.AppSettingsService.Repositories;
using Skarynagram.Features.AppSettingsService.Services;
using Skarynagram.Features.InstaViewer.Entities;
using Skarynagram.Features.InstaViewer.Messages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skarynagram.Features.InstaViewer.Services
{
    /// <summary>
    /// Класс для взаимодействия с Instagram Graph API для получения медиа по хэштегам.
    /// </summary>
    public class InstaViewerService
    {
        private ILogger<InstaViewerService> _logger;
        private IInstaMediaStoreService _instaMediaStoreService;
        private AppSettings _appSettings;
        private HttpClient _httpClient;
        public AppUsage? AppUsage { get; private set; }
        private CancellationTokenSource _cts = new();
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(3);
        private bool _started = false;
        public InstaViewerService(ILogger<InstaViewerService> logger, AppSettings appSettings, IInstaMediaStoreService instaMediaStoreService, HttpClient httpClient)
        {
            _logger = logger;
            _appSettings = appSettings;
            _instaMediaStoreService = instaMediaStoreService;
            _httpClient = httpClient;
            _appSettings.InstaViewerRequestDelay = (int)RefreshInterval.TotalMilliseconds;
            // A new program session refreshes immediately, then every three minutes.
            _appSettings.NextRequest = DateTimeOffset.Now;
        }

        public void Start()
        {
            if (_started)
                return;

            Task.Run(async () =>
            {
                _started = true;
                while (!_cts.Token.IsCancellationRequested)
                {
                    var nextRequest = _appSettings.NextRequest;
                    if (DateTimeOffset.Now >= nextRequest)
                    {

                        _logger.LogDebug($"Время с последнего запроса: {(DateTimeOffset.Now - _appSettings.LastRequest).TotalMinutes} мин.");
                        await GetLastMedia();
                        WeakReferenceMessenger.Default.Send(new FetchLatestMediaMessage("Запрос новых медиа"));
                        var requestCompletedAt = DateTimeOffset.Now;
                        _appSettings.LastRequest = requestCompletedAt;
                        _appSettings.NextRequest = requestCompletedAt.Add(RefreshInterval);
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(1000), _cts.Token);
                }
            });
        }


        public void Stop() 
        { 
            _cts.Cancel();
            _started = false;
        }

        /// <summary>
        /// Метод для получения последних медиа по выбранному хэштегу и сохранения их в хранилище.
        /// </summary>
        /// <returns></returns>
        public async Task GetLastMedia()
        {
            var selectedHashtagId = _appSettings.SelectedHashtagId;
            var selectedPageId = _appSettings.SelectedPageId;
            var authToken = _appSettings.AuthToken;
            if (!string.IsNullOrWhiteSpace(selectedHashtagId) &&
                !string.IsNullOrWhiteSpace(selectedPageId) &&
                !string.IsNullOrWhiteSpace(authToken))
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("Запрашиваем последние медиа");
                var mediaSearchResponse = await GetRecentMediaByHashtagId(
                    _appSettings.SelectedHashtagId,
                    _appSettings.SelectedPageId,
                    _appSettings.AuthToken,
                    9
                    );
                sw.Stop();
                _logger.LogDebug($"Время запроса последних медиа {sw.ElapsedMilliseconds} мс ({TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds).TotalSeconds} сек)");
                if (mediaSearchResponse == null || mediaSearchResponse.Data == null)
                {
                    _logger.LogWarning("Не получили последние медиа");

                }
                else
                {
                    const int targetImageCount = 9;
                    const int maxPages = 20;
                    var collectedImages = new List<MediaElement>();
                    var collectedIds = new HashSet<string>();
                    var currentPage = mediaSearchResponse;
                    var pageNumber = 1;

                    while (currentPage != null && pageNumber <= maxPages)
                    {
                        MediaSelection.AppendPrintable(collectedImages, collectedIds, currentPage.Data, targetImageCount);

                        if (collectedImages.Count >= targetImageCount ||
                            string.IsNullOrWhiteSpace(currentPage.Paging?.Next))
                            break;

                        currentPage = await GetRecentMediaNext(currentPage.Paging.Next);
                        pageNumber++;
                    }

                    // recent_media can contain fewer than nine printable images.
                    // Fill the remainder from top_media for the same hashtag.
                    if (collectedImages.Count < targetImageCount)
                    {
                        var topPage = await GetTopMediaByHashtagId(
                            selectedHashtagId,
                            selectedPageId,
                            authToken,
                            targetImageCount);
                        var topPageNumber = 1;

                        while (topPage != null && topPageNumber <= maxPages)
                        {
                            MediaSelection.AppendPrintable(collectedImages, collectedIds, topPage.Data, targetImageCount);

                            if (collectedImages.Count >= targetImageCount ||
                                string.IsNullOrWhiteSpace(topPage.Paging?.Next))
                                break;

                            topPage = await GetRecentMediaNext(topPage.Paging.Next);
                            topPageNumber++;
                        }

                        _logger.LogInformation(
                            "После добора из top_media собрано {ImageCount} изображений",
                            collectedImages.Count);
                    }

                    _logger.LogInformation(
                        "Собрано {ImageCount} изображений по хэштегу с {PageCount} страниц API",
                        collectedImages.Count,
                        pageNumber);
                    await _instaMediaStoreService.AddMediaElements(collectedImages.Take(targetImageCount));
                }
            }
            else
            {
                _logger.LogError($"ID хэштега, ID страницы или Токен равны null. Проверьте настройки");
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="hashtag"></param>
        /// <param name="pageId">Используй IAuthService.GetPageId()</param>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        public async Task<string?> GetHashtagId(string hashtag, string pageId, string accessToken)
        {
            try
            {
                hashtag = hashtag.Trim().TrimStart('#');
                var url = $"https://graph.facebook.com/v24.0/ig_hashtag_search?user_id={pageId}" + 
                    $"&q={hashtag}&" + 
                    $"access_token={accessToken}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var json = response.Content.ReadAsStringAsync().Result;
                using var doc = JsonDocument.Parse(json);
                var hashtagId = doc.RootElement.GetProperty("data")[0].GetProperty("id").GetString();
                return hashtagId;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении ID хэштега");
            }
            return string.Empty;
        }

        public async Task<MediaSearchResponse> GetRecentMediaByHashtagId(string hashtagId, string pageId, string accessToken, int perPage = 9)
        {
            try
            {
                var url = $"https://graph.facebook.com/v24.0/{hashtagId}/recent_media?user_id={pageId}" +
                    $"&fields=id,caption,media_type,media_url,permalink,timestamp" +
                    $"&access_token={accessToken}" +
                    $"&limit={perPage}";

                var response = await _httpClient.GetAsync(url);
                AppUsage = await LogAppUsageHeader(response.Headers);

                response.EnsureSuccessStatusCode();
                var json = response.Content.ReadAsStringAsync().Result;
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,

                };
                var mediaList = JsonSerializer.Deserialize<MediaSearchResponse>(json, options) ?? new MediaSearchResponse(new List<MediaElement>(), new PagingInfo(Cursors.Empty, string.Empty)); ;
                return mediaList;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении медиа по хэштегу");
            }
            return new MediaSearchResponse(new List<MediaElement>(), new PagingInfo(Cursors.Empty, string.Empty));
        }

        public async Task<MediaSearchResponse> GetTopMediaByHashtagId(string hashtagId, string pageId, string accessToken, int perPage = 9)
        {
            try
            {
                var url = $"https://graph.facebook.com/v24.0/{hashtagId}/top_media?user_id={pageId}" +
                    $"&fields=id,caption,media_type,media_url,permalink,timestamp" +
                    $"&access_token={accessToken}" +
                    $"&limit={perPage}";

                var response = await _httpClient.GetAsync(url);
                AppUsage = await LogAppUsageHeader(response.Headers);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                };
                return JsonSerializer.Deserialize<MediaSearchResponse>(json, options) ??
                    new MediaSearchResponse(new List<MediaElement>(), new PagingInfo(Cursors.Empty, string.Empty));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении top media по хэштегу");
                return new MediaSearchResponse(new List<MediaElement>(), new PagingInfo(Cursors.Empty, string.Empty));
            }
        }

        public async Task<MediaSearchResponse> GetRecentMediaNext(string nextUrl)
        {
            //var http = new HttpClient();
            try
            {
                var response = await _httpClient.GetAsync(nextUrl);
                AppUsage = await LogAppUsageHeader(response.Headers);
                response.EnsureSuccessStatusCode();
                var json = response.Content.ReadAsStringAsync().Result;
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,

                };
                var mediaList = JsonSerializer.Deserialize<MediaSearchResponse>(json, options) ??
                    new MediaSearchResponse(
                        new List<MediaElement>(),
                        new PagingInfo(Cursors.Empty, string.Empty)
                        );
                return mediaList;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении следующей страницы медиа");
            }

            return new MediaSearchResponse(new List<MediaElement>(), new PagingInfo(Cursors.Empty, string.Empty));
        }

        private async Task LogHeader(HttpResponseHeaders headers, string headerKey)
        {
            if (headers.TryGetValues(headerKey, out var values))
                _logger.LogDebug("{HeaderKey} = {HeaderValue}", headerKey, values.FirstOrDefault());
        }

        private async Task<AppUsage> LogAppUsageHeader(HttpResponseHeaders headers)
        {
            var headerKey = "x-app-usage";
            // Meta does not guarantee this diagnostic header on every successful
            // Graph API response. Its absence must never discard the response body.
            if (!headers.TryGetValues(headerKey, out var values))
            {
                _logger.LogDebug("Заголовок {HeaderKey} отсутствует; продолжаем обработку ответа", headerKey);
                return new AppUsage();
            }
            var value = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(value))
                return new AppUsage();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            };
            var appUsage = JsonSerializer.Deserialize<AppUsage>(value, options);
            await LogHeader(headers, headerKey);
            return appUsage ?? new AppUsage();
        }
    }
}
