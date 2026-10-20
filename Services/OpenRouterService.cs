using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class OpenRouterService
{
    private readonly HttpClient _client;
    private const string ApiKey = "";
    public OpenRouterService()
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri("https://api.groq.com/openai/v1/")
        };
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ApiKey);
    }

    public async Task<string?> GetCoachingCueAsync(string prompt)
    {
        var requestBody = new
        {
            model = "openai/gpt-oss-120b",
            messages = new[]
            {
            new { role = "user", content = prompt }
        }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _client.PostAsync("chat/completions", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            // TEMP: see exactly what's coming back
            System.Diagnostics.Debug.WriteLine($"Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Body: {responseJson}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception: {ex}");
            return null;
        }
    }
}