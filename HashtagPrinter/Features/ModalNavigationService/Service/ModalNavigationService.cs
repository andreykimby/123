using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Features.ModalNavigationService
{
    public class ModalNavigationService : IModalNavigationService
    {
        private readonly WeakReference<Page?> _currentPage = new(null);

        // Вызывается один раз при показе модального окна (в команде MediaClick)
        public void SetCurrentPage(Page page)
        {
            _currentPage.SetTarget(page);
        }

        public async Task CloseModalAsync(object? result = null)
        {
            if (_currentPage.TryGetTarget(out var page) && page != null)
            {
                await page.Navigation.PopModalAsync();
                // Если нужно передать результат — используйте MessagingCenter / WeakReferenceMessenger / событие
            }
            else if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.Navigation.PopModalAsync();
            }
        }
    }
}
