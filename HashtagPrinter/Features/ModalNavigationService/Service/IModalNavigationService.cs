using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Features.ModalNavigationService
{
    public interface IModalNavigationService
    {
        void SetCurrentPage(Page page);
        Task CloseModalAsync(object? result = null);
    }
}
