using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Features.AppSettingsService.Repositories
{
    public interface IAppSettingsRepository
    {
        T Get<T>(string key);
        void Set<T>(string key, T value);
    }
}
