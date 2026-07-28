using System.Text.Json;
using System.Text.Json.Nodes;

namespace Skarynagram.Features.AppSettingsService.Services;

public static class DataMigrationCore
{
    private static readonly string[] MigratedFiles = ["settings.json", "accounts.json"];

    public static void Migrate(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        if (!Directory.Exists(sourceDirectory) || PathsEqual(sourceDirectory, destinationDirectory))
            return;

        foreach (var fileName in MigratedFiles)
            TryCopyIfMissing(Path.Combine(sourceDirectory, fileName), Path.Combine(destinationDirectory, fileName));
        foreach (var template in Directory.EnumerateFiles(sourceDirectory, "saved-layout-template.*"))
            TryCopyIfMissing(template, Path.Combine(destinationDirectory, Path.GetFileName(template)));
        RepairTemplatePath(destinationDirectory);
    }

    public static void RepairTemplatePath(string dataDirectory)
    {
        var settingsPath = Path.Combine(dataDirectory, "settings.json");
        if (!File.Exists(settingsPath))
            return;
        var root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject;
        var template = Directory.EnumerateFiles(dataDirectory, "saved-layout-template.*").FirstOrDefault();
        if (root == null || template == null)
            return;
        root["TemplatePath"] = template;
        File.WriteAllText(settingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CopyIfMissing(string source, string destination)
    {
        if (File.Exists(source) && !File.Exists(destination))
            File.Copy(source, destination);
    }

    private static void TryCopyIfMissing(string source, string destination)
    {
        try
        {
            CopyIfMissing(source, destination);
        }
        catch (IOException)
        {
            // A legacy file may still be locked by an older build. Skip it and continue startup.
        }
        catch (UnauthorizedAccessException)
        {
            // Migration is optional and must not make the application unusable.
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left).TrimEnd('\\'), Path.GetFullPath(right).TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);
}
