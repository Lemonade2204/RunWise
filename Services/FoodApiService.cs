using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using RunWise.Models;

namespace RunWise.Services
{

    public class FoodApiService
    {
        private readonly HttpClient _http;
        private const string ApiKey = "UGZqG62YVxqUf9y0TFeeHq9HEhhyfPV4eofzWdEh";

        // Curated list of ingredients/dishes commonly eaten and bought in the Philippines.
        private static readonly string[] FilipinoTerms = new[]
        {
            "rice", "pork", "chicken","chicken breast", "adobo", "tilapia", "bangus",
            "malunggay", "kangkong", "ampalaya", "mango", "calamansi", "boiled eggs", "eggs",
            "coconut milk", "soy sauce", "vinegar", "garlic", "ginger",
            "longganisa", "tocino", "lumpia", "pancit", "sinigang", "Fries", "bacon", "yakult",
            "kare-kare", "banana", "eggplant", "tofu", "tenderjuicy hotdog", "spaghetti", "balut"
        };

        public FoodApiService(HttpClient http)
        {
            _http = http;
        }

        // Fetch candidate foods from FDC. Uses /fdc/v1/foods/search (POST).
        public async Task<List<FoodItem>> GetCandidatesAsync(int maxCalories, int number = 8, string searchTerm = "")
        {
            List<FoodItem> results;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                
                results = await SearchOnceAsync(searchTerm, number);
            }
            else
            {
                // No term given: pull from several curated Filipino terms and merge.
                var rnd = new Random();
                var termsToUse = FilipinoTerms.OrderBy(_ => rnd.Next()).Take(5).ToList();

                var perTermCount = Math.Max(3, number / termsToUse.Count);
                var merged = new List<FoodItem>();

                foreach (var term in termsToUse)
                {
                    var batch = await SearchOnceAsync(term, perTermCount);
                    merged.AddRange(batch);
                }

                // Dedupe by title (case-insensitive) and drop items with no calorie data.
                results = merged
                    .Where(f => f.Calories > 0)
                    .GroupBy(f => f.Title.Trim().ToLowerInvariant())
                    .Select(g => g.First())
                    .ToList();
            }

            return results.Take(Math.Max(number, results.Count > number ? number : results.Count)).ToList();
        }

        private async Task<List<FoodItem>> SearchOnceAsync(string term, int pageSize)
        {
            var url = $"https://api.nal.usda.gov/fdc/v1/foods/search?api_key={ApiKey}";

            var requestBody = new
            {
                generalSearchInput = term,
                pageSize = pageSize,
                requireAllWords = false
            };

            var response = await _http.PostAsJsonAsync(url, requestBody);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<FdcSearchResult>();
            if (result?.foods == null) return new List<FoodItem>();

            return result.foods.Select(f =>
            {
                double calories = 0;
                if (f.foodNutrients != null)
                {
                    var en = f.foodNutrients.FirstOrDefault(n => n.nutrientId == 1008 || string.Equals(n.nutrientName, "Energy", StringComparison.OrdinalIgnoreCase));
                    if (en != null) calories = en.value;
                }

                // Try to include serving information if present in the FDC response
                var measurement = string.Empty;
                if (f.servingSize.HasValue && !string.IsNullOrEmpty(f.servingSizeUnit))
                {
                    measurement = $"{f.servingSize.Value:0.##} {f.servingSizeUnit}";
                }

                // Fallback heuristics for common items when FDC doesn't provide serving info
                if (string.IsNullOrEmpty(measurement) && !string.IsNullOrEmpty(f.description))
                {
                    var desc = f.description.ToLowerInvariant();
                    if (desc.Contains("rice")) measurement = "1.0 cup (cooked)"; // typical cooked rice serving
                    else if (desc.Contains("egg")) measurement = "1 large";
                    else if (desc.Contains("chicken breast")) measurement = "100 g (cooked)";
                    else if (desc.Contains("banana")) measurement = "1 medium";
                    else if (desc.Contains("white rice")) measurement = "1.0 cup (cooked)";
                }

                return new FoodItem
                {
                    Title = f.description ?? string.Empty,
                    Description = f.brandOwner ?? string.Empty,
                    Calories = (int)Math.Round(calories),
                    Measurement = measurement
                };
            }).ToList();
        }

        private class FdcSearchResult
        {
            public List<FdcFood>? foods { get; set; }
        }

        private class FdcFood
        {
            public string? description { get; set; }
            public string? brandOwner { get; set; }
            // Some FDC responses include serving size info
            public double? servingSize { get; set; }
            public string? servingSizeUnit { get; set; }
            public List<FdcNutrient>? foodNutrients { get; set; }
        }

        private class FdcNutrient
        {
            public int? nutrientId { get; set; }
            public string? nutrientName { get; set; }
            public double value { get; set; }
        }
    }
}