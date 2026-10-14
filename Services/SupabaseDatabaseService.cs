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
            steps = run.Steps,
            calories = run.Calories,
            duration_seconds = run.DurationSeconds,
            // use datecreated column (some schemas use datecreated rather than created_at)
            datecreated = run.CreatedAt
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

    // Meals CRUD
    public async Task<(bool Success, string? Error)> CreateMealAsync(RunWise.Models.MealItem meal)
    {
        if (string.IsNullOrEmpty(_auth.UserId))
            return (false, "User not signed in");
        // Map local MealItem to Supabase 'meals' table columns
        var payload = new
        {
            id = meal.Id,
            user_id = Guid.Parse(_auth.UserId),
            food_name = meal.Name,
            calories = meal.Calories,
            servings = meal.Quantity,
            serving_size = (string?)null,
            meal_type = meal.Type.ToString().ToLowerInvariant(),
            meal_date = meal.Date.ToString("yyyy-MM-dd"),
            created_at = DateTime.UtcNow
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/meals")
        {
            Content = JsonContent.Create(payload)
        };

        SetAuthHeader(req);
        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var content = await resp.Content.ReadAsStringAsync();
            return (false, $"Create meal failed: {resp.StatusCode}: {content}");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateMealAsync(RunWise.Models.MealItem meal)
    {
        if (string.IsNullOrEmpty(_auth.UserId))
            return (false, "User not signed in");
        var totalCalories = (int)(meal.Calories * (int)Math.Max(1, Math.Round(meal.Quantity)));
        var payload = new
        {
            food_name = meal.Name,
            calories = meal.Calories,
            servings = meal.Quantity,
            serving_size = (string?)null,
            meal_type = meal.Type.ToString().ToLowerInvariant(),
            meal_date = meal.Date.ToString("yyyy-MM-dd")
        };

        var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"/rest/v1/meals?id=eq.{meal.Id}")
        {
            Content = JsonContent.Create(payload)
        };

        SetAuthHeader(req);
        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var content = await resp.Content.ReadAsStringAsync();
            return (false, $"Update meal failed: {resp.StatusCode}: {content}");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteMealAsync(RunWise.Models.MealItem meal)
    {
        if (string.IsNullOrEmpty(_auth.UserId))
            return (false, "User not signed in");

        var req = new HttpRequestMessage(HttpMethod.Delete, $"/rest/v1/meals?id=eq.{meal.Id}");
        SetAuthHeader(req);
        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var content = await resp.Content.ReadAsStringAsync();
            return (false, $"Delete meal failed: {resp.StatusCode}: {content}");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error, List<RunWise.Models.MealItem>? Meals)> GetMealsForUserDateAsync(DateTime date)
    {
        if (string.IsNullOrEmpty(_auth.UserId))
            return (false, "User not signed in", null);
        var url = $"/rest/v1/meals?select=*&user_id=eq.{_auth.UserId}&meal_date=eq.{date:yyyy-MM-dd}";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetAuthHeader(req);
        req.Headers.Add("Accept", "application/json");
        var resp = await _http.SendAsync(req);
        var content = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return (false, $"Get meals failed: {resp.StatusCode}: {content}", null);

        try
        {
            var meals = new List<RunWise.Models.MealItem>();
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return (false, "Unexpected response format", null);

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                try
                {
                    var m = new RunWise.Models.MealItem();
                    if (el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String && Guid.TryParse(idEl.GetString(), out var gid))
                        m.Id = gid;
                    if (el.TryGetProperty("food_name", out var nameEl))
                        m.Name = nameEl.GetString() ?? string.Empty;
                    if (el.TryGetProperty("calories", out var calEl))
                    {
                        // calories may be returned as a number or as a string (possibly decimal)
                        if (calEl.ValueKind == JsonValueKind.Number)
                        {
                            if (calEl.TryGetInt32(out var ical)) m.Calories = ical;
                            else if (calEl.TryGetDouble(out var dcal)) m.Calories = (int)Math.Round(dcal);
                        }
                        else if (calEl.ValueKind == JsonValueKind.String)
                        {
                            var s = calEl.GetString();
                            if (!string.IsNullOrEmpty(s))
                            {
                                if (int.TryParse(s, out var ical2)) m.Calories = ical2;
                                else if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dcal2))
                                    m.Calories = (int)Math.Round(dcal2);
                            }
                        }
                    }
                    if (el.TryGetProperty("meal_type", out var mtEl) && mtEl.ValueKind == JsonValueKind.String)
                    {
                        var mtStr = mtEl.GetString();
                        if (!string.IsNullOrEmpty(mtStr) && Enum.TryParse<RunWise.Models.MealType>(mtStr, true, out var mt))
                            m.Type = mt;
                    }
                    if (el.TryGetProperty("servings", out var qEl))
                    {
                        // servings may be numeric or string
                        if (qEl.ValueKind == JsonValueKind.Number && qEl.TryGetDouble(out var dq))
                            m.Quantity = dq;
                        else if (qEl.ValueKind == JsonValueKind.String && double.TryParse(qEl.GetString(), out var dq2))
                            m.Quantity = dq2;
                    }
                    if (el.TryGetProperty("meal_date", out var dEl) && dEl.ValueKind == JsonValueKind.String && DateTime.TryParse(dEl.GetString(), out var dd))
                        m.Date = dd.Date;
                    // serving_size and total_calories exist in DB but MealItem has no explicit fields; ignore or place in Notes
                    if (el.TryGetProperty("serving_size", out var ssEl))
                        m.Notes = ssEl.GetString();
                    // read snake_case total_calories if present, else try camelCase totalCalories
                    if (el.TryGetProperty("total_calories", out var totSnake) && totSnake.TryGetInt32(out var tval2))
                    {
                        if (string.IsNullOrEmpty(m.Notes)) m.Notes = $"Total: {tval2} kcal";
                        else m.Notes += $"; Total: {tval2} kcal";
                    }
                    else if (el.TryGetProperty("totalCalories", out var totEl) && totEl.TryGetInt32(out var tval))
                    {
                        if (string.IsNullOrEmpty(m.Notes)) m.Notes = $"Total: {tval} kcal";
                        else m.Notes += $"; Total: {tval} kcal";
                    }

                    meals.Add(m);
                }
                catch { }
            }

            return (true, null, meals);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    // Upsert daily calorie summary for current user
    public async Task<(bool Success, string? Error)> UpsertCalorieSummaryAsync(DateTime date, int totalCalories)
    {
        if (string.IsNullOrEmpty(_auth.UserId))
            return (false, "User not signed in");

        // Try to find existing summary row for (user_id, date)
        var url = $"/rest/v1/calorie_summaries?select=*&user_id=eq.{_auth.UserId}&date=eq.{date:yyyy-MM-dd}";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetAuthHeader(req);
        req.Headers.Add("Accept", "application/json");
        var resp = await _http.SendAsync(req);
        var content = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return (false, $"Get summary failed: {resp.StatusCode}: {content}");

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                // update first matching row
                var first = doc.RootElement.EnumerateArray().First();
                if (first.TryGetProperty("id", out var idEl))
                {
                    var idVal = idEl.GetRawText().Trim('"');
                    var payload = new { total_calories = totalCalories };
                    var patchReq = new HttpRequestMessage(new HttpMethod("PATCH"), $"/rest/v1/calorie_summaries?id=eq.{idVal}")
                    {
                        Content = JsonContent.Create(payload)
                    };
                    SetAuthHeader(patchReq);
                    patchReq.Headers.Add("Accept", "application/json");
                    var patchResp = await _http.SendAsync(patchReq);
                    var patchContent = await patchResp.Content.ReadAsStringAsync();
                    if (!patchResp.IsSuccessStatusCode)
                        return (false, $"Update summary failed: {patchResp.StatusCode}: {patchContent}");
                    return (true, null);
                }
            }

            // Not found - create new
            var payload2 = new
            {
                user_id = Guid.Parse(_auth.UserId),
                date = date.ToString("yyyy-MM-dd"),
                total_calories = totalCalories
            };

            var postReq = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/calorie_summaries")
            {
                Content = JsonContent.Create(payload2)
            };
            SetAuthHeader(postReq);
            postReq.Headers.Add("Accept", "application/json");
            var postResp = await _http.SendAsync(postReq);
            var postContent = await postResp.Content.ReadAsStringAsync();
            if (!postResp.IsSuccessStatusCode)
                return (false, $"Create summary failed: {postResp.StatusCode}: {postContent}");

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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
            steps = run.Steps,
            calories = run.Calories,
            duration_seconds = (int)run.Duration.TotalSeconds,
            // prefer datecreated column; avoid created_at if not present in schema
            datecreated = run.StartTime,
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
        var url = $"/rest/v1/runs?select=*&user_id=eq.{_auth.UserId}&order=datecreated.desc";
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

                    // try created_at first, fall back to datecreated if schema uses that
                    if (el.TryGetProperty("created_at", out var caEl))
                    {
                        if (caEl.ValueKind == JsonValueKind.String && DateTime.TryParse(caEl.GetString(), out var dt))
                            run.CreatedAt = dt;
                    }
                    else if (el.TryGetProperty("datecreated", out var dcEl))
                    {
                        if (dcEl.ValueKind == JsonValueKind.String && DateTime.TryParse(dcEl.GetString(), out var dt2))
                            run.CreatedAt = dt2;
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

                    // calories (int) and steps (int) may be returned as numbers or strings
                    if (el.TryGetProperty("calories", out var calEl))
                    {
                        if (calEl.ValueKind == JsonValueKind.Number && calEl.TryGetInt32(out var cval))
                            run.Calories = cval;
                        else if (calEl.ValueKind == JsonValueKind.String && int.TryParse(calEl.GetString(), out var cstr))
                            run.Calories = cstr;
                    }

                    if (el.TryGetProperty("steps", out var stEl))
                    {
                        if (stEl.ValueKind == JsonValueKind.Number && stEl.TryGetInt32(out var sval))
                            run.Steps = sval;
                        else if (stEl.ValueKind == JsonValueKind.String && int.TryParse(stEl.GetString(), out var sstr))
                            run.Steps = sstr;
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
            // prefer datecreated column; avoid created_at if not present in schema
            datecreated = run.StartTime,
            steps = run.Steps,
            calories = run.Calories,
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
