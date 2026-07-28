using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Features.AuthService.Models
{
    public class GetAccountsResponse
    {
        public required List<Account> Data { get; set; }

        public static GetAccountsResponse Empty => new GetAccountsResponse
        {
            Data = new List<Account>()
        };
    }
}
