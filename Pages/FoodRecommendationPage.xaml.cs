using System;
using System.Linq;
using Microsoft.Maui.Controls;
using RunWise.Services;
using System.Text.Json;
using RunWise.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RunWise.Pages
{
    public partial class FoodRecommendationPage : ContentPage
    {
        private readonly FoodApiService _api;
        private readonly FoodRecommendationService _recommender;
        private readonly GeminiService _gemini;

        public FoodRecommendationPage()
        {
            InitializeComponent();

            _api = MauiProgram.Services.GetService(typeof(FoodApiService)) as FoodApiService ?? throw new InvalidOperationException("FoodApiService not registered");
            _recommender = MauiProgram.Services.GetService(typeof(FoodRecommendationService)) as FoodRecommendationService ?? throw new InvalidOperationException("FoodRecommendationService not registered");
            _gemini = new GeminiService();
        }

        private async void OnGetRecommendationClicked(object sender, EventArgs e)
        {
            if (!int.TryParse(CaloriesEntry.Text, out var remaining))
            {
                await DisplayAlert("Invalid", "Please enter a valid number for remaining calories.", "OK");
                return;
            }

            PickLabel.Text = "Loading...";
            CandidatesList.ItemsSource = null;

            try
            {
                // If the user typed something, search that directly.
                // If not, FoodApiService now rotates through Filipino-food terms itself.
                var searchTerm = SearchEntry.Text?.Trim() ?? string.Empty;

                var candidates = await _api.GetCandidatesAsync(Math.Max(remaining, 300), number: 12, searchTerm: searchTerm);

                // Make a working copy for display; we may insert the selection reason into it later.
                var displayCandidates = new List<FoodItem>(candidates);

                // Gemini now ranks/confirms Filipino-familiarity rather than filtering
                // a mostly-irrelevant list. Candidates already skew Filipino from the search step.
                List<FoodItem> filtered = new List<FoodItem>(candidates);
                try
                {
                    if (candidates.Any())
                    {
                        var candidateSummary = string.Join("\n", candidates.Select(c => $"- {c.Title}"));
                        var prompt = $@"
You are helping a Filipino user find a meal after a long run can you recommend something like has nutrition for the runners no ice cream. Below is a list of food item titles.
Identify which items are dishes/ingredients that are commonly eaten in the Philippines
or are easily bought in Philippine grocery stores/wet markets (sari-sari stores, palengke, supermarkets).
Exclude items that are clearly foreign/unfamiliar to a typical Filipino household (e.g. niche
Middle Eastern, European, or US-branded specialty products) unless the item is a widely known
international staple (e.g. plain rice, chicken, eggs).

Respond ONLY with JSON, no other text, in this exact shape:
{{""filipino"": [""<exact title from the list>"", ""...""]}}

If none qualify, respond with {{""filipino"": []}}.

Candidate items:
{candidateSummary}
";

                        var geminiText = await _gemini.GenerateTextAsync(prompt);
                        if (!string.IsNullOrWhiteSpace(geminiText))
                        {
                            var clean = geminiText.Replace("```json", "").Replace("```", "").Trim();
                            try
                            {
                                var resp = JsonSerializer.Deserialize<FilipinoResponse>(clean, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                var set = new HashSet<string>(resp?.Filipino ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                                var matches = candidates.Where(c => set.Contains(c.Title)).ToList();
                                if (matches.Any()) filtered = matches;
                            }
                            catch
                            {
                                // fall back to unfiltered (already Filipino-biased) candidates on parse errors
                            }
                        }
                    }
                }
                catch
                {
                    // If Gemini fails, continue with the already Filipino-biased candidates
                }

                var pick = _recommender.RecommendByCalories(filtered.Any() ? filtered : candidates, remaining);
                if (pick is null)
                {
                    PickLabel.Text = "No recommendation available";
                }
                else
                {
                    PickLabel.Text = pick.Title;

                    // Insert the selection reason into the top of the candidates list so it's visible in the "Suggestion" section.
                    var reasonItem = new FoodItem
                    {
                        Title = $"Selected: {pick.Title}",
                        Description = $"Selected because it fits your remaining {remaining} kcal budget (meal: {pick.Calories} kcal).",
                        Calories = pick.Calories,
                        Measurement = pick.Measurement
                    };

                    // place reason at the start of the displayed list
                    displayCandidates.Insert(0, reasonItem);
                }

                // Compute guidance for each displayed candidate so the user knows how much to eat to reach the remaining calories
                foreach (var item in displayCandidates)
                {
                    if (!string.IsNullOrEmpty(item.Title) && item.Title.StartsWith("Selected: ", StringComparison.OrdinalIgnoreCase))
                    {
                        // show the reason text for the selected item
                        item.Guidance = item.Description;
                        continue;
                    }

                    if (item.Calories <= 0)
                    {
                        item.Guidance = string.IsNullOrEmpty(item.Measurement) ? string.Empty : $"Typical serving: {item.Measurement}";
                        continue;
                    }

                    var servingsNeeded = remaining / (double)item.Calories;
                    var perServing = $"1 serving = {item.Calories} kcal" + (string.IsNullOrEmpty(item.Measurement) ? string.Empty : $" ({item.Measurement})");
                    if (servingsNeeded >= 1.0)
                    {
                        item.Guidance = $"{perServing}. To use {remaining} kcal: {servingsNeeded:0.##} servings.";
                    }
                    else if (servingsNeeded >= 0.01)
                    {
                        // less than one serving but non-trivial
                        item.Guidance = $"{perServing}. To use {remaining} kcal: {servingsNeeded:0.##} of a serving.";
                    }
                    else
                    {
                        item.Guidance = perServing;
                    }
                }

                CandidatesList.ItemsSource = displayCandidates;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
                PickLabel.Text = string.Empty;
            }
        }
    }

    // Used to deserialize Gemini's JSON reply: {"filipino": ["title1", "title2", ...]}
    public class FilipinoResponse
    {
        public List<string> Filipino { get; set; } = new();
    }
}