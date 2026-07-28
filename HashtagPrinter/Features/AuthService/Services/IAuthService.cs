using Skarynagram.Features.AuthService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Features.AuthService.Services
{
    public interface IAuthService
    {
        /// <summary>
        /// Должен вернуть токен авторизации
        /// </summary>
        /// <returns></returns>
        Task<string> Login(string appId, string appSecret, string redirectUrl);
        Task<List<Account>?> GetAccounts(string accessToken);
        Task<string?> GetPageId(string accountId, string accessToken);
    }
}
