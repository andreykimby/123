using System.Net.Http.Headers;
using System.Text.Json;
using SkiaSharp;

namespace Skarynagram.Features.ImageTransformation;

public sealed class HistoricalImageService
{
    private const string Endpoint = "https://api.openai.com/v1/images/edits";
    private readonly HttpClient _httpClient;

    public HistoricalImageService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(3);
    }

    public async Task<byte[]> TransformPeopleAsync(
        string imagePath,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("У наладах не пазначаны ключ OpenAI API.");
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Зыходнае фота не знойдзена.", imagePath);

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        using var content = new MultipartFormDataContent();
        var preparedImage = await PrepareImageAsync(imagePath, cancellationToken);
        using var imageStream = new MemoryStream(preparedImage.Bytes, writable: false);
        using var imageContent = new StreamContent(imageStream);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(preparedImage.MediaType);
        content.Add(imageContent, "image", preparedImage.FileName);
        content.Add(new StringContent("gpt-image-2"), "model");
        content.Add(new StringContent("low"), "quality");
        content.Add(new StringContent("1024x1024"), "size");
        content.Add(new StringContent(HistoricalPrompt), "prompt");
        request.Content = content;

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(GetApiError(responseBody, response.StatusCode));

        using var document = JsonDocument.Parse(responseBody);
        var data = document.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0 ||
            !data[0].TryGetProperty("b64_json", out var encodedImage) ||
            string.IsNullOrWhiteSpace(encodedImage.GetString()))
        {
            throw new InvalidOperationException("OpenAI не вярнуў апрацаваную выяву.");
        }

        return Convert.FromBase64String(encodedImage.GetString()!);
    }

    private static async Task<PreparedImage> PrepareImageAsync(
        string imagePath,
        CancellationToken cancellationToken)
    {
        await using var sourceStream = File.OpenRead(imagePath);
        using var source = SKBitmap.Decode(sourceStream);

        if (source is null || Math.Max(source.Width, source.Height) <= 1024)
        {
            return new PreparedImage(
                await File.ReadAllBytesAsync(imagePath, cancellationToken),
                GetMediaType(imagePath),
                Path.GetFileName(imagePath));
        }

        var scale = 1024d / Math.Max(source.Width, source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var resized = source.Resize(
            new SKImageInfo(width, height),
            new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 88);

        return new PreparedImage(
            encoded.ToArray(),
            "image/jpeg",
            $"{Path.GetFileNameWithoutExtension(imagePath)}-1024.jpg");
    }

    private sealed record PreparedImage(byte[] Bytes, string MediaType, string FileName);

    private static string GetMediaType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };

    private static string GetApiError(string body, System.Net.HttpStatusCode statusCode)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var message = document.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString();
            return $"OpenAI ({(int)statusCode}): {message}";
        }
        catch
        {
            return $"OpenAI вярнуў памылку {(int)statusCode}.";
        }
    }

    private const string HistoricalPrompt = """
        Edit the supplied photograph, not its framing or background.
        Detect every human being in the photograph. Preserve each person's identity,
        face, facial expression, age, body shape, pose, hands, position, scale,
        lighting, shadows and occlusion as faithfully as possible.
        Change only the people and their clothing: dress them in historically plausible
        early-sixteenth-century clothing of the Grand Duchy of Lithuania and the
        Belarusian lands in the period of Francysk Skaryna. Use realistic natural
        fabrics, construction and accessories appropriate to each person.
        Do not add or remove people.
        The entire non-human background and all objects must remain unchanged:
        identical architecture, furniture, landscape, signs, text, floor, walls,
        colors, crop, camera angle, perspective and lighting. Do not replace, repaint,
        stylize, blur or expand the background. Do not add historical objects.
        Keep the result photorealistic and avoid theatrical fantasy costumes.
        """;
}
