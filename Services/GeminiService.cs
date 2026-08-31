using System.Text;
using System.Text.Json;

namespace RunWise.Services
{
    public class GeminiService
    {
        private readonly HttpClient _client;
        // User provided Gemini API key
        // TODO: move this out of source (user secrets / env var / appsettings) and rotate it.
        // Also double check this looks like a real Gemini API key (usually starts "AIzaSy...").
        private const string ApiKey = "AQ.Ab8RN6LtFU_nSKi-0DkOSzLhAEk2oQrDOX14sPSqk81jcUzxmw";

        public GeminiService()
        {
            _client = new HttpClient();
        }
        
        public async Task<string?> GenerateTextAsync(string prompt)
        {
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={ApiKey}";

                // v1beta generateContent expects "contents", not the old "prompt" shape.
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[] { new { text = prompt } }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.2,
                        maxOutputTokens = 256
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _client.PostAsync(url, content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Gemini API error: {response.StatusCode} - {json}");
                    return null;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Correct path: candidates[0].content.parts[0].text
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var first = candidates[0];
                    if (first.TryGetProperty("content", out var contentEl) &&
                        contentEl.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0 &&
                        parts[0].TryGetProperty("text", out var textEl))
                    {
                        return textEl.GetString();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GeminiService exception: {ex}");
                return null;
            }
        }
    }
}