using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using RunWise.Services;
using RunWise.Models;

namespace RunWise.Pages
{
    [QueryProperty(nameof(RunId), "runId")]
    [QueryProperty(nameof(InitialSearch), "search")]
    [QueryProperty(nameof(MealTarget), "meal")]
    [QueryProperty(nameof(MealDate), "date")]
    public partial class RunFoodRecommendationPage : ContentPage
    {
        private readonly CalorieApiService _api;
        private readonly RunTrackerService _runService;
        private RunSession? _run;
        private RunWise.Models.MealType? _targetMealType;

        public string InitialSearch { get; set; } = string.Empty;

        private string mealTarget = string.Empty;
        public string MealTarget
        {
            get => mealTarget;
            set
            {
                mealTarget = value ?? string.Empty;
                if (Enum.TryParse<RunWise.Models.MealType>(mealTarget, out var mt))
                    _targetMealType = mt;
                else
                    _targetMealType = null;
            }
        }

        private string mealDate = string.Empty;
        public string MealDate
        {
            get => mealDate;
            set
            {
                mealDate = value ?? string.Empty;
                if (DateTime.TryParse(mealDate, out var dt))
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            var dp = this.FindByName<DatePicker>("DatePicker");
                            if (dp != null) dp.Date = dt.Date;
                        }
                        catch { }
                    });
                }
            }
        }

        public RunFoodRecommendationPage()
        {
            InitializeComponent();
            _api = MauiProgram.Services.GetService(typeof(CalorieApiService)) as CalorieApiService ?? throw new InvalidOperationException("CalorieApiService not registered");
            _runService = IPlatformApplication.Current?.Services.GetService<RunTrackerService>() ?? new RunTrackerService();
        }

        // Use IQueryAttributable to receive 'search' and 'meal' parameters when navigated via Shell
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            try
            {
                if (query.TryGetValue("meal", out var m))
                {
                    MealTarget = m?.ToString() ?? string.Empty;
                }

                if (query.TryGetValue("search", out var s))
                {
                    var q = s?.ToString() ?? string.Empty;
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        var se = this.FindByName<Entry>("SearchEntry");
                        if (se != null) se.Text = q;
                        if (!string.IsNullOrWhiteSpace(q))
                            await RecommendForQuery(q);
                    });
                }
            }
            catch { }
        }

        public string RunId
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                    _ = LoadRunAndRecommendAsync(value);
            }
        }

        private async System.Threading.Tasks.Task LoadRunAndRecommendAsync(string id)
        {
            _run = await _runService.GetRunByIdAsync(id);
            if (_run != null)
            {
                RunSummaryLabel.Text = $"Run: {_run.StartTime:MMM dd, yyyy} • {Math.Round(_run.DistanceKm,2)} km • {_run.Calories} kcal";
                CaloriesLabel.Text = _run.Calories.ToString();
                await RecommendForCalories(_run.Calories);
            }
            else
            {
                RunSummaryLabel.Text = "Run not found.";
            }
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            // If a search query is provided, search by food name instead of using calories
            var searchEntry = this.FindByName<Entry>("SearchEntry");
            var query = (searchEntry?.Text ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(query))
            {
                await RecommendForQuery(query);
                return;
            }

            int remaining = 0;
            if (_run != null)
                remaining = _run.Calories;
            else if (!int.TryParse(CaloriesLabel.Text, out remaining))
            {
                await DisplayAlert("Invalid", "No valid calorie value to refresh.", "OK");
                return;
            }

            await RecommendForCalories(remaining);
        }

        private async System.Threading.Tasks.Task RecommendForCalories(int remaining)
        {
            PickLabel.Text = "Loading...";
            CandidatesList.ItemsSource = null;

            try
            {
                var candidates = await _api.GetCandidatesAsync(remaining, number: 12);

                if (candidates == null || !candidates.Any())
                {
                    PickLabel.Text = "No foods found. Try a different search.";
                    CandidatesList.ItemsSource = new List<FoodItem>();
                    return;
                }

                var displayCandidates = new List<FoodItem>(candidates);

                FoodItem pick = null;
                var positive = candidates.Where(c => c.Calories > 0).ToList();
                if (positive.Any())
                {
                    var bestUnder = positive.Where(c => c.Calories <= remaining).OrderByDescending(c => c.Calories).FirstOrDefault();
                    if (bestUnder != null)
                        pick = bestUnder;
                    else
                        pick = positive.OrderBy(c => c.Calories).FirstOrDefault();
                }

                if (pick == null)
                {
                    PickLabel.Text = "No recommendation available.";
                }
                else
                {
                    PickLabel.Text = $"Recommended: {pick.Title} ({pick.Calories} kcal)";

                    var reasonItem = new FoodItem
                    {
                        Title = pick.Title,
                        Description = pick.Description,
                        Calories = pick.Calories,
                        Measurement = pick.Measurement,
                        NutrientsSummary = pick.NutrientsSummary,
                        ImageUrl = pick.ImageUrl,
                        Guidance = $"This fits your remaining {remaining} kcal budget."
                    };

                    var existingIdx = displayCandidates.FindIndex(d => string.Equals(d.Title?.Trim(), pick.Title?.Trim(), StringComparison.OrdinalIgnoreCase) && d.Calories == pick.Calories);
                    if (existingIdx >= 0)
                        displayCandidates.RemoveAt(existingIdx);

                    displayCandidates.Insert(0, reasonItem);
                }

                foreach (var item in displayCandidates)
                {
                    if (item.Calories <= 0)
                    {
                        item.Guidance = string.IsNullOrEmpty(item.Measurement) ? string.Empty : $"Typical serving: {item.Measurement}";
                        continue;
                    }

                    var perServing = $"1 serving = {item.Calories} kcal" + (string.IsNullOrEmpty(item.Measurement) ? string.Empty : $" ({item.Measurement})");
                    item.Guidance = perServing;
                }

                CandidatesList.ItemsSource = displayCandidates;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
                PickLabel.Text = string.Empty;
            }
        }

        private async System.Threading.Tasks.Task RecommendForQuery(string query)
        {
            PickLabel.Text = "Loading...";
            CandidatesList.ItemsSource = null;

            try
            {
                var candidates = await _api.GetCandidatesAsync(0, number: 12, searchTerm: query);
                if (candidates == null || !candidates.Any())
                {
                    PickLabel.Text = "No foods found. Try a different search.";
                    CandidatesList.ItemsSource = new List<FoodItem>();
                    return;
                }

                var displayCandidates = new List<FoodItem>(candidates);
                var pick = displayCandidates.FirstOrDefault();
                if (pick == null)
                {
                    PickLabel.Text = "No recommendation available.";
                }
                else
                {
                    PickLabel.Text = $"Recommended: {pick.Title} ({pick.Calories} kcal)";
                    var reasonItem = new FoodItem
                    {
                        Title = pick.Title,
                        Description = pick.Description,
                        Calories = pick.Calories,
                        Measurement = pick.Measurement,
                        NutrientsSummary = pick.NutrientsSummary,
                        ImageUrl = pick.ImageUrl,
                        Guidance = string.IsNullOrEmpty(pick.Measurement) ? string.Empty : $"Typical serving: {pick.Measurement}"
                    };

                    var existingIdx = displayCandidates.FindIndex(d => string.Equals(d.Title?.Trim(), pick.Title?.Trim(), StringComparison.OrdinalIgnoreCase) && d.Calories == pick.Calories);
                    if (existingIdx >= 0)
                        displayCandidates.RemoveAt(existingIdx);

                    displayCandidates.Insert(0, reasonItem);
                }

                foreach (var item in displayCandidates)
                {
                    var perServing = item.Calories > 0 ? $"1 serving = {item.Calories} kcal" : string.Empty;
                    if (!string.IsNullOrEmpty(item.Measurement))
                    {
                        item.Guidance = string.IsNullOrEmpty(perServing) ? $"Typical serving: {item.Measurement}" : $"{perServing} ({item.Measurement})";
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

        private async void OnAddToMealClicked(object sender, EventArgs e)
        {
            if (sender is Button b && b.BindingContext is FoodItem item)
            {
                MealType type;
                if (_targetMealType.HasValue)
                {
                    type = _targetMealType.Value;
                }
                else
                {
                    // Ask which meal type to add to
                    var choice = await DisplayActionSheet("Add to meal", "Cancel", null, "Breakfast", "Lunch", "Dinner", "Snack");
                    if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

                    type = MealType.Breakfast;
                    switch (choice)
                    {
                        case "Lunch": type = MealType.Lunch; break;
                        case "Dinner": type = MealType.Dinner; break;
                        case "Snack": type = MealType.Snack; break;
                    }
                }

                var store = MauiProgram.Services.GetService(typeof(RunWise.Services.MealStore)) as RunWise.Services.MealStore ?? new RunWise.Services.MealStore();
                var meal = new RunWise.Models.MealItem
                {
                    Name = item.Title ?? "Food",
                    Calories = item.Calories > 0 ? item.Calories : 0,
                    Type = type,
                    Quantity = 1,
                    Date = this.FindByName<DatePicker>("DatePicker")?.Date ?? DateTime.Today
                };

                await store.AddMealAsync(meal);

                try
                {
                    var sup = MauiProgram.Services.GetService(typeof(SupabaseDatabaseService)) as SupabaseDatabaseService;
                    if (sup != null)
                    {
                        var (ok, err) = await sup.CreateMealAsync(meal);
                        if (!ok)
                        {
                            await DisplayAlert("Saved locally", $"Meal saved locally but server sync failed: {err}", "OK");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Saved locally", $"Meal saved locally but server sync failed: {ex.Message}", "OK");
                    return;
                }

                await DisplayAlert("Added", "Added to Meal Planner", "OK");
            }
        }
    }
}
