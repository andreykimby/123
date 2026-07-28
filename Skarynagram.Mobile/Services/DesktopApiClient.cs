using System.Net;
using System.Net.Http.Json;
using Skarynagram.Mobile.Models;

namespace Skarynagram.Mobile.Services;

public sealed class DesktopApiClient(HttpClient httpClient)
{
    private Uri? _baseAddress;
    private string _pin = string.Empty;

    public void Configure(string address, string pin)
    {
        address = address.Trim();
        if (!address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            address = "http://" + address;
        if (!address.EndsWith('/'))
            address += "/";

        _baseAddress = new Uri(address);
        _pin = pin.Trim();
    }

    public Task<RemoteStatus?> GetStatusAsync(CancellationToken cancellationToken = default) =>
        SendAsync<RemoteStatus>(HttpMethod.Get, "api/status", null, cancellationToken);

    public Task<PrintOptions?> GetPrintOptionsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<PrintOptions>(HttpMethod.Get, "api/print-options", null, cancellationToken);

    public Task<PrintOptions?> UpdatePrintOptionsAsync(
        string printer,
        string paper,
        CancellationToken cancellationToken = default) =>
        SendAsync<PrintOptions>(
            HttpMethod.Put,
            "api/print-options",
            JsonContent.Create(new PrintOptionsUpdate(printer, paper)),
            cancellationToken);

    private async Task<T?> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (_baseAddress == null)
            throw new InvalidOperationException("Спачатку ўкажыце адрас камп'ютара.");

        var attempts = method == HttpMethod.Get ? 2 : 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var request = new HttpRequestMessage(method, new Uri(_baseAddress, relativePath))
            {
                Content = content,
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };
            request.Headers.ConnectionClose = true;
            request.Headers.Add("X-Skarynagram-Pin", _pin);

            try
            {
                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseContentRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            }
            catch (HttpRequestException) when (attempt < attempts)
            {
                await Task.Delay(200, cancellationToken);
            }
        }

        throw new HttpRequestException("The computer closed the response before it was fully received.");
    }
}
