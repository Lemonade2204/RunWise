using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RunWise.Models;

namespace RunWise.Services
{
    // CalorieApiService: queries an external calorie API (calorieapi.com) to find food items.
    // This implementation uses the provided API key (environment variable CALORIEAPI_KEY or a fallback)
    // and attempts a few common search endpoints. It parses results heuristically to extract
    // title, calories, nutrients, image, and measurement where available.
    public class CalorieApiService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        // A small set of Filipino search terms used to seed searches when no search term is provided.
        private static readonly string[] FilipinoDishes = new[]
        {
            "adobo", "sinigang","Boiled egg", "KARE-KARE PEANUT SAUCE", "Boiled egg", "pancit", "lumpia", "sisig", "longganisa", "bangus",
            "pork blood", "menudo", "caldereta", "bulalo", "tinola", "banana", "Fried egg", "white Rice",
            "palabok", "ginataang gulay", "pork belly", "Tilapia",
            "giniling", "chicken inasal", "afritada",
            "eggplant", "Chicken Cooked in Coconut Milk", "tinapa"    
        };

        // Fallback API key: use the key the user provided; prefer environment variable if set.
        private const string FallbackApiKey = "";

        public CalorieApiService(HttpClient http)
        {
            _http = http;
            var env = Environment.GetEnvironmentVariable("CALORIEAPI_KEY");
            _apiKey = string.IsNullOrWhiteSpace(env) ? FallbackApiKey : env;

            // Add required header for calorieapiadmin.com: X-API-Key
            if (!_http.DefaultRequestHeaders.Contains("X-API-Key") && !_http.DefaultRequestHeaders.Contains("x-api-key"))
                _http.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }

        public async Task<List<FoodItem>> GetCandidatesAsync(int maxCalories, int number = 8, string searchTerm = "")
        {
            var results = new List<FoodItem>();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                results = await SearchApiAsync(searchTerm, Math.Clamp(number, 1, 30));
            }
            else
            {
                // Query a small set of Filipino terms sequentially to avoid API rate limits
                var termsToQuery = FilipinoDishes.Take(6).ToList();
                var perTerm = Math.Max(3, number / Math.Max(1, termsToQuery.Count));

                foreach (var term in termsToQuery)
                {
                    try
                    {
                        var batch = await SearchApiAsync(term, perTerm);
                        if (batch != null && batch.Count > 0)
                        {
                            results.AddRange(batch);
                        }
                        // small delay to be gentle with the API
                        await Task.Delay(150);
                    }
                    catch
                    {
                        // ignore individual term failures and continue
                    }
                }
            }

            // Deduplicate by title
            var dedup = results
                .Where(f => !string.IsNullOrWhiteSpace(f.Title) && f.Calories > 0)
                .GroupBy(f => f.Title.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            // If no results, return empty list (no built-in fallback)
            if (!dedup.Any()) return new List<FoodItem>();

            // Order by closeness to requested calories
            var ordered = dedup.OrderBy(f => Math.Abs(f.Calories - maxCalories)).Take(number).ToList();
            return ordered;
        }

        private async Task<List<FoodItem>> SearchApiAsync(string query, int maxResults)
        {
            var outList = new List<FoodItem>();

            // Prefer the calorieapiadmin.com search endpoint; include a couple fallback patterns.
            var paths = new[]
            {
                $"api/v1/search/foods?q={Uri.EscapeDataString(query)}",
                $"api/v1/foods/search?q={Uri.EscapeDataString(query)}",
                $"api/foods?query={Uri.EscapeDataString(query)}",
                $"api/search?query={Uri.EscapeDataString(query)}",
                $"search?query={Uri.EscapeDataString(query)}"
            };

            foreach (var path in paths)
            {
                try
                {
                    // Some endpoints may require absolute URI — try relative first (BaseAddress should be set),
                    // otherwise try full calorieapiadmin.com URL.
                    HttpResponseMessage res;
                    try
                    {
                        res = await _http.GetAsync(path);
                    }
                    catch
                    {
                        // try full domain
                        var full = $"https://calorieapiadmin.com/{path}".Replace("//", "/").Replace("https:/", "https://");
                        res = await _http.GetAsync(full);
                    }
                    if (!res.IsSuccessStatusCode) continue;
                    var txt = await res.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(txt)) continue;

                    using var doc = JsonDocument.Parse(txt);
                    var root = doc.RootElement;

                    // The documented response contains a `data` array of foods
                    JsonElement[] candidates = Array.Empty<JsonElement>();
                    if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        candidates = data.EnumerateArray().ToArray();
                    }
                    else if (root.ValueKind == JsonValueKind.Array)
                    {
                        candidates = root.EnumerateArray().ToArray();
                    }
                    else
                    {
                        // fallback to trying root as a single item
                        candidates = new[] { root };
                    }

                    foreach (var c in candidates)
                    {
                        // According to docs: fields include id, name, brand_name, calories_100g, meal, default_portion
                        var name = TryGetString(c, new[] { "name", "title" }) ?? query;

                        // calories: prefer meal.calories (already scaled to default portion) else calculate from calories_100g and default_portion.grams
                        int? calories = null;
                        if (c.ValueKind == JsonValueKind.Object)
                        {
                            if (c.TryGetProperty("meal", out var meal) && meal.ValueKind == JsonValueKind.Object && meal.TryGetProperty("calories", out var mealCal) && mealCal.ValueKind == JsonValueKind.Number && mealCal.TryGetInt32(out var mc))
                            {
                                calories = mc;
                            }
                            else if (c.TryGetProperty("calories_100g", out var cal100) && (cal100.ValueKind == JsonValueKind.Number || cal100.ValueKind == JsonValueKind.String))
                            {
                                double calPer100 = 0;
                                if (cal100.ValueKind == JsonValueKind.Number) calPer100 = cal100.GetDouble();
                                else if (cal100.ValueKind == JsonValueKind.String && double.TryParse(cal100.GetString(), out var d)) calPer100 = d;

                                // default portion grams
                                double grams = 100;
                                if (c.TryGetProperty("default_portion", out var defPort) && defPort.ValueKind == JsonValueKind.Object && defPort.TryGetProperty("grams", out var gramsProp) && gramsProp.ValueKind == JsonValueKind.Number)
                                {
                                    grams = gramsProp.GetDouble();
                                }

                                var calc = (int)Math.Round(calPer100 * grams / 100.0);
                                calories = calc;
                            }
                        }

                        if (calories == null) continue; // skip entries without usable calorie info

                        // measurement: prefer default_portion.label else serving_size + serving_unit
                        string measurement = string.Empty;
                        if (c.TryGetProperty("default_portion", out var dp) && dp.ValueKind == JsonValueKind.Object && dp.TryGetProperty("label", out var lbl) && lbl.ValueKind == JsonValueKind.String)
                        {
                            measurement = lbl.GetString() ?? string.Empty;
                        }
                        else if (c.TryGetProperty("serving_size", out var ss) && c.TryGetProperty("serving_unit", out var su) && (ss.ValueKind == JsonValueKind.Number || ss.ValueKind == JsonValueKind.String))
                        {
                            var s = ss.ValueKind == JsonValueKind.Number ? ss.GetRawText() : ss.GetString();
                            var u = su.ValueKind == JsonValueKind.String ? su.GetString() : string.Empty;
                            measurement = $"{s} {u}".Trim();
                        }

                        // Nutrients summary: try meal.macros or per-100g macros
                        string nutrients = string.Empty;
                        if (c.TryGetProperty("meal", out var mealObj) && mealObj.ValueKind == JsonValueKind.Object && mealObj.TryGetProperty("macros", out var macros) && macros.ValueKind == JsonValueKind.Object)
                        {
                            var parts = new List<string>();
                            if (macros.TryGetProperty("protein", out var p) && TryGetNumericString(p, out var pStr)) parts.Add($"Protein: {pStr}g");
                            if (macros.TryGetProperty("carbs", out var ca) && TryGetNumericString(ca, out var caStr)) parts.Add($"Carbs: {caStr}g");
                            if (macros.TryGetProperty("fat", out var f) && TryGetNumericString(f, out var fStr)) parts.Add($"Fat: {fStr}g");
                            nutrients = string.Join(" • ", parts);
                        }
                        else
                        {
                            var parts = new List<string>();
                            if (c.TryGetProperty("protein_100g", out var p2) && TryGetNumericString(p2, out var p2s)) parts.Add($"Protein: {p2s}g/100g");
                            if (c.TryGetProperty("carbs_100g", out var ca2) && TryGetNumericString(ca2, out var ca2s)) parts.Add($"Carbs: {ca2s}g/100g");
                            if (c.TryGetProperty("fat_100g", out var f2) && TryGetNumericString(f2, out var f2s)) parts.Add($"Fat: {f2s}g/100g");
                            nutrients = string.Join(" • ", parts);
                        }

                        var item = new FoodItem
                        {
                            Title = name,
                            Calories = calories.Value,
                            Description = TryGetString(c, new[] { "brand_name", "category", "food_type", "description" }) ?? string.Empty,
                            ImageUrl = string.Empty,
                            NutrientsSummary = nutrients,
                            Measurement = measurement
                        };

                        outList.Add(item);
                        if (outList.Count >= maxResults) break;
                    }

                    if (outList.Any()) break; // stop trying other paths once we have results
                }
                catch
                {
                    // ignore and try next path
                }
            }

            return outList;
        }

        private static string? TryGetString(JsonElement el, string[] keys)
        {
            foreach (var k in keys)
            {
                if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(k, out var prop) && prop.ValueKind == JsonValueKind.String)
                    return prop.GetString();
            }
            return null;
        }

        private static int? TryGetInt(JsonElement el, string[] keys)
        {
            foreach (var k in keys)
            {
                if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(k, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var v)) return v;
                    if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var sv)) return sv;
                }
            }
            return null;
        }

        private static string BuildNutrientsSummary(JsonElement el)
        {
            var parts = new List<string>();
            if (el.ValueKind == JsonValueKind.Object)
            {
                if (el.TryGetProperty("protein", out var p) && TryGetNumericString(p, out var pStr)) parts.Add($"Protein: {pStr}");
                if (el.TryGetProperty("fat", out var f) && TryGetNumericString(f, out var fStr)) parts.Add($"Fat: {fStr}");
                if (el.TryGetProperty("carbohydrates", out var c) && TryGetNumericString(c, out var cStr)) parts.Add($"Carbs: {cStr}");
                if (el.TryGetProperty("carb", out var c2) && TryGetNumericString(c2, out var c2Str)) parts.Add($"Carbs: {c2Str}");
            }
            return string.Join(" • ", parts);
        }

        private static bool TryGetNumericString(JsonElement el, out string value)
        {
            if (el.ValueKind == JsonValueKind.Number)
            {
                value = el.GetRawText();
                return true;
            }
            if (el.ValueKind == JsonValueKind.String)
            {
                value = el.GetString() ?? string.Empty;
                return !string.IsNullOrEmpty(value);
            }
            value = string.Empty;
            return false;
        }
    }
}
