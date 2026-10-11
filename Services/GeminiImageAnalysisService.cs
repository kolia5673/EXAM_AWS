using System.Net.Http.Json;
using System.Text.Json;

namespace AIImageService.Services;

public class GeminiImageAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiImageAnalysisService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> AnalyzeAsync(
        byte[] imageBytes,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        var model = _configuration["Gemini:Model"]
                    ?? "gemini-3.8-flash";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured.");
        }

        var endpoint =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(
                HttpMethod.Post, endpoint);

            request.Headers.Add("x-goog-api-key", apiKey);

            request.Content = JsonContent.Create(new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = Convert.ToBase64String(imageBytes)
                                }
                            },
                            new
                            {
                                text = """
                                    Analyze this image.
                                    Identify the main object or scene.
                                    Return a concise result in English:
                                    Object: [main object or scene]
                                    Description: [brief description]
                                    Do not invent a confidence percentage.
                                    """
                            }
                        }
                    }
                }
            });

            using var response = await _httpClient.SendAsync(
                request, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            var statusCode = (int)response.StatusCode;

            var shouldRetry =
                statusCode == 429 ||
                statusCode == 503 ||
                statusCode >= 500;

            if (shouldRetry && attempt < 3)
            {
                Console.WriteLine(
                    $"Gemini returned HTTP {statusCode}. " +
                    $"Retry {attempt + 1}/3 in {attempt * 3} seconds.");

                await Task.Delay(
                    TimeSpan.FromSeconds(attempt * 3),
                    cancellationToken);

                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Gemini API returned {statusCode}: {responseBody}");
            }

            using var document = JsonDocument.Parse(responseBody);

            if (!document.RootElement.TryGetProperty(
                    "candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    "Gemini returned no analysis.");
            }

            var parts = candidates[0]
                .GetProperty("content")
                .GetProperty("parts");

            var result = string.Join(
                "\n",
                parts.EnumerateArray()
                    .Where(p => p.TryGetProperty("text", out _))
                    .Select(p => p.GetProperty("text").GetString())
                    .Where(t => !string.IsNullOrWhiteSpace(t)));

            if (string.IsNullOrWhiteSpace(result))
            {
                throw new InvalidOperationException(
                    "Gemini returned an empty analysis.");
            }

            return result;
        }

        throw new InvalidOperationException(
            "Gemini analysis failed after multiple attempts.");
    }
}