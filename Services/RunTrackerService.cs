using RunWise.Models;
using System.Text.Json;

namespace RunWise.Services;

public class RunTrackerService
{
    private readonly string _dataPath;
    private const string HistoryFileName = "run_history.json";

    public RunTrackerService()
    {
        _dataPath = Path.Combine(FileSystem.AppDataDirectory, HistoryFileName);
    }

    public async Task SaveRunAsync(RunSession run)
    {
        try
        {
            var allRuns = await GetAllRunsAsync();
            allRuns.Add(run);

            var json = JsonSerializer.Serialize(allRuns, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_dataPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving run: {ex.Message}");
        }
    }

    public async Task<List<RunSession>> GetAllRunsAsync()
    {
        try
        {
            if (!File.Exists(_dataPath))
                return new List<RunSession>();

            var json = await File.ReadAllTextAsync(_dataPath);
            var runs = JsonSerializer.Deserialize<List<RunSession>>(json) ?? new List<RunSession>();
            return runs.OrderByDescending(r => r.StartTime).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading runs: {ex.Message}");
            return new List<RunSession>();
        }
    }

    public async Task<RunSession?> GetRunByIdAsync(string id)
    {
        try
        {
            var allRuns = await GetAllRunsAsync();
            return allRuns.FirstOrDefault(r => r.Id == id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting run: {ex.Message}");
            return null;
        }
    }

    public async Task UpdateRunAsync(RunSession run)
    {
        try
        {
            var allRuns = await GetAllRunsAsync();
            var idx = allRuns.FindIndex(r => r.Id == run.Id);
            if (idx >= 0)
                allRuns[idx] = run;
            else
                allRuns.Add(run);

            var json = JsonSerializer.Serialize(allRuns, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_dataPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating run: {ex.Message}");
        }
    }

    public async Task DeleteRunAsync(string id)
    {
        try
        {
            var allRuns = await GetAllRunsAsync();
            allRuns.RemoveAll(r => r.Id == id);

            var json = JsonSerializer.Serialize(allRuns, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_dataPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting run: {ex.Message}");
        }
    }

    public async Task ClearAllRunsAsync()
    {
        try
        {
            if (File.Exists(_dataPath))
                File.Delete(_dataPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing runs: {ex.Message}");
        }
    }
}
