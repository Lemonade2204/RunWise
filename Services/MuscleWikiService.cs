using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using RunWise.Models;
using System.Collections.Generic;
using System.Linq;

namespace RunWise.Services;

public class MuscleWikiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public MuscleWikiService(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        if (_http.BaseAddress == null) _http.BaseAddress = new Uri("https://api.musclewiki.com/");


        _apiKey = Environment.GetEnvironmentVariable("MUSCLEWIKI_KEY") ;
    }

    // Videos removed per user request; do not expose video endpoints here.


    public async Task<Exercise?> GetExerciseByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var candidates = new[] { $"exercises/{id}", $"exercise/{id}", $"exercises?id={id}", $"exercise?id={id}" };

        foreach (var path in candidates)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, path);
                req.Headers.Add("x-api-key", _apiKey);

                using var resp = await _http.SendAsync(req).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) continue;

                var txt = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(txt)) continue;

                try
                {
                    var doc = JsonDocument.Parse(txt);
                    var root = doc.RootElement;

                    // If the response is an array, pick first element
                    JsonElement elem = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().FirstOrDefault() : root;

                    if (elem.ValueKind != JsonValueKind.Object) continue;

                    var title = TryGetString(elem, "title", "name") ?? string.Empty;
                    var instr = TryGetString(elem, "instruction", "instructions", "tips", "description") ?? string.Empty;
                    var duration = TryGetString(elem, "duration", "time", "reps", "duration_or_reps") ?? string.Empty;

                    var ex = new Exercise
                    {
                        Title = title,
                        Description = duration,
                        Tips = instr
                    };

                    ex.DurationSeconds = ParseDurationSeconds(duration ?? instr);
                    return ex;
                }
                catch (JsonException)
                {
                    // try next candidate
                    continue;
                }
            }
            catch
            {
                // network / parse error - try next pattern
                continue;
            }
        }

        return null;
    }

    public async Task<List<Exercise>> CreateWorkoutAsync(string when, string level)
    {
        var result = new List<Exercise>();
        try
        {
            var doc = await GetDashboardRoutineAsync(when, level).ConfigureAwait(false);
            if (doc == null) return result;

            var root = doc.RootElement;
            JsonElement arrayElem = default;
            if (root.ValueKind == JsonValueKind.Array)
                arrayElem = root;
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("items", out var it) && it.ValueKind == JsonValueKind.Array) arrayElem = it;
                else if (root.TryGetProperty("data", out var da) && da.ValueKind == JsonValueKind.Array) arrayElem = da;
                else if (root.TryGetProperty("exercises", out var exs) && exs.ValueKind == JsonValueKind.Array) arrayElem = exs;
            }

            if (arrayElem.ValueKind != JsonValueKind.Array) return result;

            foreach (var item in arrayElem.EnumerateArray())
            {
                var title = TryGetString(item, "title", "name") ?? string.Empty;
                var instr = TryGetString(item, "instruction", "instructions", "tips", "description") ?? string.Empty;
                var duration = TryGetString(item, "duration", "time", "reps", "duration_or_reps") ?? string.Empty;

                var ex = new Exercise
                {
                    Title = title,
                    Description = duration,
                    Tips = instr
                };

                ex.DurationSeconds = ParseDurationSeconds(duration ?? instr);
                result.Add(ex);
            }
        }
        catch
        {
            // swallow and return what we have (or empty list)
        }

        return result;
    }

    // reuse helpers used elsewhere
    private static string? TryGetString(JsonElement elem, params string[] names)
    {
        foreach (var n in names)
        {
            if (elem.ValueKind == JsonValueKind.Object && elem.TryGetProperty(n, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                try { return v.ToString(); } catch { }
            }
        }
        return null;
    }

    private static int? ParseDurationSeconds(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            var s = text.ToLowerInvariant();
            var numStr = System.Text.RegularExpressions.Regex.Match(s, "\\d+").Value;
            if (int.TryParse(numStr, out var n))
            {
                if (s.Contains("min") || s.Contains("minute")) return n * 60;
                if (s.Contains("sec") || s.Contains("second") || s.Contains("s")) return n;
                if (n <= 60) return n;
                return n;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Call the MuscleWiki dashboard endpoint to fetch a curated routine (includes video information when available).
    /// </summary>
    public async Task<JsonDocument?> GetDashboardRoutineAsync(string when, string level)
    {
        try
        {
            var url = $"dashboard?when={Uri.EscapeDataString(when)}&level={Uri.EscapeDataString(level)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("x-api-key", _apiKey);

            using var resp = await _http.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            var txt = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            try
            {
                var doc = JsonDocument.Parse(txt);
                return doc;
            }
            catch (JsonException)
            {
                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GenerateRoutineAsync(string when, string level)
    {
        // when: "pre" or "post"; level: novice|beginner|intermediate|advance
        var friendlyWhen = when == "pre" ? "pre-run warmup" : "post-run cooldown and stretching";
        var prompt = $"Create a {level} {friendlyWhen} routine with 5 items. For each item return title, duration_or_reps, and a one-line instruction. Return the result as a JSON array of objects with properties: title, duration, instruction.";

        var payload = new { prompt = prompt, max_tokens = 800 };

        using var req = new HttpRequestMessage(HttpMethod.Post, "playground")
        {
            Content = JsonContent.Create(payload)
        };

        // MuscleWiki expects an api key header named 'x-api-key'
        req.Headers.Add("x-api-key", _apiKey);

        try
        {
            using var resp = await _http.SendAsync(req).ConfigureAwait(false);
            var txt = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            // Try to parse JSON response. The playground may return { text: "..." } or a raw array
            try
            {
                var doc = JsonDocument.Parse(txt);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return txt;
                }

                if (doc.RootElement.TryGetProperty("text", out var t))
                    return t.GetString();
                if (doc.RootElement.TryGetProperty("response", out var r))
                    return r.GetString();

                // otherwise return raw text
                return txt;
            }
            catch (JsonException)
            {
                return txt;
            }
        }
        catch
        {
            return null;
        }
    }
}
