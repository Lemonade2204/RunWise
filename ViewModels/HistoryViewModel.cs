using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunWise.Models;
using RunWise.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace RunWise.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly RunTrackerService _runTrackerService;

    public HistoryViewModel()
    {
        _runTrackerService =
            IPlatformApplication.Current?.Services.GetService<RunTrackerService>()
            ?? new RunTrackerService();
        // update summary whenever collection changes
        RunHistory.CollectionChanged += (s, e) => UpdateSummary();
    }

    [ObservableProperty]
    ObservableCollection<RunSession> runHistory = new();

    private int _totalRuns = 0;
    public int TotalRuns
    {
        get => _totalRuns;
        set => SetProperty(ref _totalRuns, value);
    }

    private double _totalDistanceKm = 0.0;
    public double TotalDistanceKm
    {
        get => _totalDistanceKm;
        set => SetProperty(ref _totalDistanceKm, value);
    }

    private int _totalCalories = 0;
    public int TotalCalories
    {
        get => _totalCalories;
        set => SetProperty(ref _totalCalories, value);
    }

    private string _overallAveragePaceFormatted = "-";
    public string OverallAveragePaceFormatted
    {
        get => _overallAveragePaceFormatted;
        set => SetProperty(ref _overallAveragePaceFormatted, value);
    }

    [ObservableProperty]
    bool isLoading = false;

    [RelayCommand]
    async Task LoadHistory()
    {
        IsLoading = true;
        try
        {
            var records = await _runTrackerService.GetAllRunsAsync();
            RunHistory.Clear();
            foreach (var record in records)
                RunHistory.Add(record);
            UpdateSummary();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Load error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    async Task DeleteRun(RunSession run)
    {
        if (run == null) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Delete Run",
            $"Delete run from {run.StartTime:MMM dd, yyyy}?",
            "Delete", "Cancel");

        if (!confirm) return;

        await _runTrackerService.DeleteRunAsync(run.Id);
        RunHistory.Remove(run);
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        TotalRuns = RunHistory.Count;
        TotalDistanceKm = Math.Round(RunHistory.Sum(r => r.DistanceKm), 2);
        TotalCalories = RunHistory.Sum(r => r.Calories);

        // Overall average pace (km/h): total distance / total hours across all runs
        var totalHours = RunHistory.Sum(r => r.Duration.TotalHours);
        if (totalHours > 0 && TotalDistanceKm > 0)
        {
            var avgKmh = TotalDistanceKm / totalHours;
            OverallAveragePaceFormatted = Math.Round(avgKmh, 2) + " km/h";
        }
        else
        {
            OverallAveragePaceFormatted = "-";
        }
    }
}