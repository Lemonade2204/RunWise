using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace RunWise.Services;

public class SupabaseDatabaseService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly SupabaseAuthService _auth;

    public SupabaseDatabaseService(string supabaseUrl, string anonKey, SupabaseAuthService authService)
    {
        _apiKey = anonKey ?? throw new ArgumentNullException(nameof(anonKey));
        _http = new HttpClient { BaseAddress = new Uri(supabaseUrl ?? throw new ArgumentNullException(nameof(supabaseUrl))) };
        _http.DefaultRequestHeaders.Add("apikey", _apiKey);
        _auth = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    private void SetAuthHeader(HttpRequestMessage req)
    {
        // Use user access token when available
        if (!string.IsNullOrEmpty(_auth.AccessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.AccessToken);
        else
            req.Headers.Add("apikey", _apiKey);
    }

    public async Task<(bool Success, string? Error)> CreateRunAsync(RunData run)
    {
        var payload = new
        {
            id = run.Id,
            user_id = run.UserId,
            distance_km = run.DistanceKm,
            duration_seconds = run.DurationSeconds,
            created_at = run.CreatedAt
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/runs")
        {
            Content = JsonContent.Create(payload)
        };

        SetAuthHeader(req);
        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var content = await resp.Content.ReadAsStringAsync();
            return (false, $"Create run failed: {resp.StatusCode}: {content}");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CreateRunFromSessionAsync(RunWise.Models.RunSession run)
    {
        if (string.IsNullOrEmpty(_auth.UserId))
            return (false, "User not signed in");
        var routePoints = run.RoutePoints?.Select(r => new { latitude = r.Latitude, longitude = r.Longitude }).ToList();

        var payload = new
        {
            id = Guid.TryParse(run.Id, out var gid) ? gid : Guid.NewGuid(),
            user_id = Guid.Parse(_auth.UserId),
            distance_km = Math.Round(run.DistanceKm, 3),
            duration_seconds = (int)run.Duration.TotalSeconds,
            created_at = run.StartTime,
            route_points = routePoints
        };

        // Log payload for debugging (remove or reduce logging in production)
        try
        {
            var dbg = JsonSerializer.Serialize(payload);
            Console.WriteLine($"Supabase CreateRun payload: {dbg}");
        }
        catch { }

        var req = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/runs")
        {
            Content = JsonContent.Create(payload)
        };

        SetAuthHeader(req);
        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        var respContent = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"Supabase create run response: {resp.StatusCode} - {respContent}");
            return (false, $"Create run failed: {resp.StatusCode}: {respContent}");
        }

        // Optionally log created row
        Console.WriteLine($"Supabase create run succeeded: {respContent}");
        return (true, null);
    }

    public async Task<(bool Success, string? Error, List<RunData>? Runs)> GetRunsForCurrentUserAsync()
    {
        if (string.IsNullOrEmpty(_auth.UserId))
            return (false, "User not signed in", null);
        // GET /rest/v1/runs?user_id=eq.<userId>
        // include select=* to be explicit
        var url = $"/rest/v1/runs?select=*&user_id=eq.{_auth.UserId}&order=created_at.desc";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetAuthHeader(req);
        req.Headers.Add("Accept", "application/json");
        var resp = await _http.SendAsync(req);
        var content = await resp.Content.ReadAsStringAsync();

        Console.WriteLine($"Supabase GET runs URL: {url}");
        Console.WriteLine($"Supabase GET runs - status: {resp.StatusCode}");
        Console.WriteLine($"Supabase GET runs - body: {content}");

        if (!resp.IsSuccessStatusCode)
            return (false, $"Get runs failed: {resp.StatusCode}: {content}", null);

        try
        {
            // Parse manually to be resilient to PostgREST numeric string responses
            var list = new List<RunData>();
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return (false, "Unexpected response format", null);

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                try
                {
                    var run = new RunData();

                    if (el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String && Guid.TryParse(idEl.GetString(), out var gid))
                        run.Id = gid;

                    if (el.TryGetProperty("user_id", out var uidEl) && uidEl.ValueKind == JsonValueKind.String && Guid.TryParse(uidEl.GetString(), out var uid))
                        run.UserId = uid;

                    if (el.TryGetProperty("distance_km", out var dEl))
                    {
                        if (dEl.ValueKind == JsonValueKind.Number && dEl.TryGetDouble(out var dval))
                            run.DistanceKm = dval;
                        else if (dEl.ValueKind == JsonValueKind.String && double.TryParse(dEl.GetString(), out var dstr))
                            run.DistanceKm = dstr;
                    }

                    if (el.TryGetProperty("duration_seconds", out var durEl))
                    {
                        if (durEl.ValueKind == JsonValueKind.Number && durEl.TryGetInt32(out var ival))
                            run.DurationSeconds = ival;
                        else if (durEl.ValueKind == JsonValueKind.String && int.TryParse(durEl.GetString(), out var istr))
                            run.DurationSeconds = istr;
                    }

                    if (el.TryGetProperty("created_at", out var caEl))
                    {
                        if (caEl.ValueKind == JsonValueKind.String && DateTime.TryParse(caEl.GetString(), out var dt))
                            run.CreatedAt = dt;
                    }

                    // route_points may be an array of {latitude, longitude}
                    if (el.TryGetProperty("route_points", out var rpEl) && rpEl.ValueKind == JsonValueKind.Array)
                    {
                        var pts = new List<RoutePointDto>();
                        foreach (var p in rpEl.EnumerateArray())
                        {
                            double lat = 0, lon = 0;
                            if (p.TryGetProperty("latitude", out var latEl))
                            {
                                if (latEl.ValueKind == JsonValueKind.Number) lat = latEl.GetDouble();
                                else if (latEl.ValueKind == JsonValueKind.String) double.TryParse(latEl.GetString(), out lat);
                            }
                            if (p.TryGetProperty("longitude", out var lonEl))
                            {
                                if (lonEl.ValueKind == JsonValueKind.Number) lon = lonEl.GetDouble();
                                else if (lonEl.ValueKind == JsonValueKind.String) double.TryParse(lonEl.GetString(), out lon);
                            }
                            pts.Add(new RoutePointDto { Latitude = lat, Longitude = lon });
                        }
                        run.RoutePoints = pts;
                    }

                    list.Add(run);
                }
                catch (Exception exInner)
                {
                    Console.WriteLine($"Error parsing run element: {exInner.Message}");
                }
            }

            return (true, null, list);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string? Error)> UpdateRunFromSessionAsync(RunWise.Models.RunSession run)
    {
        if (string.IsNullOrEmpty(_auth.UserId))
            return (false, "User not signed in");

        if (!Guid.TryParse(run.Id, out var gid))
            return (false, "Invalid run id");

        var routePoints = run.RoutePoints?.Select(r => new { latitude = r.Latitude, longitude = r.Longitude }).ToList();

        var payload = new
        {
            distance_km = run.DistanceKm,
            duration_seconds = (int)run.Duration.TotalSeconds,
            route_points = routePoints
        };

        var url = $"/rest/v1/runs?id=eq.{gid}";
        var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = JsonContent.Create(payload)
        };

        SetAuthHeader(req);
        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var content = await resp.Content.ReadAsStringAsync();
            return (false, $"Update run failed: {resp.StatusCode}: {content}");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteRunAsync(Guid id)
    {
        if (string.IsNullOrEmpty(_auth.UserId))
            return (false, "User not signed in");

        var url = $"/rest/v1/runs?id=eq.{id}";
        var req = new HttpRequestMessage(HttpMethod.Delete, url);
        SetAuthHeader(req);
        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var content = await resp.Content.ReadAsStringAsync();
            return (false, $"Delete run failed: {resp.StatusCode}: {content}");
        }

        return (true, null);
    }
}
