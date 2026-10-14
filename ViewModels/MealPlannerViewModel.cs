using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using RunWise.Models;

namespace RunWise.ViewModels
{
    public class MealPlannerViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MealItem> Breakfast { get; } = new ObservableCollection<MealItem>();
        public ObservableCollection<MealItem> Lunch { get; } = new ObservableCollection<MealItem>();
        public ObservableCollection<MealItem> Dinner { get; } = new ObservableCollection<MealItem>();

        public string NewBreakfastText { get; set; } = string.Empty;
        public string NewLunchText { get; set; } = string.Empty;
        public string NewDinnerText { get; set; } = string.Empty;
        public int TodayTotalCalories { get; set; }
        public string TodayTotalCaloriesDisplay => $"{TodayTotalCalories} kcal";
        public bool NoMeals => TodayTotalCalories == 0;
        private int _itemsPlannedCount;
        public int ItemsPlannedCount
        {
            get => _itemsPlannedCount;
            set { if (_itemsPlannedCount != value) { _itemsPlannedCount = value; OnPropertyChanged(nameof(ItemsPlannedCount)); } }
        }

        private int _breakfastCalories;
        public int BreakfastCalories
        {
            get => _breakfastCalories;
            set { if (_breakfastCalories != value) { _breakfastCalories = value; OnPropertyChanged(nameof(BreakfastCalories)); } }
        }

        private int _lunchCalories;
        public int LunchCalories
        {
            get => _lunchCalories;
            set { if (_lunchCalories != value) { _lunchCalories = value; OnPropertyChanged(nameof(LunchCalories)); } }
        }

        private int _dinnerCalories;
        public int DinnerCalories
        {
            get => _dinnerCalories;
            set { if (_dinnerCalories != value) { _dinnerCalories = value; OnPropertyChanged(nameof(DinnerCalories)); } }
        }

        private bool _isToday;
        public bool IsToday
        {
            get => _isToday;
            set { if (_isToday != value) { _isToday = value; OnPropertyChanged(nameof(IsToday)); } }
        }

        private bool _breakfastEmpty = true;
        public bool BreakfastEmpty
        {
            get => _breakfastEmpty;
            set { if (_breakfastEmpty != value) { _breakfastEmpty = value; OnPropertyChanged(nameof(BreakfastEmpty)); } }
        }

        private bool _lunchEmpty = true;
        public bool LunchEmpty
        {
            get => _lunchEmpty;
            set { if (_lunchEmpty != value) { _lunchEmpty = value; OnPropertyChanged(nameof(LunchEmpty)); } }
        }

        private bool _dinnerEmpty = true;
        public bool DinnerEmpty
        {
            get => _dinnerEmpty;
            set { if (_dinnerEmpty != value) { _dinnerEmpty = value; OnPropertyChanged(nameof(DinnerEmpty)); } }
        }
        // Selected date for which calories are shown
        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate != value)
                {
                    _selectedDate = value;
                    OnPropertyChanged(nameof(SelectedDate));
                    _ = LoadForDateAsync(_selectedDate);
                }
            }
        }

        public ICommand AddBreakfastCommand { get; }
        public ICommand AddLunchCommand { get; }
        public ICommand AddDinnerCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand DeleteMealCommand { get; }
        public ICommand UpdateMealCommand { get; }
        public ICommand PrevDateCommand { get; }
        public ICommand NextDateCommand { get; }
        public ICommand GoToTodayCommand { get; }

        private readonly Services.MealStore _store;

        public MealPlannerViewModel()
        {
            _store = MauiProgram.Services.GetService(typeof(Services.MealStore)) as Services.MealStore ?? new Services.MealStore();

            AddBreakfastCommand = new Command(async () => await AddBreakfastAsync());
            AddLunchCommand = new Command(async () => await AddLunchAsync());
            AddDinnerCommand = new Command(async () => await AddDinnerAsync());
            ClearAllCommand = new Command(async () => await ClearAllAsync());
            DeleteMealCommand = new Command(async (m) => await DeleteMealAsync(m as MealItem));
            UpdateMealCommand = new Command(async (m) => await UpdateMealAsync(m as MealItem));
            PrevDateCommand = new Command(() => SelectedDate = SelectedDate.AddDays(-1));
            NextDateCommand = new Command(() => SelectedDate = SelectedDate.AddDays(1));
            GoToTodayCommand = new Command(() => SelectedDate = DateTime.Today);

            // Seed with simple suggestions
            Breakfast.Add(new MealItem { Name = "Oatmeal with fruit", Calories = 350 });
            Lunch.Add(new MealItem { Name = "Grilled chicken salad", Calories = 450 });
            Dinner.Add(new MealItem { Name = "Salmon with veggies", Calories = 600 });

            // load selected date's meals from store
            _ = LoadTodayAsync();
            _store.MealsChanged += async () => await LoadForDateAsync(SelectedDate);
        }

        private async System.Threading.Tasks.Task LoadTodayAsync()
        {
            await LoadForDateAsync(SelectedDate);
        }

        private async System.Threading.Tasks.Task LoadForDateAsync(DateTime date)
        {
            var meals = await _store.GetMealsForDateAsync(date.Date);
            // replace collections
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Breakfast.Clear(); Lunch.Clear(); Dinner.Clear();
                foreach (var m in meals.Where(x => x.Type == MealType.Breakfast)) Breakfast.Add(m);
                foreach (var m in meals.Where(x => x.Type == MealType.Lunch)) Lunch.Add(m);
                foreach (var m in meals.Where(x => x.Type == MealType.Dinner)) Dinner.Add(m);
                // update total for that date
                TodayTotalCalories = meals.Sum(x => x.Calories * (int)Math.Max(1, Math.Round(x.Quantity)));
                OnPropertyChanged(nameof(TodayTotalCalories));
                // notify derived properties
                OnPropertyChanged(nameof(TodayTotalCaloriesDisplay));
                OnPropertyChanged(nameof(NoMeals));

                // per-meal calories and counts
                BreakfastCalories = Breakfast.Sum(x => x.Calories * (int)Math.Max(1, Math.Round(x.Quantity)));
                LunchCalories = Lunch.Sum(x => x.Calories * (int)Math.Max(1, Math.Round(x.Quantity)));
                DinnerCalories = Dinner.Sum(x => x.Calories * (int)Math.Max(1, Math.Round(x.Quantity)));
                ItemsPlannedCount = Breakfast.Count + Lunch.Count + Dinner.Count;
                BreakfastEmpty = Breakfast.Count == 0;
                LunchEmpty = Lunch.Count == 0;
                DinnerEmpty = Dinner.Count == 0;
                IsToday = date.Date == DateTime.Today;
                // notify any other bindings
                OnPropertyChanged(nameof(BreakfastCalories));
                OnPropertyChanged(nameof(LunchCalories));
                OnPropertyChanged(nameof(DinnerCalories));
                OnPropertyChanged(nameof(ItemsPlannedCount));
                OnPropertyChanged(nameof(IsToday));
            });

            // Persist daily summary to Supabase (best-effort)
            try
            {
                var sup = MauiProgram.Services.GetService(typeof(Services.SupabaseDatabaseService)) as Services.SupabaseDatabaseService;
                if (sup != null)
                {
                    _ = sup.UpsertCalorieSummaryAsync(date.Date, TodayTotalCalories);
                }
            }
            catch { }
        }

        public async System.Threading.Tasks.Task AddBreakfastAsync()
        {
            if (!string.IsNullOrWhiteSpace(NewBreakfastText))
            {
                var item = new MealItem { Name = NewBreakfastText.Trim(), Type = MealType.Breakfast, Date = SelectedDate.Date };
                await _store.AddMealAsync(item);
                try
                {
                    var sup = MauiProgram.Services.GetService(typeof(Services.SupabaseDatabaseService)) as Services.SupabaseDatabaseService;
                    if (sup != null)
                    {
                        _ = await sup.CreateMealAsync(item);
                    }
                }
                catch { }
                NewBreakfastText = string.Empty;
                OnPropertyChanged(nameof(NewBreakfastText));
                // refresh today's view after adding
                await LoadTodayAsync();
            }
        }

        public async System.Threading.Tasks.Task AddLunchAsync()
        {
            if (!string.IsNullOrWhiteSpace(NewLunchText))
            {
                var item = new MealItem { Name = NewLunchText.Trim(), Type = MealType.Lunch, Date = SelectedDate.Date };
                await _store.AddMealAsync(item);
                try
                {
                    var sup = MauiProgram.Services.GetService(typeof(Services.SupabaseDatabaseService)) as Services.SupabaseDatabaseService;
                    if (sup != null)
                    {
                        _ = await sup.CreateMealAsync(item);
                    }
                }
                catch { }
                NewLunchText = string.Empty;
                OnPropertyChanged(nameof(NewLunchText));
                // refresh today's view after adding
                await LoadTodayAsync();
            }
        }

        public async System.Threading.Tasks.Task AddDinnerAsync()
        {
            if (!string.IsNullOrWhiteSpace(NewDinnerText))
            {
                var item = new MealItem { Name = NewDinnerText.Trim(), Type = MealType.Dinner, Date = SelectedDate.Date };
                await _store.AddMealAsync(item);
                try
                {
                    var sup = MauiProgram.Services.GetService(typeof(Services.SupabaseDatabaseService)) as Services.SupabaseDatabaseService;
                    if (sup != null)
                    {
                        _ = await sup.CreateMealAsync(item);
                    }
                }
                catch { }
                NewDinnerText = string.Empty;
                OnPropertyChanged(nameof(NewDinnerText));
                // refresh today's view after adding
                await LoadTodayAsync();
            }
        }

        private async System.Threading.Tasks.Task ClearAllAsync()
        {
            // delete all today's meals
            var today = DateTime.Today;
            var meals = await _store.GetMealsForDateAsync(today);
            foreach (var m in meals)
                await _store.DeleteMealAsync(m);
            await LoadTodayAsync();
        }

        public async System.Threading.Tasks.Task DeleteMealAsync(MealItem meal)
        {
            if (meal == null) return;
            await _store.DeleteMealAsync(meal);
            await LoadTodayAsync();
        }

        public async System.Threading.Tasks.Task UpdateMealAsync(MealItem meal)
        {
            if (meal == null) return;
            await _store.UpdateMealAsync(meal);
            await LoadTodayAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
