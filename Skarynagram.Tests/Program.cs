using Skarynagram.Features.AppSettingsService.Services;
using Skarynagram.Features.InstaViewer.Entities;
using Skarynagram.Features.InstaViewer.Services;
using Skarynagram.Features.PrintService.Service;
using Skarynagram.Features.PrintStatistics;
using Skarynagram.Features.ImageTransformation;
using System.Net;

var tests = new (string Name, Action Run)[]
{
    ("Загрузка recent + top без дублей", TestMediaSelection),
    ("Центрирование макета", TestLayoutGeometry),
    ("Физические размеры печати", TestPrintGeometry),
    ("Перанос старых налад", TestSettingsMigration)
    ,("Улік друку і паўторнае адкрыццё табліцы", TestPrintStatistics)
    ,("СКАРЫНАДРУК: запыт і чытанне выявы", TestHistoricalImageService)
    ,("СКАРЫНАДРУК: памылка API вяртаецца без аварыйнага завяршэння", TestHistoricalImageApiError)
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS: {test.Name}");
}

static void TestMediaSelection()
{
    var result = new List<MediaElement>();
    var ids = new HashSet<string>();
    MediaSelection.AppendPrintable(result, ids,
    [
        Media("1"), Media("2"), Media("video", "VIDEO"), Media("2")
    ], 9);
    MediaSelection.AppendPrintable(result, ids,
        Enumerable.Range(2, 12).Select(number => Media(number.ToString())), 9);
    Assert(result.Count == 9, $"чакалася 9, атрымана {result.Count}");
    Assert(result.Select(item => item.Id).Distinct().Count() == 9, "ёсць дублікаты");
    Assert(result.All(item => item.MediaType != "VIDEO"), "відэа трапіла ў друкаваныя фота");
}

static void TestLayoutGeometry()
{
    var rect = LayoutGeometry.GetPhotoRect(600, 900, .5, .5, .5);
    Assert(Math.Abs(rect.Left - 150) < .01, "фота не па цэнтры па X");
    Assert(Math.Abs(rect.Top - 300) < .01, "фота не па цэнтры па Y");
    Assert(Math.Abs(rect.Width - 300) < .01 && Math.Abs(rect.Height - 300) < .01,
        "квадрат мае няправільны памер");
}

static void TestPrintGeometry()
{
    // PaperSize values are hundredths of an inch: 400 x 600 means exactly 4 x 6 inches.
    var bounds = PrintGeometry.FitInsidePaper(400, 600, 0, 0, 0, 0, 1200, 1800);
    Assert(bounds.X == 0 && bounds.Y == 0, "макет зрушаны");
    Assert(bounds.Width == 400 && bounds.Height == 600, "макет не адпавядае паперы 4×6");
}

static void TestSettingsMigration()
{
    var root = Path.Combine(Path.GetTempPath(), "skarynagram-migration-test-" + Guid.NewGuid());
    var source = Path.Combine(root, "old");
    var destination = Path.Combine(root, "new");
    Directory.CreateDirectory(source);
    File.WriteAllText(Path.Combine(source, "settings.json"), "{\"TemplatePath\":\"C:\\\\old\\\\saved-layout-template.png\",\"SelectedHashtag\":\"test\"}");
    File.WriteAllText(Path.Combine(source, "accounts.json"), "[]");
    File.WriteAllBytes(Path.Combine(source, "saved-layout-template.png"), [1, 2, 3]);
    try
    {
        DataMigrationCore.Migrate(source, destination);
        Assert(File.Exists(Path.Combine(destination, "settings.json")), "settings.json не перанесены");
        Assert(File.Exists(Path.Combine(destination, "accounts.json")), "accounts.json не перанесены");
        var settings = File.ReadAllText(Path.Combine(destination, "settings.json"));
        Assert(settings.Contains(destination.Replace("\\", "\\\\")), "шлях шаблона не абноўлены");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static MediaElement Media(string id, string type = "IMAGE") =>
    new(id, string.Empty, type, $"https://example.invalid/{id}.jpg", string.Empty, 0, DateTimeOffset.UtcNow);

static void TestPrintStatistics()
{
    var root = Path.Combine(Path.GetTempPath(), "skarynagram-print-statistics-" + Guid.NewGuid());
    try
    {
        var service = new PrintStatisticsService(root);
        service.RecordSuccessfulPrintAsync(2).GetAwaiter().GetResult();
        service.RecordSuccessfulPrintAsync(3).GetAwaiter().GetResult();
        var first = service.GetSnapshot();
        Assert(first.DayCount == 5, $"чакалася 5 за дзень, атрымана {first.DayCount}");
        Assert(first.TotalCount == 5, $"чакалася 5 усяго, атрымана {first.TotalCount}");

        var reopened = new PrintStatisticsService(root).GetSnapshot();
        Assert(reopened.DayCount == 5 && reopened.TotalCount == 5,
            "лічыльнік не захаваўся пасля паўторнага адкрыцця");

        var csv = File.ReadAllLines(reopened.FilePath);
        Assert(csv.Length == 2, "табліца павінна мець загаловак і адзін радок дня");
        Assert(csv[1].EndsWith(";5;5"), "значэнні ў CSV-табліцы няправільныя");

        var sourcePhoto = Path.Combine(root, "test-photo.png");
        File.WriteAllBytes(sourcePhoto, [1, 2, 3, 4]);
        var savedPhoto = service.SavePrintedPhotoAsync(sourcePhoto).GetAwaiter().GetResult();
        Assert(File.Exists(savedPhoto), "надрукаваны макет не захаваны ў photos");
        Assert(Path.GetDirectoryName(savedPhoto) == Path.Combine(root, "photos"),
            "надрукаваны макет захаваны не ў папку photos");
        Assert(Directory.Exists(Path.Combine(root, "counter")), "папка counter не створана");
    }
    finally
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }
}

static void TestHistoricalImageService()
{
    var source = Path.Combine(Path.GetTempPath(), "skarynagram-history-test-" + Guid.NewGuid() + ".png");
    File.WriteAllBytes(source, [137, 80, 78, 71]);
    try
    {
        var handler = new HistoricalApiHandler();
        var service = new HistoricalImageService(new HttpClient(handler));
        var result = service.TransformPeopleAsync(source, "test-key").GetAwaiter().GetResult();
        Assert(result.SequenceEqual(new byte[] { 1, 2, 3, 4 }), "API-выява прачытана няправільна");
        Assert(handler.Authorization == "Bearer test-key", "ключ API не перададзены");
        Assert(handler.RequestBody.Contains("gpt-image-2"), "не пазначана мадэль GPT Image 2");
        Assert(handler.RequestBody.Contains("low"), "не ўключана хуткая якасць low");
        Assert(handler.RequestBody.Contains("1024x1024"), "не зададзены хуткі памер 1024x1024");
        Assert(!handler.RequestBody.Contains("input_fidelity"), "gpt-image-2 не падтрымлівае input_fidelity");
        Assert(handler.RequestBody.Contains("background"), "промпт не абараняе фон");
    }
    finally
    {
        File.Delete(source);
    }
}

static void TestHistoricalImageApiError()
{
    var source = Path.Combine(Path.GetTempPath(), "skarynagram-history-error-test-" + Guid.NewGuid() + ".png");
    File.WriteAllBytes(source, [137, 80, 78, 71]);
    try
    {
        var service = new HistoricalImageService(new HttpClient(new HistoricalApiErrorHandler()));
        try
        {
            service.TransformPeopleAsync(source, "invalid-key").GetAwaiter().GetResult();
            throw new InvalidOperationException("чакалася кантраляваная памылка API");
        }
        catch (InvalidOperationException exception)
        {
            Assert(exception.Message.Contains("401"), "код памылкі API не перададзены ў інтэрфейс");
        }
    }
    finally
    {
        File.Delete(source);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed class HistoricalApiHandler : HttpMessageHandler
{
    public string Authorization { get; private set; } = string.Empty;
    public string RequestBody { get; private set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Authorization = request.Headers.Authorization?.ToString() ?? string.Empty;
        RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"data":[{"b64_json":"AQIDBA=="}]}""")
        };
    }
}

sealed class HistoricalApiErrorHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"error":{"message":"Invalid API key"}}""")
        });
}
