using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using RunWise.Models;
using RunWise.Services;

namespace RunWise.ViewModels
{
    // One option of the Breakfast/Lunch/Dinner/Snack selector
    public class MealChoice
    {
        public MealType Type { get; init; }
        public bool IsSelected { get; init; }
        public string Label => Type.ToString();
        public Color Background => IsSelected ? Color.FromArgb("#4A6CF7") : Colors.White;
        public Color Stroke => IsSelected ? Color.FromArgb("#4A6CF7") : Color.FromArgb("#E6EAF3");
        public Color TextColor => IsSelected ? Colors.White : Color.FromArgb("#1B2237");
    }

    // A food that can be added from the Search tab
    public class FoodOption
    {
        public string Title { get; init; } = string.Empty;
        public int Calories { get; init; }
        public string Measurement { get; init; } = string.Empty;
        public bool IsMine { get; init; }
        // Why this food was recommended (Recommended food tab only)
        public string Reason { get; init; } = string.Empty;
        public bool HasReason => !string.IsNullOrWhiteSpace(Reason);
        public string ServingText => string.IsNullOrWhiteSpace(Measurement) ? "1 serving" : $"1 serving = {Measurement}";
        public string CaloriesText => $"{Calories:N0} kcal";

        public static FoodOption From(FoodItem f, bool mine = false) =>
            new FoodOption { Title = f.Title, Calories = f.Calories, Measurement = f.Measurement, IsMine = mine };
    }

    public enum LogFoodTab { Search, Recommended, Own }

    public class LogFoodViewModel : INotifyPropertyChanged
    {
        private const string NoResultsMessage = "No foods found. Try \"My own food\" to add it yourself.";

        // Recommended foods from the calorie API, cached for the app session
        // because loading them takes several API requests.
        private static List<FoodOption>? _recommendedCache;

        private readonly MealStore _store;
        private readonly CalorieApiService? _api;
        private readonly Func<Task>? _onLogged;
        private readonly OpenRouterService? _ai;
        private readonly RunTrackerService _runs;
        private CancellationTokenSource? _searchCts;
        private CancellationTokenSource? _recommendCts;
        // date + meal the current run recommendations were made for
        private string? _recommendKey;

        public LogFoodViewModel(DateTime date, MealType meal, Func<Task>? onLogged = null)
        {
            _store = MauiProgram.Services.GetService(typeof(MealStore)) as MealStore ?? new MealStore();
            _api = MauiProgram.Services.GetService(typeof(CalorieApiService)) as CalorieApiService;
            _ai = MauiProgram.Services.GetService(typeof(OpenRouterService)) as OpenRouterService;
            _runs = MauiProgram.Services.GetService(typeof(RunTrackerService)) as RunTrackerService ?? new RunTrackerService();
            _onLogged = onLogged;
            _date = date.Date;
            _selectedMeal = meal;

            SelectMealCommand = new Command<MealChoice>(c => { if (c != null) SelectedMeal = c.Type; });
            ShowSearchCommand = new Command(() => Tab = LogFoodTab.Search);
            ShowRecommendedCommand = new Command(() => Tab = LogFoodTab.Recommended);
            ShowOwnCommand = new Command(() => Tab = LogFoodTab.Own);
            RefreshRecommendationsCommand = new Command(() => _ = LoadRunRecommendationsAsync(force: true));
            IncrementServingsCommand = new Command(() => Servings = Math.Min(20, Servings + 1));
            DecrementServingsCommand = new Command(() => Servings = Math.Max(1, Servings - 1));

            BuildMealChoices();
            _ = SearchDebouncedAsync(string.Empty);
        }

        public ObservableCollection<MealChoice> MealChoices { get; } = new ObservableCollection<MealChoice>();
        public ObservableCollection<FoodOption> Foods { get; } = new ObservableCollection<FoodOption>();
        public ObservableCollection<FoodOption> RunFoods { get; } = new ObservableCollection<FoodOption>();

        public ICommand SelectMealCommand { get; }
        public ICommand ShowSearchCommand { get; }
        public ICommand ShowRecommendedCommand { get; }
        public ICommand ShowOwnCommand { get; }
        public ICommand RefreshRecommendationsCommand { get; }
        public ICommand IncrementServingsCommand { get; }
        public ICommand DecrementServingsCommand { get; }

        private DateTime _date;
        public DateTime Date
        {
            get => _date;
            set
            {
                if (_date == value.Date) return;
                _date = value.Date;
                OnPropertyChanged(nameof(Date));
                if (IsRecommendedMode) _ = LoadRunRecommendationsAsync();
            }
        }

        private MealType _selectedMeal;
        public MealType SelectedMeal
        {
            get => _selectedMeal;
            set
            {
                if (_selectedMeal == value) return;
                _selectedMeal = value;
                OnPropertyChanged(nameof(SelectedMeal));
                OnPropertyChanged(nameof(AddOwnButtonText));
                BuildMealChoices();
                if (IsRecommendedMode) _ = LoadRunRecommendationsAsync();
            }
        }

        public string AddOwnButtonText => $"Add to {SelectedMeal}";

        private LogFoodTab _tab = LogFoodTab.Search;
        public LogFoodTab Tab
        {
            get => _tab;
            set
            {
                if (_tab == value) return;
                _tab = value;
                foreach (var name in new[] { nameof(Tab), nameof(IsSearchMode), nameof(IsRecommendedMode), nameof(IsOwnMode),
                                             nameof(SearchTabBackground), nameof(SearchTabText),
                                             nameof(RecommendedTabBackground), nameof(RecommendedTabText),
                                             nameof(OwnTabBackground), nameof(OwnTabText) })
                    OnPropertyChanged(name);
                if (IsRecommendedMode) _ = LoadRunRecommendationsAsync();
            }
        }
        public bool IsSearchMode => Tab == LogFoodTab.Search;
        public bool IsRecommendedMode => Tab == LogFoodTab.Recommended;
        public bool IsOwnMode => Tab == LogFoodTab.Own;

        private Color TabBackground(LogFoodTab t) => Tab == t ? Colors.White : Colors.Transparent;
        private Color TabText(LogFoodTab t) => Tab == t ? Color.FromArgb("#4A6CF7") : Color.FromArgb("#9AA3B5");
        public Color SearchTabBackground => TabBackground(LogFoodTab.Search);
        public Color SearchTabText => TabText(LogFoodTab.Search);
        public Color RecommendedTabBackground => TabBackground(LogFoodTab.Recommended);
        public Color RecommendedTabText => TabText(LogFoodTab.Recommended);
        public Color OwnTabBackground => TabBackground(LogFoodTab.Own);
        public Color OwnTabText => TabText(LogFoodTab.Own);

        // "Recommended food" tab state
        private string _runSummary = string.Empty;
        public string RunSummary { get => _runSummary; private set { _runSummary = value; OnPropertyChanged(nameof(RunSummary)); } }

        private string _runTargetText = string.Empty;
        public string RunTargetText
        {
            get => _runTargetText;
            private set { _runTargetText = value; OnPropertyChanged(nameof(RunTargetText)); OnPropertyChanged(nameof(HasRunTarget)); }
        }
        public bool HasRunTarget => !string.IsNullOrEmpty(RunTargetText);

        private bool _isRecommending;
        public bool IsRecommending { get => _isRecommending; private set { _isRecommending = value; OnPropertyChanged(nameof(IsRecommending)); } }

        private string _recommendMessage = string.Empty;
        public string RecommendMessage
        {
            get => _recommendMessage;
            private set { _recommendMessage = value; OnPropertyChanged(nameof(RecommendMessage)); OnPropertyChanged(nameof(HasRecommendMessage)); }
        }
        public bool HasRecommendMessage => !string.IsNullOrEmpty(RecommendMessage);

        private int _servings = 1;
        public int Servings
        {
            get => _servings;
            set { if (_servings != value) { _servings = value; OnPropertyChanged(nameof(Servings)); } }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value ?? string.Empty;
                OnPropertyChanged(nameof(SearchText));
                _ = SearchDebouncedAsync(_searchText);
            }
        }

        private string _listTitle = "Recommended foods";
        public string ListTitle
        {
            get => _listTitle;
            private set { _listTitle = value; OnPropertyChanged(nameof(ListTitle)); }
        }

        private bool _isSearching;
        public bool IsSearching
        {
            get => _isSearching;
            private set { _isSearching = value; OnPropertyChanged(nameof(IsSearching)); }
        }

        private string _emptyMessage = string.Empty;
        public string EmptyMessage
        {
            get => _emptyMessage;
            private set { _emptyMessage = value; OnPropertyChanged(nameof(EmptyMessage)); OnPropertyChanged(nameof(HasEmptyMessage)); }
        }
        public bool HasEmptyMessage => !string.IsNullOrEmpty(EmptyMessage);

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); OnPropertyChanged(nameof(HasStatus)); }
        }
        public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

        // "My own food" form
        private string _ownName = string.Empty;
        public string OwnName { get => _ownName; set { _ownName = value ?? string.Empty; OnPropertyChanged(nameof(OwnName)); } }
        private string _ownCalories = string.Empty;
        public string OwnCalories { get => _ownCalories; set { _ownCalories = value ?? string.Empty; OnPropertyChanged(nameof(OwnCalories)); } }
        private string _ownServing = string.Empty;
        public string OwnServing { get => _ownServing; set { _ownServing = value ?? string.Empty; OnPropertyChanged(nameof(OwnServing)); } }
        private bool _saveToMyFoods = true;
        public bool SaveToMyFoods { get => _saveToMyFoods; set { _saveToMyFoods = value; OnPropertyChanged(nameof(SaveToMyFoods)); } }

        private void BuildMealChoices()
        {
            MealChoices.Clear();
            foreach (var t in new[] { MealType.Breakfast, MealType.Lunch, MealType.Dinner, MealType.Snack })
                MealChoices.Add(new MealChoice { Type = t, IsSelected = t == SelectedMeal });
        }

        // My foods first, then recommendations from the calorie API
        private async Task ShowRecommendedAsync(CancellationTokenSource cts)
        {
            var mine = MyFoodsStore.GetAll().Select(f => FoodOption.From(f, true)).ToList();
            ListTitle = "Recommended foods";
            EmptyMessage = string.Empty;
            SetFoods(mine.Concat(_recommendedCache ?? new List<FoodOption>()));

            if (_recommendedCache == null && _api != null)
            {
                IsSearching = true;
                List<FoodItem> remote;
                try { remote = await _api.GetCandidatesAsync(300, number: 15); }
                catch { remote = new List<FoodItem>(); }
                var recommended = remote.Select(f => FoodOption.From(f)).ToList();
                if (recommended.Count > 0) _recommendedCache = recommended;
                if (cts.IsCancellationRequested) return;

                IsSearching = false;
                SetFoods(mine.Concat(recommended));
            }

            if (Foods.Count == 0) EmptyMessage = "Couldn't load foods right now. Search for a food or add your own.";
        }

        // Recommends foods that refuel the calories burned in the runs on the selected date
        private async Task LoadRunRecommendationsAsync(bool force = false)
        {
            var key = $"{Date:yyyy-MM-dd}|{SelectedMeal}";
            if (!force && key == _recommendKey) return;
            _recommendKey = key;

            _recommendCts?.Cancel();
            var cts = _recommendCts = new CancellationTokenSource();
            var day = Date.Date == DateTime.Today ? "today" : $"on {Date:ddd, MMM d}";

            RunFoods.Clear();
            RecommendMessage = string.Empty;
            RunTargetText = string.Empty;
            RunSummary = "Checking your runs…";
            IsRecommending = true;

            try
            {
                var runs = (await _runs.GetAllRunsAsync()).Where(r => r.StartTime.Date == Date.Date).ToList();
                if (cts.IsCancellationRequested) return;

                if (runs.Count == 0)
                {
                    RunSummary = $"No run logged {day}.";
                    RecommendMessage = "Finish a run and we'll suggest food to refuel the calories you burned.";
                    return;
                }

                var burned = runs.Sum(r => r.Calories);
                var km = runs.Sum(r => r.DistanceMeters) / 1000.0;
                RunSummary = runs.Count == 1
                    ? $"You ran {km:0.##} km {day} and burned {burned:N0} kcal."
                    : $"You ran {runs.Count} times ({km:0.##} km) {day} and burned {burned:N0} kcal.";

                if (burned <= 0)
                {
                    RecommendMessage = "Your run has no calorie data, so we can't size a meal for it yet.";
                    return;
                }

                // Aim to refuel what the run burned, kept to a sensible size for one meal
                var target = Math.Clamp(burned, 150, SelectedMeal == MealType.Snack ? 400 : 900);
                RunTargetText = $"Target for {SelectedMeal}: about {target:N0} kcal";

                var foods = await AskAiForFoodsAsync(burned, km, target);
                if (cts.IsCancellationRequested) return;

                if (foods.Count == 0 && _api != null)
                {
                    List<FoodItem> remote;
                    try { remote = await _api.GetCandidatesAsync(target, number: 6); }
                    catch { remote = new List<FoodItem>(); }
                    if (cts.IsCancellationRequested) return;
                    foods = remote.Select(f => FoodOption.From(f)).ToList();
                }

                foreach (var f in foods) RunFoods.Add(f);
                if (RunFoods.Count == 0)
                {
                    RecommendMessage = "Couldn't load recommendations right now. Tap refresh to try again.";
                    _recommendKey = null;
                }
            }
            finally
            {
                if (!cts.IsCancellationRequested) IsRecommending = false;
            }
        }

        private class AiFood
        {
            public string? Name { get; set; }
            public int Calories { get; set; }
            public string? Serving { get; set; }
            public string? Reason { get; set; }
        }

        private async Task<List<FoodOption>> AskAiForFoodsAsync(int burned, double km, int target)
        {
            if (_ai == null) return new List<FoodOption>();

            var prompt =
                $"A runner just ran {km:0.##} km and burned {burned} kcal. " +
                $"Suggest 5 {SelectedMeal.ToString().ToLowerInvariant()} options that help them recover, each about {target} kcal per serving " +
                "(within ±15%). Favor protein plus carbs for recovery, and include some common Filipino dishes. " +
                "Reply with ONLY a JSON array, no other text, in this format: " +
                "[{\"name\":\"...\",\"calories\":450,\"serving\":\"1 plate\",\"reason\":\"one short sentence\"}]";

            string? reply;
            try { reply = await _ai.GetCoachingCueAsync(prompt); }
            catch { return new List<FoodOption>(); }
            if (string.IsNullOrWhiteSpace(reply)) return new List<FoodOption>();

            var start = reply.IndexOf('[');
            var end = reply.LastIndexOf(']');
            if (start < 0 || end <= start) return new List<FoodOption>();

            try
            {
                var items = System.Text.Json.JsonSerializer.Deserialize<List<AiFood>>(reply.Substring(start, end - start + 1),
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true,
                        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString });
                return (items ?? new List<AiFood>())
                    .Where(i => !string.IsNullOrWhiteSpace(i.Name) && i.Calories > 0)
                    .Select(i => new FoodOption
                    {
                        Title = i.Name!.Trim(),
                        Calories = i.Calories,
                        Measurement = i.Serving?.Trim() ?? string.Empty,
                        Reason = i.Reason?.Trim() ?? string.Empty
                    })
                    .ToList();
            }
            catch { return new List<FoodOption>(); }
        }

        private void SetFoods(IEnumerable<FoodOption> foods)
        {
            Foods.Clear();
            foreach (var f in foods
                .Where(f => !string.IsNullOrWhiteSpace(f.Title))
                .GroupBy(f => f.Title.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()))
                Foods.Add(f);
        }

        private async Task SearchDebouncedAsync(string text)
        {
            _searchCts?.Cancel();
            var cts = _searchCts = new CancellationTokenSource();
            var query = text.Trim();

            if (query.Length == 0)
            {
                IsSearching = false;
                await ShowRecommendedAsync(cts);
                return;
            }

            try { await Task.Delay(400, cts.Token); } catch (TaskCanceledException) { return; }

            // Matching My foods show immediately; API results are appended when they arrive
            var local = MyFoodsStore.GetAll().Select(f => FoodOption.From(f, true))
                .Where(f => f.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            SetFoods(local);
            ListTitle = "Results";
            EmptyMessage = string.Empty;

            if (_api == null)
            {
                if (local.Count == 0) EmptyMessage = NoResultsMessage;
                return;
            }

            IsSearching = true;
            List<FoodItem> remote;
            try { remote = await _api.GetCandidatesAsync(0, number: 12, searchTerm: query); }
            catch { remote = new List<FoodItem>(); }
            if (cts.IsCancellationRequested) return;

            IsSearching = false;
            SetFoods(local.Concat(remote.Select(f => FoodOption.From(f))));
            if (Foods.Count == 0) EmptyMessage = NoResultsMessage;
        }

        public async Task AddFoodAsync(FoodOption food)
        {
            var meal = new MealItem
            {
                Name = food.Title,
                Calories = Math.Max(0, food.Calories),
                Type = SelectedMeal,
                Quantity = Servings,
                Date = Date,
                Notes = string.IsNullOrWhiteSpace(food.Measurement) ? null : food.Measurement
            };
            await LogAsync(meal);
            StatusMessage = Servings == 1
                ? $"Added {food.Title} to {SelectedMeal}"
                : $"Added {Servings} × {food.Title} to {SelectedMeal}";
            Servings = 1;
        }

        // Returns an error message, or null when the food was logged
        public async Task<string?> AddOwnFoodAsync()
        {
            var name = OwnName.Trim();
            if (name.Length == 0) return "Please enter a meal name.";
            if (!int.TryParse(OwnCalories.Trim(), out var cal) || cal <= 0) return "Calories must be a positive number.";
            var serving = OwnServing.Trim();

            if (SaveToMyFoods)
                MyFoodsStore.Save(new FoodItem { Title = name, Calories = cal, Measurement = serving });

            await LogAsync(new MealItem
            {
                Name = name,
                Calories = cal,
                Type = SelectedMeal,
                Quantity = 1,
                Date = Date,
                Notes = serving.Length == 0 ? null : serving
            });

            StatusMessage = $"Added {name} to {SelectedMeal}";
            OwnName = OwnCalories = OwnServing = string.Empty;
            // re-run the current list so a newly saved food shows up
            if (SaveToMyFoods) _ = SearchDebouncedAsync(SearchText);
            return null;
        }

        private async Task LogAsync(MealItem meal)
        {
            await _store.AddMealAsync(meal);
            try
            {
                var sup = MauiProgram.Services.GetService(typeof(SupabaseDatabaseService)) as SupabaseDatabaseService;
                if (sup != null) _ = await sup.CreateMealAsync(meal);
            }
            catch { }
            if (_onLogged != null) await _onLogged();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
