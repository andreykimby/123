using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Skarynagram.Features.AppSettingsService.Services;
using Skarynagram.Features.PrintStatistics;
using Skarynagram.Platforms.Windows.Features.PrintService.Service;

namespace Skarynagram.Features.RemoteControl;

public sealed class RemoteControlServer : IDisposable
{
    public const int Port = 9877;
    private readonly AppSettings _settings;
    private readonly IPrintService _printService;
    private readonly IPrintStatisticsService _statistics;
    private readonly ILogger<RemoteControlServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private TcpListener? _listener;
    private CancellationTokenSource? _cancellation;

    public RemoteControlServer(
        AppSettings settings,
        IPrintService printService,
        IPrintStatisticsService statistics,
        ILogger<RemoteControlServer> logger)
    {
        _settings = settings;
        _printService = printService;
        _statistics = statistics;
        _logger = logger;
    }

    public void Start()
    {
        if (_listener != null)
            return;

        try
        {
            _cancellation = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            _ = AcceptLoopAsync(_cancellation.Token);
            _logger.LogInformation("Мабільны пульт даступны на порце {Port}", Port);
        }
        catch (Exception exception)
        {
            _listener = null;
            _cancellation?.Dispose();
            _cancellation = null;
            _logger.LogError(exception, "Не атрымалася запусціць мабільны пульт");
        }
    }

    public void Stop()
    {
        _cancellation?.Cancel();
        _listener?.Stop();
        _listener = null;
        _cancellation?.Dispose();
        _cancellation = null;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Памылка мабільнага API");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            client.ReceiveTimeout = 8000;
            client.SendTimeout = 8000;
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true);

            var requestLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(requestLine))
                return;

            var requestParts = requestLine.Split(' ', 3);
            if (requestParts.Length < 2)
            {
                await WriteJsonAsync(stream, 400, new { error = "Няправільны запыт." }, cancellationToken);
                return;
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken)))
            {
                var separator = line.IndexOf(':');
                if (separator > 0)
                    headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }

            var method = requestParts[0].ToUpperInvariant();
            var path = requestParts[1].Split('?', 2)[0].TrimEnd('/');
            if (method == "GET" && string.IsNullOrEmpty(path))
            {
                await WriteTextAsync(stream, 200, "text/html; charset=utf-8", MobileControlPage.Html, cancellationToken);
                return;
            }
            if (method == "GET" && path == "/manifest.webmanifest")
            {
                await WriteTextAsync(stream, 200, "application/manifest+json; charset=utf-8", MobileControlPage.Manifest, cancellationToken);
                return;
            }

            if (!headers.TryGetValue("X-Skarynagram-Pin", out var pin)
                || !string.Equals(pin, _settings.ClientPassword ?? string.Empty, StringComparison.Ordinal))
            {
                await WriteJsonAsync(stream, 401, new { error = "Няправільны PIN-код." }, cancellationToken);
                return;
            }

            if (method == "GET" && path == "/api/status")
            {
                var snapshot = _statistics.GetSnapshot();
                var current = _settings.PrintSettings ?? _printService.PrintSettings;
                await WriteJsonAsync(stream, 200, new
                {
                    connected = true,
                    computerName = Environment.MachineName,
                    hashtag = (_settings.SelectedHashtag ?? string.Empty).TrimStart('#'),
                    printedToday = snapshot.DayCount,
                    printedTotal = snapshot.TotalCount,
                    selectedPrinter = current.PrinterName,
                    selectedPaper = PaperDisplay(current.PaperSize)
                }, cancellationToken);
                return;
            }

            if (method == "GET" && path == "/api/print-options")
            {
                await WritePrintOptionsAsync(stream, cancellationToken);
                return;
            }

            if (method == "PUT" && path == "/api/print-options")
            {
                var length = headers.TryGetValue("Content-Length", out var lengthText)
                    && int.TryParse(lengthText, out var parsedLength)
                    ? Math.Clamp(parsedLength, 0, 16_384)
                    : 0;
                var body = new char[length];
                var read = 0;
                while (read < length)
                {
                    var count = await reader.ReadAsync(body.AsMemory(read, length - read), cancellationToken);
                    if (count == 0) break;
                    read += count;
                }

                var update = JsonSerializer.Deserialize<PrintOptionsUpdate>(
                    new string(body, 0, read),
                    _jsonOptions);
                if (update == null || string.IsNullOrWhiteSpace(update.Printer) || string.IsNullOrWhiteSpace(update.Paper))
                {
                    await WriteJsonAsync(stream, 400, new { error = "Абярыце прынтар і паперу." }, cancellationToken);
                    return;
                }

                var printers = _printService.ListPrinters();
                if (!printers.Contains(update.Printer))
                {
                    await WriteJsonAsync(stream, 400, new { error = "Прынтар не знойдзены." }, cancellationToken);
                    return;
                }

                _printService.PrintSettings.PrinterName = update.Printer;
                var paper = _printService.GetPaperSizes().FirstOrDefault(value => PaperDisplay(value) == update.Paper);
                if (paper == null)
                {
                    await WriteJsonAsync(stream, 400, new { error = "Фармат паперы не знойдзены." }, cancellationToken);
                    return;
                }

                _printService.PrintSettings.PaperSize = paper;
                _settings.SelectedPrinter = update.Printer;
                _settings.PrintSettings = _printService.PrintSettings;
                await WritePrintOptionsAsync(stream, cancellationToken);
                return;
            }

            await WriteJsonAsync(stream, 404, new { error = "Каманда не знойдзена." }, cancellationToken);
        }
    }

    private Task WritePrintOptionsAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var current = _settings.PrintSettings ?? _printService.PrintSettings;
        _printService.PrintSettings = current;
        var printers = _printService.ListPrinters();
        var papers = _printService.GetPaperSizes().Select(PaperDisplay).ToList();
        return WriteJsonAsync(stream, 200, new
        {
            printers,
            papers,
            selectedPrinter = current.PrinterName,
            selectedPaper = PaperDisplay(current.PaperSize)
        }, cancellationToken);
    }

    private static string PaperDisplay(System.Drawing.Printing.PaperSize paper) =>
        $"{paper.PaperName} — {paper.Width / 100d:0.##} × {paper.Height / 100d:0.##} цалі";

    private async Task WriteJsonAsync(
        NetworkStream stream,
        int statusCode,
        object value,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        var body = Encoding.UTF8.GetBytes(json);
        var status = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            401 => "Unauthorized",
            404 => "Not Found",
            _ => "Error"
        };
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {statusCode} {status}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }

    private static async Task WriteTextAsync(
        NetworkStream stream,
        int statusCode,
        string contentType,
        string value,
        CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(value);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {statusCode} OK\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }

    public void Dispose() => Stop();

    private sealed record PrintOptionsUpdate(string Printer, string Paper);
}
