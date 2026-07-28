using Skarynagram.Features.AuthService.Models;

namespace Skarynagram.Features.AccountStorageService.Service
{
    public interface IAccountStorageService
    {
        Task<List<Account>> LoadAccountsAsync();
        Task SaveAccountsAsync(IEnumerable<Account> accounts);
    }
}