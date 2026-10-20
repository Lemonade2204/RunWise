using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunWise.Models;
using RunWise.Services;

namespace RunWise.ViewModels;

public enum StretchKind
{
    PreRun,
    PostRun,
}

public partial class StretchRoutineViewModel : ObservableObject
{
    // UI level name -> level key understood by ExercisesViewModel.CreateRoutineAsync
    private static readonly Dictionary<string, string> LevelKeys = new()
    {
        ["Easy"] = "beginner",
        ["Intermediate"] = "intermediate",
        ["Hard"] = "advance",
    };

    private readonly ExercisesViewModel _exercises;
    private readonly RunTrackerService? _runs;
    private bool _userPickedLevel;

    public ObservableCollection<StretchMoveItem> Moves { get; } = new();
    public List<Exercise> Routine { get; private set; } = new();

    public StretchKind Kind { get; }
    public bool IsPostRun => Kind == StretchKind.PostRun;

    // Copy and colours that differ between the warm-up and the cool-down
    public string PageTitle => IsPostRun ? "Post Run Stretch" : "Pre Run Stretch";
    public string HeroEyebrow => IsPostRun ? "POST-RUN COOL-DOWN" : "PRE-RUN WARM-UP";
    public string HeroTitle => IsPostRun ? "Ease out your legs" : "Wake up your legs";
    public string StartText => IsPostRun ? "Start Cool-Down" : "Start Warm-Up";
    public string ListTitle => IsPostRun ? "After you run" : "Before you run";
    public string ListHint => IsPostRun ? "Hold, don't bounce" : "Keep moving";
    public Color HeroStartColor => IsPostRun ? Color.FromArgb("#4A2BCB") : Color.FromArgb("#5B3CDD");
    public Color HeroEndColor => IsPostRun ? Color.FromArgb("#2B2580") : Color.FromArgb("#4A2BCB");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEasy), nameof(IsIntermediate), nameof(IsHard))]
    private string _selectedLevel = "Intermediate";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEasyRecommended), nameof(IsIntermediateRecommended), nameof(IsHardRecommended))]
    private string _recommendedLevel = "Intermediate";

    [ObservableProperty]
    private string _recommendationText = "Checking your recent runs...";

    [ObservableProperty]
    private string _durationText = string.Empty;

    [ObservableProperty]
    private string _movesText = string.Empty;

    public bool IsEasy => SelectedLevel == "Easy";
    public bool IsIntermediate => SelectedLevel == "Intermediate";
    public bool IsHard => SelectedLevel == "Hard";

    public bool IsEasyRecommended => RecommendedLevel == "Easy";
    public bool IsIntermediateRecommended => RecommendedLevel == "Intermediate";
    public bool IsHardRecommended => RecommendedLevel == "Hard";

    public string SelectedLevelKey => LevelKeys[SelectedLevel];

    public StretchRoutineViewModel(StretchKind kind = StretchKind.PreRun)
    {
        Kind = kind;
        _exercises = MauiProgram.Services.GetService(typeof(ExercisesViewModel)) as ExercisesViewModel ?? new ExercisesViewModel();
        _runs = MauiProgram.Services.GetService(typeof(RunTrackerService)) as RunTrackerService;
        _ = LoadRoutineAsync();
    }

    [RelayCommand]
    private async Task SelectLevel(string level)
    {
        if (!LevelKeys.ContainsKey(level)) return;
        _userPickedLevel = true;
        SelectedLevel = level;
        await LoadRoutineAsync();
    }

    public async Task LoadRecommendationAsync()
    {
        var (level, text) = await ComputeRecommendationAsync();
        RecommendedLevel = level;
        RecommendationText = text;

        if (!_userPickedLevel && SelectedLevel != level)
        {
            SelectedLevel = level;
            await LoadRoutineAsync();
        }
    }

    private async Task<(string Level, string Text)> ComputeRecommendationAsync()
    {
        try
        {
            if (_runs == null)
                return ("Easy", "Easy is a gentle place to start.");

            var runs = await _runs.GetAllRunsAsync();

            // After a run, size the cool-down to the run that was just finished.
            if (IsPostRun)
            {
                var last = runs.Where(r => r.StartTime >= DateTime.Now.AddHours(-4))
                               .OrderByDescending(r => r.StartTime)
                               .FirstOrDefault();
                if (last != null)
                {
                    var km = last.DistanceMeters / 1000.0;
                    var fit = km >= 8 ? "Hard" : km >= 3 ? "Intermediate" : "Easy";
                    return (fit, $"After your {km:0.0} km run, {fit} will help you recover.");
                }
            }

            var cutoff = DateTime.Now.AddDays(-21);
            var recent = runs.Where(r => r.StartTime >= cutoff).ToList();
            if (recent.Count == 0)
                return ("Easy", "No recent runs yet, so Easy is a gentle place to start.");

            var avgKm = recent.Average(r => r.DistanceMeters) / 1000.0;
            string level;
            if (avgKm >= 8 || (recent.Count >= 6 && avgKm >= 5))
                level = "Hard";
            else if (avgKm >= 3 || recent.Count >= 3)
                level = "Intermediate";
            else
                level = "Easy";

            return (level, $"Based on your recent runs, {level} is a good fit.");
        }
        catch
        {
            return ("Intermediate", "Intermediate is a good all-round fit.");
        }
    }

    private async Task LoadRoutineAsync()
    {
        Routine = await _exercises.CreateRoutineAsync(IsPostRun ? "post" : "pre", SelectedLevelKey);

        Moves.Clear();
        for (var i = 0; i < Routine.Count; i++)
            Moves.Add(new StretchMoveItem(Routine[i], i));

        var totalSeconds = Routine.Sum(e => StretchTiming.MoveSeconds(e) + StretchTiming.TransitionSeconds);
        DurationText = $"{Math.Max(1, (int)Math.Ceiling(totalSeconds / 60.0))} min";
        MovesText = $"{Routine.Count} {(IsPostRun ? "stretches" : "moves")}";
    }
}

public class StretchMoveItem
{
    public StretchMoveItem(Exercise exercise, int index)
    {
        Exercise = exercise;
        Index = index;
    }

    public Exercise Exercise { get; }
    public int Index { get; }
    public int Number => Index + 1;
    public bool IsFirst => Index == 0;
    public string Title => Exercise.Title;
    public string Focus => Exercise.Focus;

    // "30s" -> "30 sec", "30s each leg" -> "30 sec / leg", "8 each leg" -> "8 / leg", "10 reps" -> "10 reps"
    public string AmountText
    {
        get
        {
            var d = Exercise.Description.Trim();
            var m = Regex.Match(d, "^(\\d+)\\s*s$", RegexOptions.IgnoreCase);
            if (m.Success) return $"{m.Groups[1].Value} sec";
            m = Regex.Match(d, "^(\\d+)\\s*s\\s+each\\s+(\\w+)$", RegexOptions.IgnoreCase);
            if (m.Success) return $"{m.Groups[1].Value} sec / {m.Groups[2].Value}";
            m = Regex.Match(d, "^(\\d+)\\s*each\\s+(\\w+)$", RegexOptions.IgnoreCase);
            if (m.Success) return $"{m.Groups[1].Value} / {m.Groups[2].Value}";
            return d;
        }
    }
}

public static class StretchTiming
{
    public const int TransitionSeconds = 15;

    // Timed holds start with a number of seconds, e.g. "30s" or "45s each leg".
    public static bool IsTimed(Exercise e) => Regex.IsMatch(e.Description.Trim(), @"^\d+\s*s\b", RegexOptions.IgnoreCase);

    public static bool IsRepBased(Exercise e)
    {
        if (IsTimed(e)) return false;
        var amount = e.Description.ToLowerInvariant();
        return amount.Contains("rep") || amount.Contains("each");
    }

    // Timed holds done once per side ("30s each leg") - the session switches sides halfway.
    public static bool IsPerSideHold(Exercise e) => IsTimed(e) && e.Description.ToLowerInvariant().Contains("each");

    // Seconds a move takes: timed holds use their duration, reps take ~3s each (doubled for "each leg").
    public static int MoveSeconds(Exercise e)
    {
        var amount = e.Description.ToLowerInvariant();
        var n = int.TryParse(Regex.Match(amount, @"\d+").Value, out var v) ? v : 0;
        var sides = amount.Contains("each") ? 2 : 1;

        if (IsRepBased(e))
            return Math.Max(10, n * 3 * sides);
        return Math.Max(10, n * sides);
    }
}
