using CommunityToolkit.Maui.Storage;
using System.IO.Compression;

namespace Skarynagram.Features.AppSettingsService.Services;

public sealed class SettingsTransferService
{
    private static readonly string[] SettingsFiles = ["settings.json", "accounts.json"];

    public async Task<bool> ExportAsync(CancellationToken cancellationToken = default)
    {
        var tempPath = Path.Combine(FileSystem.CacheDirectory, $"skarynagram-settings-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
        {
            foreach (var fileName in SettingsFiles)
                AddIfExists(archive, Path.Combine(FileSystem.AppDataDirectory, fileName), fileName);
            foreach (var template in Directory.EnumerateFiles(FileSystem.AppDataDirectory, "saved-layout-template.*"))
                AddIfExists(archive, template, Path.GetFileName(template));
        }

        await using var stream = File.OpenRead(tempPath);
        var result = await FileSaver.Default.SaveAsync(Path.GetFileName(tempPath), stream, cancellationToken);
        return result.IsSuccessful;
    }

    public async Task<bool> ImportAsync(CancellationToken cancellationToken = default)
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Абярыце рэзервовую копію Skarynagram",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = [".zip"]
            })
        });
        if (picked == null)
            return false;

        await using var input = await picked.OpenReadAsync();
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        var allowed = new HashSet<string>(SettingsFiles, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var name = Path.GetFileName(entry.FullName);
            if (!allowed.Contains(name) && !name.StartsWith("saved-layout-template.", StringComparison.OrdinalIgnoreCase))
                continue;
            var destination = Path.Combine(FileSystem.AppDataDirectory, name);
            await using var source = entry.Open();
            await using var target = File.Create(destination);
            await source.CopyToAsync(target, cancellationToken);
        }

        LegacyDataMigration.RepairTemplatePath(FileSystem.AppDataDirectory);
        return true;
    }

    private static void AddIfExists(ZipArchive archive, string path, string name)
    {
        if (File.Exists(path))
            archive.CreateEntryFromFile(path, name, CompressionLevel.Optimal);
    }
}
