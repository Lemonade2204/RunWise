using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunWise.Models;
using RunWise.Services;
using System.Collections.ObjectModel;

namespace RunWise.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly RunTrackerService _runTrackerService;

    public HistoryViewModel()
    {
        _runTrackerService =
            IPlatformApplication.Current?.Services.GetService<RunTrackerService>()
            ?? new RunTrackerService();
    }

    [ObservableProperty]
    ObservableCollection<RunSession> runHistory = new();

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
    }
}