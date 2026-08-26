using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using RunWise.Models;

namespace RunWise.Services;

public class SupabaseAuthService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? UserId { get; private set; }

    public SupabaseAuthService(string supabaseUrl, string anonKey)
    {
        _apiKey = anonKey ?? throw new ArgumentNullException(nameof(anonKey));
        _http = new HttpClient { BaseAddress = new Uri(supabaseUrl ?? throw new ArgumentNullException(nameof(supabaseUrl))) };

        _http.DefaultRequestHeaders.Add("apikey", _apiKey);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<(bool Success, string? Error, string? AccessToken)> SignUpAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are required", null);

        var payload = new { email, password };
        var resp = await _http.PostAsJsonAsync("/auth/v1/signup", payload);
        var content = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            try
            {
                var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("msg", out var msg))
                    return (false, msg.GetString(), null);
                if (doc.RootElement.TryGetProperty("error_description", out var ed))
                    return (false, ed.GetString(), null);
            }
            catch { }
            return (false, $"Sign up failed: {resp.StatusCode}", null);
        }

        try
        {
            var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("access_token", out var tok))
                AccessToken = tok.GetString();

            if (doc.RootElement.TryGetProperty("refresh_token", out var rtok))
                RefreshToken = rtok.GetString();

            if (doc.RootElement.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
            {
                if (user.TryGetProperty("id", out var id))
                    UserId = id.GetString();
            }

            Console.WriteLine($"SignUp: userId={UserId} accessSet={AccessToken != null} refreshSet={RefreshToken != null}");

            return (true, null, AccessToken);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to parse sign up response: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string? Error, string? AccessToken)> SignInAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are required", null);

        // Supabase token endpoint: /auth/v1/token?grant_type=password
        var resp = await _http.PostAsJsonAsync("/auth/v1/token?grant_type=password", new { email, password });
        var content = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            try
            {
                var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("error_description", out var ed))
                    return (false, ed.GetString(), null);
                if (doc.RootElement.TryGetProperty("msg", out var msg))
                    return (false, msg.GetString(), null);
            }
            catch { }
            return (false, $"Sign in failed: {resp.StatusCode}", null);
        }

        try
        {
            var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("access_token", out var tok))
                AccessToken = tok.GetString();

            if (doc.RootElement.TryGetProperty("refresh_token", out var rtok))
                RefreshToken = rtok.GetString();

            if (doc.RootElement.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
            {
                if (user.TryGetProperty("id", out var id))
                    UserId = id.GetString();
            }

            Console.WriteLine($"SignIn: userId={UserId} accessSet={AccessToken != null} refreshSet={RefreshToken != null}");

            return (true, null, AccessToken);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to parse sign in response: {ex.Message}", null);
        }
    }

    public Task SignOutAsync()
    {
        AccessToken = null;
        RefreshToken = null;
        UserId = null;
        return Task.CompletedTask;
    }

    public async Task<(bool Success, string? Error)> CreateProfileAsync(string email, string? username = null)
    {
        if (string.IsNullOrEmpty(UserId))
            return (false, "UserId is not available");

        var payload = new { id = UserId, email, username };

        var req = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/profiles")
        {
            Content = JsonContent.Create(payload)
        };

        // Use the user's access token for auth so RLS policies based on auth.uid() work
        if (!string.IsNullOrEmpty(AccessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        else
            req.Headers.Add("apikey", _apiKey);

        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var content = await resp.Content.ReadAsStringAsync();
            return (false, $"Create profile failed: {resp.StatusCode}: {content}");
        }

        return (true, null);
    }

    public async Task<(bool Success, string? Error, Profile? Profile)> GetProfileAsync()
    {
        if (string.IsNullOrEmpty(UserId))
            return (false, "UserId is not available", null);

        var url = $"/rest/v1/profiles?select=*&id=eq.{UserId}";
        var req = new HttpRequestMessage(HttpMethod.Get, url);

        // Use the user's access token for auth if available
        if (!string.IsNullOrEmpty(AccessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        else
            req.Headers.Add("apikey", _apiKey);

        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        var content = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return (false, $"Get profile failed: {resp.StatusCode}: {content}", null);

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return (false, "Profile not found", null);

            var el = doc.RootElement[0];
            var profile = new Profile();
            if (el.TryGetProperty("id", out var idEl))
                profile.Id = idEl.GetString();
            if (el.TryGetProperty("email", out var emailEl))
                profile.Email = emailEl.GetString();
            if (el.TryGetProperty("username", out var userEl))
                profile.Username = userEl.GetString();
            if (el.TryGetProperty("weight_kg", out var wEl))
            {
                if (wEl.ValueKind == JsonValueKind.Number && wEl.TryGetDouble(out var dv))
                    profile.WeightKg = dv;
                else if (wEl.ValueKind == JsonValueKind.String && double.TryParse(wEl.GetString(), out var dv2))
                    profile.WeightKg = dv2;
            }
            if (el.TryGetProperty("height_cm", out var hEl))
            {
                if (hEl.ValueKind == JsonValueKind.Number && hEl.TryGetDouble(out var dv))
                    profile.HeightCm = dv;
                else if (hEl.ValueKind == JsonValueKind.String && double.TryParse(hEl.GetString(), out var dv2))
                    profile.HeightCm = dv2;
            }
            if (el.TryGetProperty("weekly_avg_distance_km", out var wadEl))
            {
                if (wadEl.ValueKind == JsonValueKind.Number && wadEl.TryGetDouble(out var dv))
                    profile.WeeklyAverageDistanceKm = dv;
                else if (wadEl.ValueKind == JsonValueKind.String && double.TryParse(wadEl.GetString(), out var dv2))
                    profile.WeeklyAverageDistanceKm = dv2;
            }
            if (el.TryGetProperty("weekly_avg_time_seconds", out var watsEl))
            {
                if (watsEl.ValueKind == JsonValueKind.Number && watsEl.TryGetInt32(out var ival))
                    profile.WeeklyAverageTimeSeconds = ival;
                else if (watsEl.ValueKind == JsonValueKind.String && int.TryParse(watsEl.GetString(), out var ival2))
                    profile.WeeklyAverageTimeSeconds = ival2;
            }
            if (el.TryGetProperty("all_time_distance_km", out var atdEl))
            {
                if (atdEl.ValueKind == JsonValueKind.Number && atdEl.TryGetDouble(out var dv))
                    profile.AllTimeDistanceKm = dv;
                else if (atdEl.ValueKind == JsonValueKind.String && double.TryParse(atdEl.GetString(), out var dv2))
                    profile.AllTimeDistanceKm = dv2;
            }
            if (el.TryGetProperty("all_time_time_seconds", out var attsEl))
            {
                if (attsEl.ValueKind == JsonValueKind.Number && attsEl.TryGetInt32(out var ival))
                    profile.AllTimeTimeSeconds = ival;
                else if (attsEl.ValueKind == JsonValueKind.String && int.TryParse(attsEl.GetString(), out var ival2))
                    profile.AllTimeTimeSeconds = ival2;
            }

            return (true, null, profile);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to parse profile: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string? Error)> UpdateProfileAsync(Profile profile)
    {
        if (string.IsNullOrEmpty(UserId))
            return (false, "UserId is not available");

        var payload = new Dictionary<string, object?>()
        {
            ["username"] = profile.Username,
            ["weight_kg"] = profile.WeightKg,
            ["height_cm"] = profile.HeightCm,
            ["weekly_avg_distance_km"] = profile.WeeklyAverageDistanceKm,
            ["weekly_avg_time_seconds"] = profile.WeeklyAverageTimeSeconds,
            ["all_time_distance_km"] = profile.AllTimeDistanceKm,
            ["all_time_time_seconds"] = profile.AllTimeTimeSeconds
        };

        var req = new HttpRequestMessage(HttpMethod.Patch, $"/rest/v1/profiles?id=eq.{UserId}")
        {
            Content = JsonContent.Create(payload)
        };

        if (!string.IsNullOrEmpty(AccessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        else
            req.Headers.Add("apikey", _apiKey);

        req.Headers.Add("Accept", "application/json");

        var resp = await _http.SendAsync(req);
        var content = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return (false, $"Update profile failed: {resp.StatusCode}: {content}");

        return (true, null);
    }
}
//using System;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Net.Http.Json;
//using System.Text.Json;
//using System.Threading.Tasks;

//namespace RunWise.Services;

//    public class SupabaseAuthService
//{
//    private readonly HttpClient _http;
//    private readonly string _apiKey;

//    public string? AccessToken { get; private set; }
//    public string? RefreshToken { get; private set; }
//    public string? UserId { get; private set; }


//    public SupabaseAuthService(string supabaseUrl, string anonKey)
//    {
//        _apiKey = anonKey ?? throw new ArgumentNullException(nameof(anonKey));
//        _http = new HttpClient { BaseAddress = new Uri(supabaseUrl ?? throw new ArgumentNullException(nameof(supabaseUrl))) };

//        _http.DefaultRequestHeaders.Add("apikey", _apiKey);
//        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
//    }

//    public async Task<(bool Success, string? Error, string? AccessToken)> SignUpAsync(string email, string password)
//    {
//        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
//            return (false, "Email and password are required", null);

//        var payload = new { email, password };
//        var resp = await _http.PostAsJsonAsync("/auth/v1/signup", payload);
//        var content = await resp.Content.ReadAsStringAsync();

//        if (!resp.IsSuccessStatusCode)
//        {
//            try
//            {
//                var doc = JsonDocument.Parse(content);
//                if (doc.RootElement.TryGetProperty("msg", out var msg))
//                    return (false, msg.GetString(), null);
//                if (doc.RootElement.TryGetProperty("error_description", out var ed))
//                    return (false, ed.GetString(), null);
//            }
//            catch { }
//            return (false, $"Sign up failed: {resp.StatusCode}", null);
//        }

//        try
//        {
//            var doc = JsonDocument.Parse(content);
//            if (doc.RootElement.TryGetProperty("access_token", out var tok))
//            {
//                AccessToken = tok.GetString();
//            }
//            if (doc.RootElement.TryGetProperty("refresh_token", out var rtok))
//            {
//                RefreshToken = rtok.GetString();
//            }
//            if (doc.RootElement.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
//            {
//                if (user.TryGetProperty("id", out var id))
//                    UserId = id.GetString();
//            }

//            try
//            {
//                Console.WriteLine($"SignUp: userId={UserId} accessSet={AccessToken != null} refreshSet={RefreshToken != null}");
//            }
//            catch { }

//            return (true, null, AccessToken);
//        }
//        catch { }


//            try
//            {
//                Console.WriteLine($"SignIn: userId={UserId} accessSet={AccessToken != null} refreshSet={RefreshToken != null}");
//            }
//            catch { }

//            return (true, null, AccessToken);
//    }

//    public async Task<(bool Success, string? Error, string? AccessToken)> SignInAsync(string email, string password)
//    {
//        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
//            return (false, "Email and password are required", null);

//        // Supabase token endpoint: /auth/v1/token?grant_type=password
//        var resp = await _http.PostAsJsonAsync("/auth/v1/token?grant_type=password", new { email, password });
//        var content = await resp.Content.ReadAsStringAsync();

//        if (!resp.IsSuccessStatusCode)
//        {
//            try
//            {
//                var doc = JsonDocument.Parse(content);
//                if (doc.RootElement.TryGetProperty("error_description", out var ed))
//                    return (false, ed.GetString(), null);
//                if (doc.RootElement.TryGetProperty("msg", out var msg))
//                    return (false, msg.GetString(), null);
//            }
//            catch { }
//            return (false, $"Sign in failed: {resp.StatusCode}", null);
//        }

//        try
//        {
//            var doc = JsonDocument.Parse(content);
//            if (doc.RootElement.TryGetProperty("access_token", out var tok))
//            {
//                AccessToken = tok.GetString();
//            }
//            if (doc.RootElement.TryGetProperty("refresh_token", out var rtok))
//            {
//                RefreshToken = rtok.GetString();
//            }
//            if (doc.RootElement.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
//            {
//                if (user.TryGetProperty("id", out var id))
//                    UserId = id.GetString();
//            }

//            return (true, null, AccessToken);
//        }
//        catch { }

//        return (true, null, AccessToken);
//    }

//    public Task SignOutAsync()
//    {
//        AccessToken = null;
//        RefreshToken = null;
//        UserId = null;
//        return Task.CompletedTask;
//    }

//    public async Task<(bool Success, string? Error)> CreateProfileAsync(string email, string? username = null)
//    {
//        if (string.IsNullOrEmpty(UserId))
//            return (false, "UserId is not available");

//        var payload = new { id = UserId, email, username };

//        var req = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/profiles")
//        {
//            Content = JsonContent.Create(payload)
//        };

//        // Use the user's access token for auth so RLS policies based on auth.uid() work
//        if (!string.IsNullOrEmpty(AccessToken))
//            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
//        else
//            req.Headers.Add("apikey", _apiKey);

//        req.Headers.Add("Accept", "application/json");

//        var resp = await _http.SendAsync(req);
//        if (!resp.IsSuccessStatusCode)
//        {
//            var content = await resp.Content.ReadAsStringAsync();
//            return (false, $"Create profile failed: {resp.StatusCode}: {content}");
//        }

//        return (true, null);
//    }
//}
