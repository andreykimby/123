namespace Skarynagram.Features.AppSettingsService.Services;

internal static class LegacyDataMigration
{
    public static void MigrateIfNeeded()
    {
        var destination = FileSystem.AppDataDirectory;
        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Lukavskiy Mikhail", "ru.lukavskym.hashtagprinter", "Data");

        DataMigrationCore.Migrate(legacy, destination);
    }

    internal static void RepairTemplatePath(string dataDirectory)
    {
        DataMigrationCore.RepairTemplatePath(dataDirectory);
    }
}
