using Skarynagram.Features.AppSettingsService.Services;
using Skarynagram.Features.AuthService.Models;
using Skarynagram.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace Skarynagram.Features.AuthService.Services
{
    public class AuthService : IAuthService
    {
        private const string GraphApiVersion = "v24.0";
        private ILogger _logger;
        private HttpClient _httpClient;

        public AuthService(ILogger<AuthService> logger, HttpClient httpClient) 
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Redirect url: http://localhost:9876/
        /// </summary>
        /// <returns></returns>
        private static async Task OpenLoginPage(string appId, string redirectUrl)
        {
            var authUrl = $"https://www.facebook.com/{GraphApiVersion}/dialog/oauth?" +
             $"client_id={Uri.EscapeDataString(appId)}" +
              $"&redirect_uri={Uri.EscapeDataString(redirectUrl)}" +
              "&response_type=code" +
              "&scope=instagram_basic,pages_show_list,pages_read_engagement,business_management";

            await Launcher.Default.OpenAsync(new Uri(authUrl));
        }

        /// <summary>
        /// Возвращает короткоживущий токен
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="redirectUrl"></param>
        /// <param name="appSecret"></param>
        /// <param name="code"></param>
        /// <returns></returns>
        private async Task<string?> ExchangeCodeForTokenAsync(string appId, string redirectUrl, string appSecret, string code)
        {
            

            //using var http = new HttpClient();
            try
            {
                var url = $"https://graph.facebook.com/{GraphApiVersion}/oauth/access_token" +
                          $"?client_id={Uri.EscapeDataString(appId)}" +
                          $"&redirect_uri={Uri.EscapeDataString(redirectUrl)}" +
                          $"&client_secret={Uri.EscapeDataString(appSecret)}" +
                          $"&code={Uri.EscapeDataString(code)}";

                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(GetFacebookError(json, response.StatusCode));

                // Ответ вида:
                // { "access_token":"...", "token_type":"bearer", "expires_in":5183944 }
                using var doc = JsonDocument.Parse(json);
                var token = doc.RootElement.GetProperty("access_token").GetString();
            
                return token;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обмене code на токен");
                throw;
            }
        }

        /// <summary>
        /// Обменивает короткоживущий токен на длительный
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="appSecret"></param>
        /// <param name="shortLivedAccessToken"></param>
        /// <returns>Token or <see cref="string.Empty"/></returns>
        private async Task<string> ExchangeShortLivedAccessToken(string appId, string appSecret, string shortLivedAccessToken)
        {
            //using var http = new HttpClient();
            try
            {
                var url = $"https://graph.facebook.com/{GraphApiVersion}/oauth/access_token" +
                    $"?grant_type=fb_exchange_token" +
                    $"&client_id={Uri.EscapeDataString(appId)}" +
                    $"&client_secret={Uri.EscapeDataString(appSecret)}" +
                    $"&fb_exchange_token={Uri.EscapeDataString(shortLivedAccessToken)}";
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(GetFacebookError(json, response.StatusCode));

                using var doc = JsonDocument.Parse(json);
                var token = doc.RootElement.GetProperty("access_token").GetString();
            
                return token ?? string.Empty;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении instagramBusinessAccoundId");
                throw;
            }

        }

        public async Task<string?> GetPageId(string accountId, string accessToken)
        {
            //using var http = new HttpClient();
            try
            {
                //TODO: https://copilot.microsoft.com/shares/LmDzuKR7d1qUqFuCpTwPJ
                var url = $"https://graph.facebook.com/v24.0/{accountId}?" +
                    "fields=instagram_business_account&" +
                    $"access_token={accessToken}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                string? instagramBusinessAccoundId = doc.RootElement.GetProperty("instagram_business_account").GetProperty("id").GetString();
                return instagramBusinessAccoundId;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении instagramBusinessAccoundId");
            }
            return null;
            
        }

        private static async Task<string> WaitCodeInRedirect(string redirectUrl)
        {
            var redirectUri = new Uri(redirectUrl);
            if (!redirectUri.IsLoopback)
                throw new InvalidOperationException("Адрас перанакіравання павінен весці на localhost.");

            var listenerPrefix = redirectUri.GetLeftPart(UriPartial.Path);
            if (!listenerPrefix.EndsWith('/'))
                listenerPrefix += "/";

            using var listener = new HttpListener();
            listener.Prefixes.Add(listenerPrefix);
            listener.Start();

            var context = await listener.GetContextAsync();
            var response = context.Response;
            var callbackError = context.Request.QueryString["error_message"]
                ?? context.Request.QueryString["error_description"]
                ?? context.Request.QueryString["error"];
            var callbackCode = context.Request.QueryString["code"];
            string responseString = string.IsNullOrWhiteSpace(callbackError) && !string.IsNullOrWhiteSpace(callbackCode)
                ? "<html><body>Login successful. You can close this window.</body></html>"
                : "<html><body>Login failed. Return to Skarynagram for details.</body></html>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();

            listener.Stop();

            if (!string.IsNullOrWhiteSpace(callbackError))
                throw new InvalidOperationException($"Facebook адхіліў аўтарызацыю: {callbackError}");
            if (string.IsNullOrWhiteSpace(callbackCode))
                throw new InvalidOperationException("Facebook не вярнуў код аўтарызацыі.");

            return callbackCode;
            
        }

        public async Task<List<Account>?> GetAccounts(string accessToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(accessToken))
                    return null;

                string url = "https://graph.facebook.com/v24.0/me/accounts?" +
                    "fields=id,name,category,access_token,instagram_business_account&" +
                    $"access_token={Uri.EscapeDataString(accessToken)}";
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                string json = await response.Content.ReadAsStringAsync();

                GetAccountsResponse getAccountsResponse  = JsonSerializer.Deserialize<GetAccountsResponse>(json, options) ?? GetAccountsResponse.Empty;
                _logger.LogInformation($"Получен список аккаунтов: {getAccountsResponse.Data.Count()}");
                foreach (var item in getAccountsResponse.Data)
                {
                    _logger.LogDebug($"{item.Id}: {item.Name}");
                }
                return getAccountsResponse.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запросе списка аккаунтов");
            }

            return null;
        }

        public async Task<string> Login(string appId, string appSecret, string redirectUrl)
        {
            appId = appId?.Trim() ?? string.Empty;
            appSecret = appSecret?.Trim() ?? string.Empty;
            redirectUrl = redirectUrl?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
                throw new InvalidOperationException("Укажыце ідэнтыфікатар і сакрэт праграмы Facebook.");
            if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException("Адрас перанакіравання мае няправільны фармат.");

            var codeTask = AuthService.WaitCodeInRedirect(redirectUrl);
            await AuthService.OpenLoginPage(appId, redirectUrl);
            string code = await codeTask;
            if (code != null)
            {
                _logger.LogDebug($"Обмениваем code {StringSanitizer.MaskToken(code)} на короткоживущий токен");
                
                string? shortLivedToken = await ExchangeCodeForTokenAsync(appId, redirectUrl, appSecret, code);
                if (shortLivedToken == null)
                {
                    _logger.LogError("Не получили короткоживущик токен, он null");
                    return string.Empty;
                }
                _logger.LogInformation($"Обмениваем короткоживущий токен {StringSanitizer.MaskToken(shortLivedToken)} на долгоживущий");
                string longLivedToken = await ExchangeShortLivedAccessToken(appId, appSecret, shortLivedToken);
                if (longLivedToken == null)
                {
                    _logger.LogError("Не получили долгоживущий токен, он null");
                    return string.Empty;
                }
                _logger.LogInformation($"Успешно получен долгоживущий токен: {StringSanitizer.MaskToken(longLivedToken)}");
                return longLivedToken;
            }
            
            return string.Empty;
        }

        private static string? ExtractCode(string fragment)
        {
            if (string.IsNullOrEmpty(fragment)) return null;
            var parts = fragment.Split('&');
            foreach (var part in parts)
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && kv[0].Trim('?') == "code")
                    return kv[1];
            }
            return null;
        }

        private static string GetFacebookError(string json, HttpStatusCode statusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    var message = error.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString()
                        : null;
                    var code = error.TryGetProperty("code", out var codeElement)
                        ? codeElement.ToString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(message))
                        return string.IsNullOrWhiteSpace(code)
                            ? $"Facebook: {message}"
                            : $"Facebook ({code}): {message}";
                }
            }
            catch (JsonException)
            {
                // Fall through to a status-only message; never expose request credentials.
            }
            return $"Facebook вярнуў памылку HTTP {(int)statusCode}.";
        }

    }
}
