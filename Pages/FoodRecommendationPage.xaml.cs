using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using RunWise.Services;
using RunWise.Models;

namespace RunWise.Pages
{
    [QueryProperty(nameof(CaloriesQuery), "calories")]
    [QueryProperty(nameof(InitialSearch), "search")]
    [QueryProperty(nameof(MealTarget), "meal")]
    [QueryProperty(nameof(MealDate), "date")]
    public partial class FoodRecommendationPage : ContentPage
    {
        private readonly CalorieApiService _api;
        private RunWise.Models.MealType? _targetMealType;
        private readonly Services.MealStore _mealStore;
       

        public FoodRecommendationPage()
        {
            InitializeComponent();

            _api = MauiProgram.Services.GetService(typeof(CalorieApiService)) as CalorieApiService ?? throw new InvalidOperationException("CalorieApiService not registered");
            _mealStore = MauiProgram.Services.GetService(typeof(Services.MealStore)) as Services.MealStore ?? new Services.MealStore();

            // refresh total calories on open
            _ = UpdateTotalCaloriesAsync();

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
                    // ensure future Add uses this date
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

        // Initial search term supplied via Shell navigation
        private string initialSearch = string.Empty;
        public string InitialSearch
        {
            get => initialSearch;
            set
            {
                initialSearch = value ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(initialSearch))
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        CaloriesEntry.Text = initialSearch;
                        await RecommendForQuery(initialSearch);
                    });
                }
            }
        }

        private string mealTarget = string.Empty;
        public string MealTarget
        {
            get => mealTarget;
            set
            {
                mealTarget = value ?? string.Empty;
                if (Enum.TryParse<RunWise.Models.MealType>(mealTarget, out var mt))
                {
                    _targetMealType = mt;
                    // update UI indicator and picker selection
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        TargetMealLabel.Text = $"Add to: {mt}";
                        try { MealTypePicker.SelectedItem = mt.ToString(); } catch { }
                    });
                }
                else
                {
                    _targetMealType = null;
                    Device.BeginInvokeOnMainThread(() => TargetMealLabel.Text = "Food");
                }
            }
        }

        // Populate picker after InitializeComponent
        protected override void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                MealTypePicker.Items.Clear();
                MealTypePicker.Items.Add("Breakfast");
                MealTypePicker.Items.Add("Lunch");
                MealTypePicker.Items.Add("Dinner");
                MealTypePicker.Items.Add("Snack");
                MealTypePicker.SelectedIndexChanged += MealTypePicker_SelectedIndexChanged;
            }
            catch { }
        }

        private void MealTypePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var p = sender as Picker;
                var sel = p?.SelectedItem?.ToString() ?? string.Empty;
                if (Enum.TryParse<RunWise.Models.MealType>(sel, out var mt))
                {
                    _targetMealType = mt;
                    TargetMealLabel.Text = $"Add to: {mt}";
                }
            }
            catch { }
        }

        private async void OnGetRecommendationClicked(object sender, EventArgs e)
        {
            var button = sender as Button;
            try
            {
                if (button != null)
                    button.IsEnabled = false;

                var query = (CaloriesEntry.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(query))
                {
                    await DisplayAlert("Invalid", "Please enter a food name to search for.", "OK");
                    return;
                }

                // If the user entered an integer, treat it as remaining calories; otherwise treat as a search term.
                if (int.TryParse(query, out var cal))
                {
                    await RecommendForCalories(cal);
                }
                else
                {
                    await RecommendForQuery(query);
                }
            }
            finally
            {
                if (button != null)
                    button.IsEnabled = true;
            }
        }

        // Allow receiving initial calories via Shell query parameter 'calories'
        string caloriesQuery;
        public string CaloriesQuery
        {
            set
            {
                caloriesQuery = Uri.UnescapeDataString(value ?? string.Empty);
                if (int.TryParse(caloriesQuery, out var cal))
                {
                    // update UI and trigger recommendation
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        CaloriesEntry.Text = cal.ToString();
                        await RecommendForCalories(cal);
                    });
                }
            }
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
                    CandidatesList.ItemsSource = new List<Models.FoodItem>();
                    return;
                }

                var displayCandidates = new List<Models.FoodItem>(candidates);

                Models.FoodItem pick = null;
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

                    var reasonItem = new Models.FoodItem
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

                    var servingsNeeded = remaining / (double)item.Calories;
                    var perServing = $"1 serving = {item.Calories} kcal" + (string.IsNullOrEmpty(item.Measurement) ? string.Empty : $" ({item.Measurement})");
                    item.Guidance = perServing;
                }

                CandidatesList.ItemsSource = displayCandidates;
                // update totals after any change
                _ = UpdateTotalCaloriesAsync();
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

                // Try server sync and inform the user if it fails
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
                    CandidatesList.ItemsSource = new List<Models.FoodItem>();
                    return;
                }

                var displayCandidates = new List<Models.FoodItem>(candidates);
                var pick = displayCandidates.FirstOrDefault();
                if (pick == null)
                {
                    PickLabel.Text = "No recommendation available.";
                }
                else
                {
                    PickLabel.Text = $"Recommended: {pick.Title} ({pick.Calories} kcal)";
                    var reasonItem = new Models.FoodItem
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
                // update totals after any change
                _ = UpdateTotalCaloriesAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
                PickLabel.Text = string.Empty;
            }
        }
        // Test API button removed - relying solely on production recommendation flow

        private async System.Threading.Tasks.Task UpdateTotalCaloriesAsync()
        {
            try
            {
                var meals = await _mealStore.GetMealsForDateAsync(DateTime.Today);
                var total = meals.Sum(m => m.Calories * (int)Math.Max(1, Math.Round(m.Quantity)));
                Device.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        var lbl = this.FindByName<Label>("TotalCaloriesLabel");
                        if (lbl != null) lbl.Text = $"{total} kcal";
                    }
                    catch { }
                });
            }
            catch { }
        }
    }
}
