using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Skarynagram.Features.AuthService.Models
{
    //TODO: Hello WWAorld
    public class Account
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string Category { get; set; } = string.Empty;
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        static public Account Default()
        {
            return new Account
            {
                Id = string.Empty,
                Name = string.Empty,
                Category = string.Empty,
                AccessToken = string.Empty
            };
        }
    }
}
