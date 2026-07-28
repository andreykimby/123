using Skarynagram.Features.InstaViewer.Entities;
using LiteDB;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Features.InstaViewer.Services
{
    public class InstaMediaStoreService : IInstaMediaStoreService, IAsyncDisposable, IDisposable
    {
        private readonly ILogger _logger;
        private LiteDatabase? _db;
        private ILiteCollection<MediaElement>? mediaCollection;
        private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "mediaData.db");
        /// <summary>
        /// Определяет допустимые типы контента для добавления в БД. 
        /// </summary>
        /// <remarks>Доступные на данный моменть типы контента: CAROUSEL_ALBUM, IMAGE или VIDEO.<br />Подробнее в документации:<br /> https://developers.facebook.com/docs/instagram-platform/instagram-graph-api/reference/ig-hashtag/recent-media#returnable-fields</remarks>
        private string[] allowedTypes = ["IMAGE", "CAROUSEL_ALBUM"];
        public int PerPage { get; set; } = 27;
        public InstaMediaStoreService(ILogger<InstaMediaStoreService> logger, int perPage )
        {
            _logger = logger;
            PerPage = perPage;
            try
            {
                _db = new LiteDatabase(_filePath);
                mediaCollection = _db.GetCollection<MediaElement>("insta_media");
                mediaCollection.EnsureIndex(x => x.Id, unique: true);
                mediaCollection.EnsureIndex(x => x.Timestamp);

            }
            catch (System.IO.IOException ioEx)
            {
                _logger.LogError(ioEx, "Ошибка при инициализации базы данных медиа");
                throw;
            }
        }

        

        public async Task<IEnumerable<MediaElement>> GetMediaElements(int pageNumber = 0)
        {
            if (_db == null) throw new ObjectDisposedException(nameof(InstaMediaStoreService));

            
            try
            {
                if (mediaCollection == null)
                {
                    _logger.LogError("Коллекция mediaCollection не инициализирована.");
                    throw new ObjectDisposedException(nameof(InstaMediaStoreService));
                }
                var sw = Stopwatch.StartNew();
                var skip = pageNumber == 0 ? 0 : pageNumber* PerPage;
                var results = mediaCollection.FindAll()
                    .OrderByDescending(x => x.Timestamp)
                    .Skip(skip)
                    .Take(PerPage);
                sw.Stop();
                _logger.LogDebug($"Получено {results.Count()} объектов за {sw.ElapsedMilliseconds} мс ");
                return results;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении медиа из базы данных");
                //throw;
            }
            return Enumerable.Empty<MediaElement>();
            
        }

        public async Task AddMediaElements(IEnumerable<MediaElement> mediaElements)
        {
            if (_db == null) throw new ObjectDisposedException(nameof(InstaMediaStoreService));

            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                if (mediaCollection == null)
                {     _logger.LogError("Коллекция mediaCollection не инициализирована.");
                    return;

                }
                var newItems = mediaElements
                    .Where(item => allowedTypes.Contains(item.MediaType))
                    .Where(item => !mediaCollection.Exists(x => x.Id == item.Id))
                    .ToList();

                if (newItems.Any())
                {
                    mediaCollection.InsertBulk(newItems);  // быстрее Insert в цикле
                }
                _db.Checkpoint();
                sw.Stop();
                _logger.LogDebug($"Вставлено {newItems.Count} объектов за {sw.ElapsedMilliseconds} мс ");
            });
        }

        public async Task ClearMediaElements()
        {
            if (_db == null) throw new ObjectDisposedException(nameof(InstaMediaStoreService));
            if (mediaCollection == null) throw new ObjectDisposedException(nameof(InstaMediaStoreService));

            await Task.Run(() =>
            {
                mediaCollection.DeleteAll();
                _db.Checkpoint();
                _logger.LogInformation("База изображений очищена после смены хэштега");
            });
        }

        public int GetMediaElementsCount()
        {
            if (_db == null) throw new ObjectDisposedException(nameof(InstaMediaStoreService));
            if (mediaCollection == null) throw new ObjectDisposedException(nameof(InstaMediaStoreService));
            return mediaCollection.Count();
        }

        public async ValueTask DisposeAsync()
        {
            Dispose();
            _db = null;
            mediaCollection = null;
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_db != null)
            {
                _db.Checkpoint();  // финальный сброс на диск
                _db.Dispose();
            }
            
        }

    }
}
