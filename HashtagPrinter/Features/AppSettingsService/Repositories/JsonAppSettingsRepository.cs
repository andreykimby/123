using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skarynagram.Features.AppSettingsService.Repositories
{
    internal class JsonAppSettingsRepository : IAppSettingsRepository
    {
        private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "settings.json");
        private Dictionary<string, object> _settings;

        public JsonAppSettingsRepository() 
        {
            
            _settings = LoadSettings();
        }

        private Dictionary<string, object> LoadSettings()
        {
            if (!File.Exists(_filePath))
                return new Dictionary<string, object>();
            
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
        }


        public T Get<T>(string key)
        {
            
            if (_settings.TryGetValue(key, out var value))
            {
                return value is JsonElement jsonElement
                    ? JsonSerializer.Deserialize<T>(jsonElement.GetRawText())
                    : (T)Convert.ChangeType(value, typeof(T));
            }

            return default!;
        }

        public void Set(string key, string value)
        {
            _settings[key] = value;
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_settings));
        }

        public void Set<T>(string key, T value)
        {
            _settings[key] = value!;
            SaveSettings();
        }

        private void SaveSettings()
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_filePath, json);
        }
    }
}
