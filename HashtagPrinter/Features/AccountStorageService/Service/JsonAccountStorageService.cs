using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Skarynagram.Features.AuthService.Models;
using Microsoft.Extensions.Logging;


namespace Skarynagram.Features.AccountStorageService.Service
{
    public class JsonAccountStorageService : IAccountStorageService
    {
        private readonly ILogger _logger;
        private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "accounts.json");
        public JsonAccountStorageService(ILogger<JsonAccountStorageService> logger) 
        { 
            _logger = logger;
            _logger.LogDebug(_filePath);
        }
        public async Task<List<Account>> LoadAccountsAsync()
        {
            if (!File.Exists(_filePath)) 
                return new List<Account>();
            var json = await File.ReadAllTextAsync(_filePath);

            return JsonSerializer.Deserialize<List<Account>>(json) ?? new List<Account>();
        }

        public async Task SaveAccountsAsync(IEnumerable<Account> accounts)
        {
            var json = JsonSerializer.Serialize(accounts);
            await File.WriteAllTextAsync(_filePath, json);
        }

    }
}
