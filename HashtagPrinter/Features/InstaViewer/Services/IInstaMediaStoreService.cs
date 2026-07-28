using Skarynagram.Features.InstaViewer.Entities;

namespace Skarynagram.Features.InstaViewer.Services
{
    public interface IInstaMediaStoreService
    {
        Task AddMediaElements(IEnumerable<MediaElement> mediaElements);
        Task ClearMediaElements();
        public int PerPage { get; set; }
        /// <summary>
        /// Возвращает отсортированную по Timestamp коллекцию MediaElement (в начале новее)
        /// </summary>
        /// <param name="pageSize">Сколько элементов запросить</param>
        /// <param name="pageNumber">Номер страницы</param>
        /// <returns></returns>
        Task<IEnumerable<MediaElement>> GetMediaElements(int pageNumber = 0);

        int GetMediaElementsCount();
    }
}
