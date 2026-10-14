using System;
using System.Threading.Tasks;
using RunWise.Models;
using System.Globalization;
using RunWise.Services;
using Microsoft.Maui.Controls;

namespace RunWise.Pages;

[QueryProperty(nameof(RunId), "runId")]
public partial class EditRunPage : ContentPage
{
    private RunSession? _run;
    private readonly RunTrackerService _runService;
    private readonly SupabaseAuthService? _auth;
    private RunWise.Models.Profile? _loadedProfile;

   public EditRunPage()
    {
        InitializeComponent();
        BindingContext = this;
        _runService = IPlatformApplication.Current?.Services.GetService<RunTrackerService>() ?? new RunTrackerService();
        _auth = IPlatformApplication.Current?.Services.GetService<SupabaseAuthService>();
        _ = LoadProfileAsync();
    }

    public string RunId
    {
        set
        {
            if (!string.IsNullOrEmpty(value))
                _ = LoadRunAsync(value);
        }
    }

    // Bindable properties for UI
    public DateTime StartDate { get; set; } = DateTime.Now.Date;
    public TimeSpan StartTimeOfDay { get; set; } = DateTime.Now.TimeOfDay;
    public string DistanceKm { get; set; } = "0";
    public string DurationText { get; set; } = "00:00:00";
    // new bindable fields for steps and calories (text for easier binding/parsing)
    public string StepsText { get; set; } = "0";
    public string CaloriesText { get; set; } = "0";

    // Constants used to estimate steps and calories from distance
    private const double StepsPerKm = 1300.0; // approximation used elsewhere in the app
    private const double CaloriesPerKm = 62.0; // approximation used elsewhere in the app

    private async Task LoadRunAsync(string id)
    {
        var run = await _runService.GetRunByIdAsync(id);
        if (run == null) return;
        _run = run;
        StartDate = run.StartTime.Date;
        StartTimeOfDay = run.StartTime.TimeOfDay;
        DistanceKm = (run.DistanceMeters / 1000.0).ToString("F2");
        DurationText = run.Duration.ToString(@"hh\\:mm\\:ss");
        StepsText = run.Steps.ToString();
        // Format calories as integer string
        CaloriesText = run.Calories.ToString(CultureInfo.InvariantCulture);

        // Recalculate derived stats from the loaded distance to ensure UI shows calculated values
        await RecalculateDerivedStatsAsync();

        // Refresh bindings
        OnPropertyChanged(nameof(StartDate));
        OnPropertyChanged(nameof(StartTimeOfDay));
        OnPropertyChanged(nameof(DistanceKm));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(StepsText));
        OnPropertyChanged(nameof(CaloriesText));
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_run == null) return;

        // Parse distance and duration
        if (double.TryParse(DistanceKm, out var km))
        {
            _run.DistanceMeters = km * 1000.0;
        }

        if (TimeSpan.TryParse(DurationText, out var dur))
        {
            _run.Duration = dur;
        }

        // Combine date and time
        _run.StartTime = StartDate.Date + StartTimeOfDay;
        if (_run.Duration > TimeSpan.Zero)
            _run.EndTime = _run.StartTime + _run.Duration;
        // Calculate steps and calories from distance (do not allow manual editing)
        var kmVal = _run.DistanceMeters / 1000.0;
        // steps estimate may use height when available
        if (_loadedProfile != null && _loadedProfile.HeightCm.HasValue && _loadedProfile.HeightCm.Value > 0)
        {
            var heightM = _loadedProfile.HeightCm.Value / 100.0;
            // approximate step length as 0.415 * height (common approximation)
            var stepLength = 0.415 * heightM; // meters
            var stepsPerKmFromHeight = (int)Math.Round(1000.0 / stepLength);
            _run.Steps = (int)Math.Round(kmVal * stepsPerKmFromHeight);
        }
        else
        {
            _run.Steps = (int)Math.Round(kmVal * StepsPerKm);
        }

        // calories: prefer weight-based estimate (approx 1 kcal per kg per km)
        if (_loadedProfile != null && _loadedProfile.WeightKg.HasValue && _loadedProfile.WeightKg.Value > 0)
        {
            _run.Calories = (int)Math.Round(_loadedProfile.WeightKg.Value * kmVal * 1.0);
        }
        else
        {
            _run.Calories = (int)Math.Round(kmVal * CaloriesPerKm);
        }

        // Update UI texts to reflect calculated values
        StepsText = _run.Steps.ToString();
        CaloriesText = _run.Calories.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(StepsText));
        OnPropertyChanged(nameof(CaloriesText));

        await _runService.UpdateRunAsync(_run);

        // Return to previous page (History). HistoryPage.OnAppearing will reload the list.
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnDistanceChanged(object sender, TextChangedEventArgs e)
    {
        _ = RecalculateDerivedStatsAsync();
    }

    private void OnDurationChanged(object sender, TextChangedEventArgs e)
    {
        _ = RecalculateDerivedStatsAsync();
    }

    private async Task RecalculateDerivedStatsAsync()
    {
        // Ensure we have loaded profile info
        if (_loadedProfile == null && _auth != null)
        {
            await LoadProfileAsync();
        }

        // Try parse distance string into kilometers
        if (double.TryParse(DistanceKm, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var km) ||
            double.TryParse(DistanceKm, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out km))
        {
            int steps;
            int cals;

            if (_loadedProfile != null && _loadedProfile.HeightCm.HasValue && _loadedProfile.HeightCm.Value > 0)
            {
                var heightM = _loadedProfile.HeightCm.Value / 100.0;
                var stepLength = 0.415 * heightM;
                var stepsPerKmFromHeight = (int)Math.Round(1000.0 / stepLength);
                steps = (int)Math.Round(km * stepsPerKmFromHeight);
            }
            else
            {
                steps = (int)Math.Round(km * StepsPerKm);
            }

            if (_loadedProfile != null && _loadedProfile.WeightKg.HasValue && _loadedProfile.WeightKg.Value > 0)
            {
                cals = (int)Math.Round(_loadedProfile.WeightKg.Value * km * 1.0);
            }
            else
            {
                cals = (int)Math.Round(km * CaloriesPerKm);
            }

            StepsText = steps.ToString();
            CaloriesText = cals.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(StepsText));
            OnPropertyChanged(nameof(CaloriesText));
        }
    }

    private async Task LoadProfileAsync()
    {
        if (_auth == null) return;
        try
        {
            var (ok, err, profile) = await _auth.GetProfileAsync();
            if (ok && profile != null)
            {
                _loadedProfile = profile;
            }
        }
        catch { }
    }
}
