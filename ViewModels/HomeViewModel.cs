
//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using RunWise;
//using RunWise.Models;
//using System.Text.Json;
//using System.Collections.ObjectModel;
//using System.Globalization;

//public partial class HomeViewModel : ObservableObject
//{
//    // Observable collection used by the UI for the weekly mileage chart
//    public ObservableCollection<DayBar> WeeklyBars { get; } = new ObservableCollection<DayBar>();
//    [ObservableProperty]
//    private string recentRunDate = "";
//    [ObservableProperty]
//    private string recentDistance = "";
//    [ObservableProperty]
//    private string recentDuration = "";
//    [ObservableProperty]
//    private string recentAvgPace = "";
//    [ObservableProperty]
//    private bool hasRecentRun = false;
//    [ObservableProperty]
//    private bool noRuns = true;
//    public class DayBar
//    {
//        public string DayLabel { get; set; } = "";
//        public double Kilometers { get; set; }
//        public double BarHeight { get; set; }
//        public string DistanceLabel { get; set; } = "";
//    }
//    public async Task LoadDashboard()
//    {
//        try
//        {
//            // Use RunTrackerService to load runs (will prefer remote when signed in)
//            var tracker = MauiProgram.Services.GetService(typeof(RunWise.Services.RunTrackerService)) as RunWise.Services.RunTrackerService
//                ?? new RunWise.Services.RunTrackerService();

//            var runs = await tracker.GetAllRunsAsync();

//            // check if the data has runs
//            if (runs == null || runs.Count == 0)
//            {
//                NoRuns = true;
//                HasRecentRun = false; return;
//            }



//            var recentRun = runs.OrderByDescending(r => r.StartTime).FirstOrDefault();
//            if (recentRun == null)
//            {
//                NoRuns = true; HasRecentRun = false; return;
//            }
//            RecentRunDate = recentRun.StartTime.ToString("MMM dd, yyyy - HH:mm");
//            RecentDistance = $"{recentRun.DistanceFormatted} km";
//            RecentDuration = recentRun.DurationFormatted;
//            RecentAvgPace = recentRun.AveragePaceFormatted;

//            NoRuns = false;
//            HasRecentRun = true;

//            // Build weekly mileage bars (Monday -> Sunday)
//            try
//            {
//                WeeklyBars.Clear();

//                // find the Monday of the current week
//                var today = DateTime.Today;
//                var startOfWeek = today;
//                while (startOfWeek.DayOfWeek != DayOfWeek.Monday)
//                    startOfWeek = startOfWeek.AddDays(-1);

//                // compute totals per day
//                var totals = new double[7];
//                for (int i = 0; i < 7; i++)
//                {
//                    var day = startOfWeek.AddDays(i);
//                    totals[i] = runs.Where(r => r.StartTime.Date == day.Date).Sum(r => r.DistanceKm);
//                }

//                var max = totals.Max();
//                var chartHeight = 120.0; // pixels for the tallest bar

//                for (int i = 0; i < 7; i++)
//                {
//                    var day = startOfWeek.AddDays(i);
//                    var km = Math.Round(totals[i], 2);
//                    var height = max > 0 ? (km / max) * chartHeight : 0.0;
//                    WeeklyBars.Add(new DayBar
//                    {
//                        DayLabel = day.ToString("ddd", CultureInfo.InvariantCulture),
//                        Kilometers = km,
//                        BarHeight = height,
//                        DistanceLabel = km > 0 ? km.ToString("0.0") + " km" : "0"
//                    });
//                }
//            }
//            catch { }

//        }
//        catch (Exception ex)
//        {
//            var openRouter = new OpenRouterService();
//            var cue = await openRouter.GetCoachingCueAsync("Runner increased speed during their run, give encouragement. or call him a good boy or something");

//            if (cue != null)
//            {
//                await TextToSpeech.Default.SpeakAsync(cue);
//            }
//            else
//            {
//                // fallback canned phrase
//                await TextToSpeech.Default.SpeakAsync("Keep going, you've got this!");
//            }
//        }




//    }
//}


using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunWise;
using RunWise.Models;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Globalization;

public partial class HomeViewModel : ObservableObject
{
    public HomeViewModel()
    {
        // Subscribe to meal store changes so Today's statistics update in real time
        try
        {
            var mealStore = MauiProgram.Services.GetService(typeof(RunWise.Services.MealStore)) as RunWise.Services.MealStore;
            if (mealStore != null)
            {
                mealStore.MealsChanged += async () => await LoadDashboard();
            }
        }
        catch { }
    }

    // Observable collection used by the UI for the weekly mileage chart
    public ObservableCollection<DayBar> WeeklyBars { get; } = new ObservableCollection<DayBar>();
    [ObservableProperty]
    private string recentRunDate = "";
    [ObservableProperty]
    private string recentDistance = "";
    [ObservableProperty]
    private string recentDuration = "";
    [ObservableProperty]
    private string recentAvgPace = "";
    [ObservableProperty]
    private string todayDistance = "0 km";
    [ObservableProperty]
    private string todaySteps = "0";
    [ObservableProperty]
    private string todayCalories = "0 kcal";
    [ObservableProperty]
    private string longestDistance = "0 km";
    [ObservableProperty]
    private string longestDuration = "00:00:00";
    [ObservableProperty]
    private string bestPace = "0 km/h";
    [ObservableProperty]
    private bool hasRecentRun = false;
    [ObservableProperty]
    private bool noRuns = true;
    public class DayBar
    {
        public string DayLabel { get; set; } = "";
        public double Kilometers { get; set; }
        public double BarHeight { get; set; }
        public string DistanceLabel { get; set; } = "";
    }
    public async Task LoadDashboard()
    {
        try
        {
            // Use RunTrackerService to load runs (will prefer remote when signed in)
            var tracker = MauiProgram.Services.GetService(typeof(RunWise.Services.RunTrackerService)) as RunWise.Services.RunTrackerService
                ?? new RunWise.Services.RunTrackerService();

            var runs = await tracker.GetAllRunsAsync();

            // check if the data has runs
            if (runs == null || runs.Count == 0)
            {
                NoRuns = true;
                HasRecentRun = false; return;
            }



            var recentRun = runs.OrderByDescending(r => r.StartTime).FirstOrDefault();
            if (recentRun == null)
            {
                NoRuns = true; HasRecentRun = false; return;
            }
            RecentRunDate = recentRun.StartTime.ToString("MMM dd, yyyy - HH:mm");
            RecentDistance = $"{recentRun.DistanceFormatted} km";
            RecentDuration = recentRun.DurationFormatted;
            RecentAvgPace = recentRun.AveragePaceFormatted;

            NoRuns = false;
            HasRecentRun = true;

            // compute today's aggregated stats (distance, steps, calories)
            try
            {
                var todayLocal = DateTime.Today;
                var todaysRuns = runs.Where(r => ToLocal(r.StartTime).Date == todayLocal).ToList();
                var totalKm = todaysRuns.Sum(r => r.DistanceKm);
                var totalSteps = todaysRuns.Sum(r => r.Steps);
                var totalCalories = todaysRuns.Sum(r => r.Calories);
            TodayDistance = $"{Math.Round(totalKm, 2)} km";
            TodaySteps = totalSteps.ToString();

            // Include meal planner calories for today
            try
            {
                var mealStore = MauiProgram.Services.GetService(typeof(RunWise.Services.MealStore)) as RunWise.Services.MealStore;
                if (mealStore != null)
                {
                    var mealCals = await mealStore.GetTotalCaloriesForDateAsync(DateTime.Today);
                    totalCalories += mealCals;
                }
            }
            catch { }

            TodayCalories = $"{totalCalories} kcal";
                // Compute overall records
                try
                {
                    var maxDistanceRun = runs.OrderByDescending(r => r.DistanceKm).FirstOrDefault();
                    if (maxDistanceRun != null)
                        LongestDistance = $"{Math.Round(maxDistanceRun.DistanceKm, 2)} km";

                    var maxDurationRun = runs.OrderByDescending(r => r.Duration.TotalSeconds).FirstOrDefault();
                    if (maxDurationRun != null)
                        LongestDuration = maxDurationRun.DurationFormatted;

                    var bestPaceVal = runs.Where(r => r.Duration.TotalSeconds > 0).Select(r => r.AveragePaceKmh).DefaultIfEmpty(0).Max();
                    BestPace = $"{Math.Round(bestPaceVal, 2)} km/h";
                }
                catch { }
            }
            catch { }

            // Build weekly mileage bars (Monday -> Sunday)
            try
            {
                WeeklyBars.Clear();

                // find the Monday of the current week
                var today = DateTime.Today;
                var startOfWeek = today;
                while (startOfWeek.DayOfWeek != DayOfWeek.Monday)
                    startOfWeek = startOfWeek.AddDays(-1);

                // compute totals per day
                // compute totals per day for the current local Monday-Sunday week
                var totals = new double[7];

                var cal = CultureInfo.CurrentCulture.Calendar;
                var dfi = CultureInfo.CurrentCulture.DateTimeFormat;
                var rule = dfi.CalendarWeekRule;
                var firstDayOfWeek = DayOfWeek.Monday; // enforce Monday-Sunday weeks

                var currentWeek = cal.GetWeekOfYear(today, rule, firstDayOfWeek);
                var currentYear = today.Year;

                for (int i = 0; i < 7; i++)
                {
                    var day = startOfWeek.AddDays(i);
                    totals[i] = runs.Where(r => r.StartTime.Date == day.Date).Sum(r => r.DistanceKm);
                    totals[i] = runs.Where(r =>
                    {
                        var runLocal = ToLocal(r.StartTime).Date;
                        var runWeek = cal.GetWeekOfYear(runLocal, rule, firstDayOfWeek);
                        return runWeek == currentWeek && runLocal.Year == currentYear && runLocal == day.Date;
                    }).Sum(r => r.DistanceKm);
                }

                var max = totals.Max();
                var chartHeight = 120.0; // pixels for the tallest bar

                for (int i = 0; i < 7; i++)
                {
                    var day = startOfWeek.AddDays(i);
                    var km = Math.Round(totals[i], 2);
                    var height = max > 0 ? (km / max) * chartHeight : 0.0;
                    WeeklyBars.Add(new DayBar
                    {
                        DayLabel = day.ToString("ddd", CultureInfo.InvariantCulture),
                        Kilometers = km,
                        BarHeight = height,
                        DistanceLabel = km > 0 ? km.ToString("0.0") + " km" : "0"
                    });
                }
            }
            catch { }

            DateTime ToLocal(DateTime dt)
            {
                if (dt.Kind == DateTimeKind.Local)
                    return dt;
                if (dt.Kind == DateTimeKind.Utc)
                    return dt.ToLocalTime();
                // Unspecified: assume stored as UTC (common with JSON storage) and convert to local
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime();
            }

        }
        catch (Exception ex)
        {
            var openRouter = new OpenRouterService();
            var cue = await openRouter.GetCoachingCueAsync("Runner increased speed 15% during their run, give encouragement. or call him a good boy or something");

            if (cue != null)
            {
                await TextToSpeech.Default.SpeakAsync(cue);
            }
            else
            {
                // fallback canned phrase
                await TextToSpeech.Default.SpeakAsync("Keep going, you've got this!");
            }
        }




    }
}