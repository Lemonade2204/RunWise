using CommunityToolkit.Mvvm.ComponentModel;
using RunWise.Models;
using System.Text.Json;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty] 
    private string recentRunDate = "";
    [ObservableProperty] 
    private string recentDistance = "";
    [ObservableProperty]
    private string recentDuration = "";
    [ObservableProperty] 
    private string recentAvgPace = "";
    [ObservableProperty] 
    private bool hasRecentRun = false;
    [ObservableProperty] 
    private bool noRuns = true;

    public async Task LoadDashboard()
    {
        try
        {
            var dataPath = Path.Combine(FileSystem.AppDataDirectory, "run_history.json");

            if (!File.Exists(dataPath)) { NoRuns = true; HasRecentRun = false; return; }

            var json = await File.ReadAllTextAsync(dataPath);
            var runs = JsonSerializer.Deserialize<List<RunSession>>(json) ?? new();

            //check if the data has runs
            if (runs.Count == 0) 
            { 
                NoRuns = true; 
                HasRecentRun = false; return; 
            }

            var recentRun = runs.OrderByDescending(r => r.StartTime).First();
            RecentRunDate = recentRun.StartTime.ToString("MMM dd, yyyy - HH:mm");
            RecentDistance = $"{recentRun.DistanceFormatted} km";
            RecentDuration = recentRun.DurationFormatted;
            RecentAvgPace = recentRun.AveragePaceFormatted;

            NoRuns = false;
            HasRecentRun = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Dashboard load error: {ex.Message}");
        }
    }
}