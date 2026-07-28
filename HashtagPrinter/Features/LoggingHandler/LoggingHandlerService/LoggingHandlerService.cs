using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Skarynagram.Features.LoggingHandler.LoggingHandlerService
{
    
    public class LoggingHandlerService : DelegatingHandler
    {
        private readonly ILogger<LoggingHandlerService> _logger;

        public LoggingHandlerService(ILogger<LoggingHandlerService> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {

                _logger.LogDebug("Request: {method} {url}", request.Method, RedactSecrets(request.RequestUri));

                var response = await base.SendAsync(request, cancellationToken);

                foreach (var header in response.Headers)
                    _logger.LogDebug("Header: {key} = {value}", header.Key, string.Join(", ", header.Value));

                foreach (var header in response.Content.Headers)
                    _logger.LogDebug("Content Header: {key} = {value}", header.Key, string.Join(", ", header.Value));

                return response;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Ошибка при отправке синхронного запроса");
            }
            return new HttpResponseMessage();
        }

        private static string RedactSecrets(Uri? uri)
        {
            if (uri == null)
                return string.Empty;

            return Regex.Replace(
                uri.ToString(),
                @"(?i)(access_token|client_secret)=([^&]+)",
                "$1=***");
        }
    }
}
