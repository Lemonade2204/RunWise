using System;
using System.Threading.Tasks;
using RunWise.Models;
using RunWise.Services;
using Microsoft.Maui.Controls;

namespace RunWise.Pages;

[QueryProperty(nameof(RunId), "runId")]
public partial class EditRunPage : ContentPage
{
    private RunSession? _run;
    private readonly RunTrackerService _runService;

   public EditRunPage()
    {
        InitializeComponent();
        BindingContext = this;
        _runService = IPlatformApplication.Current?.Services.GetService<RunTrackerService>() ?? new RunTrackerService();
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

    private async Task LoadRunAsync(string id)
    {
        var run = await _runService.GetRunByIdAsync(id);
        if (run == null) return;
        _run = run;
        StartDate = run.StartTime.Date;
        StartTimeOfDay = run.StartTime.TimeOfDay;
        DistanceKm = (run.DistanceMeters / 1000.0).ToString("F2");
        DurationText = run.Duration.ToString(@"hh\\:mm\\:ss");

       // Refresh bindings
        OnPropertyChanged(nameof(StartDate));
        OnPropertyChanged(nameof(StartTimeOfDay));
        OnPropertyChanged(nameof(DistanceKm));
        OnPropertyChanged(nameof(DurationText));
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

        await _runService.UpdateRunAsync(_run);

        // Navigate back
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
