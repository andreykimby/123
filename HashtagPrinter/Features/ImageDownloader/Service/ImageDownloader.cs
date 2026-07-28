using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Features.Services
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Maui.Storage;

    public class ImageDownloader
    {
        private readonly HttpClient _httpClient;

        public ImageDownloader(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Скачивает изображение по URL в временную папку приложения и возвращает полный локальный путь.
        /// При ошибке бросает исключение.
        /// </summary>
        public async Task<string> DownloadImageToTempAsync(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException("imageUrl is null or empty", nameof(imageUrl));

            // Запрос
            using var resp = await _httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            // Читаем содержимое
            var contentStream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);

            // Определяем расширение: сначала из URL, затем из Content-Type, иначе .img
            string? extension = GetExtensionFromUrl(imageUrl);
            if (string.IsNullOrEmpty(extension))
            {
                if (resp.Content.Headers.ContentType?.MediaType is string mediaType)
                {
                    extension = GetExtensionFromMediaType(mediaType);
                }
            }
            if (string.IsNullOrEmpty(extension))
                extension = ".img";

            // Формируем имя файла в папке кэша
            string fileName = $"{Guid.NewGuid()}{extension}";
            string folder = FileSystem.CacheDirectory; // временная папка
            string filePath = Path.Combine(folder, fileName);

            // Сохраняем поток в файл
            // На UI-потоке не блокировать: используем ConfigureAwait(false) выше и FileStream обычный
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await contentStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }

            return filePath;
        }

        private static string? GetExtensionFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var seg = Path.GetFileName(uri.LocalPath);
                if (!string.IsNullOrEmpty(seg) && seg.Contains("."))
                {
                    var ext = Path.GetExtension(seg);
                    if (!string.IsNullOrEmpty(ext)) return ext;
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }

        private static string? GetExtensionFromMediaType(string mediaType)
        {
            // простая мапа по популярным типам
            return mediaType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/bmp" => ".bmp",
                "image/svg+xml" => ".svg",
                _ => null
            };
        }
    }
}
