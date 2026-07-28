using Skarynagram.Features.InstaViewer.Entities;
using Skarynagram.Features.InstaViewer.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


       

namespace Skarynagram.Features.Services
{
    public class ImagesService : IDisposable
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Очередь для хранения изображений, ожидающих обработки.
        /// </summary>
        private readonly ConcurrentQueue<MediaElement> _imageQueue = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly object _collectionLock = new();
        private Task? _processingTask;
        private IInstaMediaStoreService _instaMediaStoreService;
        private int _page = 0;
        private int _initCount = 0;
        private int InitCount { 
            get => _initCount; 
            set
            {
                if (_initCount == 0)
                {
                    _initCount = value;
                }
            }
        }
        private List<MediaElement> _images { get; } = new();
        public ImagesService(ILogger<ImagesService> logger, IInstaMediaStoreService instaMediaStoreService) 
        {
            _logger = logger;
            _instaMediaStoreService = instaMediaStoreService;
            StartProcessingQueue();
        }

        public void AddImage(MediaElement image)
        {
            if (image == null)
            {
                _logger.LogWarning("Попытка добавить null изображение в коллекцию.");
                return;
            }

            _imageQueue.Enqueue(image);
            _logger.LogDebug("Изображение с ID {ImageId} добавлено в очередь.", image.Id);
        }

        public IReadOnlyList<MediaElement> GetSnapshot()
        {
            lock (_collectionLock)
                return _images.OrderByDescending((e) => e.Timestamp).ToList();
        }

        public void AddImages(IEnumerable<MediaElement> images)
        {
            if (images == null || !images.Any())
            {
                _logger.LogWarning("Попытка добавить пустую коллекцию изображений в очередь.");
                return;
            }
            foreach (var image in images)
            {
                _imageQueue.Enqueue(image);
                _logger.LogDebug("Изображение с ID {ImageId} добавлено в очередь.", image.Id);
            }
        }


        private void StartProcessingQueue()
        {
            _processingTask = Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
            _logger.LogInformation("Запуск процесса обработки очереди изображений...");
        }

        // Фоновая обработка очереди
        private async Task ProcessQueueAsync(CancellationToken ct)
        {

            #region Старый код батчевой обработки
            //const int BATCH_SIZE = 10; // подберите под себя, 8–15 обычно оптимально

            //while (!ct.IsCancellationRequested)
            //{
            //    var batch = new List<MediaElement>(BATCH_SIZE);

            //    while (batch.Count < BATCH_SIZE && _imageQueue.TryDequeue(out var img))
            //    {
            //        batch.Add(img);
            //    }

            //    if (batch.Count == 0)
            //    {
            //        await Task.Delay(50, ct);
            //        continue;
            //    }

            //    await MainThread.InvokeOnMainThreadAsync(() =>
            //    {
            //        lock (_collectionLock)
            //        {
            //            foreach (var image in batch)
            //            {
            //                if (!Images.Any(x => x.Id == image.Id))
            //                {
            //                    Images.Add(image);
            //                }
            //            }
            //        }
            //    });

            //    await Task.Delay(10, ct); // небольшая пауза между батчами, чтобы UI не задыхался
            //}
            #endregion
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_imageQueue.TryDequeue(out var image))
                    {
                        await AddToCollectionAsync(image);
                    }
                    else
                    {
                        // Если очередь пуста, делаем небольшую паузу
                        await Task.Delay(100, ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Ошибка при обработке изображения из очереди");
                    await Task.Delay(1000, ct);
                }
            }
        }

        // Асинхронное добавление в коллекцию с блокировкой
        private Task AddToCollectionAsync(MediaElement image)
        {


            return Task.Run(() =>
            //return MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Проверяем дубликаты перед добавлением
                bool exists;
                lock (_collectionLock)
                {
                    exists = _images.Any(x => x.Id == image.Id);
                    if (!exists)
                    {

                        _images.Add(image);
                        
                    }
                }

                if (!exists)
                {
                    _logger?.LogDebug("Изображение добавлено в коллекцию: {ImageId}", image.Id);
                }
                else
                {
                    _logger?.LogDebug("Изображение уже существует в коллекции: {ImageId}", image.Id);
                }
            });
        }

        

        /// <summary>
        /// Проверка наличия изображения
        /// </summary>
        /// <param name="imageId"></param>
        /// <returns></returns>
        public bool ContainsImage(string imageId)
        {
            lock (_collectionLock)
            {
                return _images.Any(x => x.Id == imageId);
            }
        }

        /// <summary>
        /// Очистка коллекции
        /// </summary>
        public void ClearImages()
        {
            lock (_collectionLock)
            {
                _images.Clear();
                _logger?.LogInformation("Коллекция изображений очищена");
            }
        }

        /// <summary>
        /// Загружает в коллекцию Images следующую страницу данных.
        /// Возвращает true если страница загружена
        /// </summary>
        /// <returns>true</returns>
        public async Task<bool> LoadNexPage(int perPage = 12)
        {
            perPage = _instaMediaStoreService.PerPage;
            _logger.LogInformation("Загрузка следующей страницы изображений...");
            var totalImages = _instaMediaStoreService.GetMediaElementsCount();
            var loadedImages = InitCount = _images.Count();
            var loadedPages = (int)(loadedImages / perPage);
            var totalPages = (int)Math.Ceiling((double)totalImages / perPage);
            if (_page >= totalPages && loadedPages >= totalPages)
            {
                _logger.LogInformation("Все страницы уже загружены. Всего страниц: {TotalPages}, текущая страница: {CurrentPage}", totalPages, _page);
                return false;
            }
            if (loadedPages < totalPages)
                _page++;
            var images = await _instaMediaStoreService.GetMediaElements(_page);
            AddImages(images);

            var pagesLeft = totalPages - _page;
            _logger.LogInformation(
                "Загружена страница {PageNumber}. Загружено {ImagesCount} изображений." +
                "Осталось страниц: {PagesLeft}. Всего страниц: {TotalPages}",
                _page,
                images.Count(),
                pagesLeft,
                totalPages
                );
            return pagesLeft > 0;
        }

        public async Task RefreshFromDB()
        {
            _logger.LogInformation("Обновление изображений из базы данных...");
            var images = (await _instaMediaStoreService.GetMediaElements())
                .OrderByDescending(x => x.Timestamp)
                .ToList();

            ClearQueue();
            lock (_collectionLock)
            {
                _images.Clear();
                _images.AddRange(images);
            }
        }

        /// <summary>
        /// Удаление изображения
        /// </summary>
        /// <param name="imageId"></param>
        /// <returns></returns>
        public bool RemoveImage(string imageId)
        {
            lock (_collectionLock)
            {
                var imageToRemove = _images.FirstOrDefault(x => x.Id == imageId);
                if (imageToRemove != null)
                {
                    _images.Remove(imageToRemove);
                    _logger?.LogDebug("Изображение удалено: {ImageId}", imageId);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Получение количества элементов
        /// </summary>
        /// <returns></returns>
        public int GetImagesCount()
        {
            lock (_collectionLock)
            {
                return _images.Count;
            }
        }

        /// <summary>
        /// Очистка очереди
        /// </summary>
        public void ClearQueue()
        {
            while (_imageQueue.TryDequeue(out _)) { }
            _logger?.LogInformation("Очередь изображений очищена");
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();

            try
            {
                _processingTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Игнорируем исключения отмены
            }

            _cancellationTokenSource?.Dispose();
            ClearImages();
            ClearQueue();

            _logger?.LogInformation("Сервис изображений остановлен");
        }

        /// <summary>
        /// Финализатор для безопасного завершения
        /// </summary>
        ~ImagesService()
        {
            Dispose();
        }

    }
}
