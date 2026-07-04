
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunWise;
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