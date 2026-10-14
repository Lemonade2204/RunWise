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

    public async Task<(bool Success, string? Error)> SendPasswordResetAsync(string email, string? redirectTo = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, "Email is required");

        object payload = string.IsNullOrEmpty(redirectTo)
            ? new { email }
            : new { email, redirect_to = redirectTo };

        var resp = await _http.PostAsJsonAsync("/auth/v1/recover", payload);
        var content = await resp.Content.ReadAsStringAsync();

        if (resp.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("msg", out var msg))
                return (false, msg.GetString());
            if (doc.RootElement.TryGetProperty("error_description", out var ed))
                return (false, ed.GetString());
            if (doc.RootElement.TryGetProperty("error", out var err))
                return (false, err.GetString());
        }
        catch { }

        return (false, $"Password reset request failed: {resp.StatusCode}: {content}");
    }

    public async Task<(bool Success, string? Error)> CreateProfileAsync(string email, string? username = null, string? gender = null)
    {
        if (string.IsNullOrEmpty(UserId))
            return (false, "UserId is not available");

        // include gender if provided to create the profile row with gender column
        if (!string.IsNullOrWhiteSpace(gender))
        {
            var payloadWithGender = new { id = UserId, email, username, gender };
            var req2 = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/profiles")
            {
                Content = JsonContent.Create(payloadWithGender)
            };

            if (!string.IsNullOrEmpty(AccessToken))
                req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            else
                req2.Headers.Add("apikey", _apiKey);

            req2.Headers.Add("Accept", "application/json");

            var resp2 = await _http.SendAsync(req2);
            if (!resp2.IsSuccessStatusCode)
            {
                var content2 = await resp2.Content.ReadAsStringAsync();
                return (false, $"Create profile failed: {resp2.StatusCode}: {content2}");
            }

            return (true, null);
        }

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
            // PATCH PLACEHOLDER: context retained for future automated inserts
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
           

            // optional extras: date of birth (DB may use dateofbirth or date_of_birth) and gender
            if (el.TryGetProperty("dateofbirth", out var dobEl) || el.TryGetProperty("dateofbirth", out dobEl))
            {
                if (dobEl.ValueKind == JsonValueKind.String && DateTime.TryParse(dobEl.GetString(), out var dt))
                    profile.DateOfBirth = dt;
                else if (dobEl.ValueKind == JsonValueKind.Number && dobEl.TryGetInt64(out var unix))
                    profile.DateOfBirth = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            }
            if (el.TryGetProperty("gender", out var gEl))
            {
                if (gEl.ValueKind == JsonValueKind.String)
                    profile.Gender = gEl.GetString();
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

        // Build an update payload that matches your current profiles table schema.
        // Current columns: username, weight_kg, height_cm, dateofbirth, gender
        var payload = new Dictionary<string, object?>();
        payload["username"] = profile.Username;
        if (profile.WeightKg.HasValue) payload["weight_kg"] = profile.WeightKg.Value;
        if (profile.HeightCm.HasValue) payload["height_cm"] = profile.HeightCm.Value;
        if (profile.DateOfBirth.HasValue) payload["dateofbirth"] = profile.DateOfBirth.Value;
        if (!string.IsNullOrWhiteSpace(profile.Gender)) payload["gender"] = profile.Gender;

        var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"/rest/v1/profiles?id=eq.{UserId}")
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
