using ABI.System;
using System;
using System.Collections.Generic;
using System.Text;

namespace Skarynagram.Utils
{
    public static class ImageUtils
    {
        public static async Task<Stream> GetImageStreamAsync(ImageSource source)
        {
            async Task<Stream> FromUri(UriImageSource uriSource)
            {
                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(uriSource.Uri);
                return new MemoryStream(bytes);
            }

            return source switch
            {
                FileImageSource file => File.OpenRead(file.File),
                StreamImageSource streamSource => await streamSource.Stream(CancellationToken.None),
                UriImageSource uriSource => await FromUri(uriSource),
                _ => throw new NotSupportedException("Unsupported ImageSource type")


            };
        }

        public static async Task<Stream> GetImageStreamAsync(string source)
        {
            return File.OpenRead(source);
        }
    }
}


