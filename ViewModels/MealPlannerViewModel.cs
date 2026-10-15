using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using RunWise.Models;

namespace RunWise.ViewModels
{
    // One cell of the Mon-Sun week strip
    public class WeekDayItem
    {
        public DateTime Date { get; init; }
        public bool IsSelected { get; init; }
        public bool IsToday { get; init; }
        public string DayName => Date.ToString("ddd");
        public string DayNumber => Date.Day.ToString();
        public Color CellBackground => IsSelected ? Color.FromArgb("#4A6CF7") : Colors.White;
        public Color CellStroke => IsSelected ? Color.FromArgb("#4A6CF7") : Color.FromArgb("#E6EAF3");
        public Color DayNameColor => IsSelected ? Color.FromArgb("#DCE3FF") : Color.FromArgb("#9AA3B5");
        public Color DayNumberColor => IsSelected ? Colors.White : (IsToday ? Color.FromArgb("#4A6CF7") : Color.FromArgb("#1B2237"));
        public Color DotColor => IsSelected ? Colors.White : Color.FromArgb("#4A6CF7");
    }

    // Display wrapper for a meal entry (keeps MealItem free of UI-only properties)
    public class MealRow
    {
        public MealRow(MealItem item) { Item = item; }
        public MealItem Item { get; }
        public string Name => string.IsNullOrWhiteSpace(Item.Name) ? "(item)" : Item.Name;
        public int Servings => (int)Math.Max(1, Math.Round(Item.Quantity));
        public int TotalCalories => Item.Calories * Servings;
        public string Detail => !string.IsNullOrWhiteSpace(Item.Notes)
            ? Item.Notes!
            : (Servings == 1 ? "1 serving" : $"{Servings} servings");
        public string CaloriesText => $"{TotalCalories:N0} kcal";
    }

    public class MealSection : INotifyPropertyChanged
    {
        public MealSection(MealType type, string title, string addText)
        {
            Type = type;
            Title = title;
            AddText = addText;
        }

        public MealType Type { get; }
        public string Title { get; }
        public string AddText { get; }
        public ObservableCollection<MealRow> Items { get; } = new ObservableCollection<MealRow>();

        public int Calories => Items.Sum(i => i.TotalCalories);
        public string CaloriesText => $"{Calories:N0} kcal";
        public bool IsEmpty => Items.Count == 0;
        public bool HasItems => Items.Count > 0;
        public string Subtitle => Items.Count == 0 ? "No items added yet" : (Items.Count == 1 ? "1 item" : $"{Items.Count} items");

        public void SetItems(IEnumerable<MealItem> meals)
        {
            Items.Clear();
            foreach (var m in meals) Items.Add(new MealRow(m));
            OnPropertyChanged(nameof(Calories));
            OnPropertyChanged(nameof(CaloriesText));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(Subtitle));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MealPlannerViewModel : INotifyPropertyChanged
    {
        private const string CalorieGoalKey = "runwise_calorie_goal";
        private const int DefaultCalorieGoal = 2000;

        public MealSection Breakfast { get; } = new MealSection(MealType.Breakfast, "Breakfast", "Add breakfast item");
        public MealSection Lunch { get; } = new MealSection(MealType.Lunch, "Lunch", "Add lunch item");
        public MealSection Dinner { get; } = new MealSection(MealType.Dinner, "Dinner", "Add dinner item");
        public MealSection Snacks { get; } = new MealSection(MealType.Snack, "Snacks", "Add a snack");
        public IReadOnlyList<MealSection> Sections { get; }

        public ObservableCollection<WeekDayItem> WeekDays { get; } = new ObservableCollection<WeekDayItem>();

        private int _todayTotalCalories;
        public int TodayTotalCalories
        {
            get => _todayTotalCalories;
            private set { _todayTotalCalories = value; OnPropertyChanged(nameof(TodayTotalCalories)); NotifyGoalDerived(); }
        }

        private int _calorieGoal;
        public int CalorieGoal
        {
            get => _calorieGoal;
            set
            {
                var v = Math.Max(1, value);
                if (_calorieGoal == v) return;
                _calorieGoal = v;
                try { Preferences.Set(CalorieGoalKey, v); } catch { }
                OnPropertyChanged(nameof(CalorieGoal));
                NotifyGoalDerived();
            }
        }

        public string GoalText => $"Goal: {CalorieGoal:N0} kcal";
        public double GoalProgress => Math.Clamp(TodayTotalCalories / (double)CalorieGoal, 0, 1);
        public string GoalCaption
        {
            get
            {
                if (TodayTotalCalories == 0) return "Your day is ready to plan.";
                var left = CalorieGoal - TodayTotalCalories;
                return left >= 0
                    ? $"{left:N0} kcal left of your {CalorieGoal:N0} goal"
                    : $"{-left:N0} kcal over your {CalorieGoal:N0} goal";
            }
        }

        private int _itemsPlannedCount;
        public int ItemsPlannedCount
        {
            get => _itemsPlannedCount;
            private set { _itemsPlannedCount = value; OnPropertyChanged(nameof(ItemsPlannedCount)); OnPropertyChanged(nameof(ItemsPlannedText)); }
        }
        public string ItemsPlannedText => ItemsPlannedCount == 1 ? "1 item planned" : $"{ItemsPlannedCount} items planned";

        public bool IsToday => SelectedDate.Date == DateTime.Today;
        public bool IsNotToday => !IsToday;

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate.Date == value.Date) return;
                _selectedDate = value.Date;
                OnPropertyChanged(nameof(SelectedDate));
                OnPropertyChanged(nameof(IsToday));
                OnPropertyChanged(nameof(IsNotToday));
                BuildWeek();
                _ = LoadForDateAsync(_selectedDate);
            }
        }

        public ICommand PrevDateCommand { get; }
        public ICommand NextDateCommand { get; }
        public ICommand GoToTodayCommand { get; }
        public ICommand SelectDayCommand { get; }

        private readonly Services.MealStore _store;

        public MealPlannerViewModel()
        {
            _store = MauiProgram.Services.GetService(typeof(Services.MealStore)) as Services.MealStore ?? new Services.MealStore();
            Sections = new[] { Breakfast, Lunch, Dinner, Snacks };

            try { _calorieGoal = Preferences.Get(CalorieGoalKey, DefaultCalorieGoal); } catch { _calorieGoal = DefaultCalorieGoal; }
            if (_calorieGoal <= 0) _calorieGoal = DefaultCalorieGoal;

            PrevDateCommand = new Command(() => SelectedDate = SelectedDate.AddDays(-1));
            NextDateCommand = new Command(() => SelectedDate = SelectedDate.AddDays(1));
            GoToTodayCommand = new Command(() => SelectedDate = DateTime.Today);
            SelectDayCommand = new Command<WeekDayItem>(d => { if (d != null) SelectedDate = d.Date; });

            BuildWeek();
            _ = LoadForDateAsync(SelectedDate);
            _store.MealsChanged += async () => await LoadForDateAsync(SelectedDate);
        }

        public System.Threading.Tasks.Task RefreshAsync() => LoadForDateAsync(SelectedDate);

        private void BuildWeek()
        {
            // Week runs Monday..Sunday
            var offset = ((int)SelectedDate.DayOfWeek + 6) % 7;
            var monday = SelectedDate.Date.AddDays(-offset);
            WeekDays.Clear();
            for (int i = 0; i < 7; i++)
            {
                var d = monday.AddDays(i);
                WeekDays.Add(new WeekDayItem { Date = d, IsSelected = d == SelectedDate.Date, IsToday = d == DateTime.Today });
            }
        }

        private async System.Threading.Tasks.Task LoadForDateAsync(DateTime date)
        {
            var meals = await _store.GetMealsForDateAsync(date.Date);
            // Ignore stale results if the user already moved to another day
            if (date.Date != SelectedDate.Date) return;

            var total = meals.Sum(x => x.Calories * (int)Math.Max(1, Math.Round(x.Quantity)));

            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var s in Sections)
                    s.SetItems(meals.Where(x => x.Type == s.Type));
                TodayTotalCalories = total;
                ItemsPlannedCount = Sections.Sum(s => s.Items.Count);
            });

            // Persist daily summary to Supabase (best-effort)
            try
            {
                var sup = MauiProgram.Services.GetService(typeof(Services.SupabaseDatabaseService)) as Services.SupabaseDatabaseService;
                if (sup != null)
                {
                    _ = sup.UpsertCalorieSummaryAsync(date.Date, total);
                }
            }
            catch { }
        }

        public async System.Threading.Tasks.Task DeleteMealAsync(MealItem meal)
        {
            if (meal == null) return;
            await _store.DeleteMealAsync(meal);
            await LoadForDateAsync(SelectedDate);
        }

        public async System.Threading.Tasks.Task UpdateMealAsync(MealItem meal)
        {
            if (meal == null) return;
            await _store.UpdateMealAsync(meal);
            await LoadForDateAsync(SelectedDate);
        }

        private void NotifyGoalDerived()
        {
            OnPropertyChanged(nameof(GoalText));
            OnPropertyChanged(nameof(GoalProgress));
            OnPropertyChanged(nameof(GoalCaption));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
