using System.Globalization;
using System.Text;

namespace Skarynagram.Features.PrintStatistics;

public sealed record PrintStatisticsSnapshot(DateOnly Day, int DayCount, long TotalCount, string FilePath);

public interface IPrintStatisticsService
{
    event EventHandler<PrintStatisticsSnapshot>? Changed;
    PrintStatisticsSnapshot GetSnapshot();
    Task<PrintStatisticsSnapshot> RecordSuccessfulPrintAsync(int copies, CancellationToken cancellationToken = default);
    Task<string> SavePrintedPhotoAsync(string sourceFilePath, CancellationToken cancellationToken = default);
}

public sealed class PrintStatisticsService : IPrintStatisticsService
{
    private const string Header = "Дата;Колькасць за дзень;Агульная колькасць";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;
    private readonly string _photosFolder;

    public event EventHandler<PrintStatisticsSnapshot>? Changed;

    public PrintStatisticsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "skarynagram"))
    {
    }

    public PrintStatisticsService(string folder)
    {
        var counterFolder = Path.Combine(folder, "counter");
        _photosFolder = Path.Combine(folder, "photos");
        Directory.CreateDirectory(counterFolder);
        Directory.CreateDirectory(_photosFolder);
        _filePath = Path.Combine(counterFolder, "статыстыка-друку.csv");
        EnsureFileExists();
    }

    public async Task<string> SavePrintedPhotoAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Файл надрукаванага макета не знойдзены.", sourceFilePath);

        var fileName = $"druk-{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}-{Guid.NewGuid():N}.png";
        var destination = Path.Combine(_photosFolder, fileName);
        await using var source = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
        return destination;
    }

    public PrintStatisticsSnapshot GetSnapshot()
    {
        _gate.Wait();
        try { return ReadSnapshot(DateOnly.FromDateTime(DateTime.Now)); }
        finally { _gate.Release(); }
    }

    public async Task<PrintStatisticsSnapshot> RecordSuccessfulPrintAsync(
        int copies,
        CancellationToken cancellationToken = default)
    {
        if (copies <= 0)
            throw new ArgumentOutOfRangeException(nameof(copies));

        await _gate.WaitAsync(cancellationToken);
        PrintStatisticsSnapshot snapshot;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var rows = ReadRows();
            var dayIndex = rows.FindIndex(row => row.Day == today);

            if (dayIndex >= 0)
            {
                var current = rows[dayIndex];
                rows[dayIndex] = current with { DayCount = checked(current.DayCount + copies) };
            }
            else
            {
                rows.Add(new PrintRow(today, copies, 0));
            }

            long runningTotal = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                runningTotal = checked(runningTotal + rows[i].DayCount);
                rows[i] = rows[i] with { TotalCount = runningTotal };
            }

            WriteRows(rows);
            var todayRow = rows.First(row => row.Day == today);
            snapshot = new PrintStatisticsSnapshot(today, todayRow.DayCount, runningTotal, _filePath);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, snapshot);
        return snapshot;
    }

    private void EnsureFileExists()
    {
        if (!File.Exists(_filePath))
            File.WriteAllText(_filePath, Header + Environment.NewLine, new UTF8Encoding(true));
    }

    private PrintStatisticsSnapshot ReadSnapshot(DateOnly today)
    {
        var rows = ReadRows();
        var todayCount = rows.FirstOrDefault(row => row.Day == today)?.DayCount ?? 0;
        var total = rows.Count == 0 ? 0 : rows[^1].TotalCount;
        return new PrintStatisticsSnapshot(today, todayCount, total, _filePath);
    }

    private List<PrintRow> ReadRows()
    {
        EnsureFileExists();
        var result = new List<PrintRow>();
        foreach (var line in File.ReadLines(_filePath, Encoding.UTF8).Skip(1))
        {
            var parts = line.Split(';');
            if (parts.Length != 3
                || !DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var dayCount)
                || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var totalCount))
                continue;
            result.Add(new PrintRow(day, dayCount, totalCount));
        }
        return result.OrderBy(row => row.Day).ToList();
    }

    private void WriteRows(IEnumerable<PrintRow> rows)
    {
        var tempPath = _filePath + ".tmp";
        using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(true)))
        {
            writer.WriteLine(Header);
            foreach (var row in rows)
                writer.WriteLine($"{row.Day:yyyy-MM-dd};{row.DayCount};{row.TotalCount}");
        }
        File.Move(tempPath, _filePath, true);
    }

    private sealed record PrintRow(DateOnly Day, int DayCount, long TotalCount);
}
