using RunWise.Models;
using System.Text.Json;
using System.Linq;

namespace RunWise.Services;

public class RunTrackerService
{
    private readonly string _dataPath;
    private const string HistoryFileName = "run_history.json";
    private readonly SupabaseDatabaseService? _db;
    private readonly SupabaseAuthService? _auth;

    public RunTrackerService()
    {
        _dataPath = Path.Combine(FileSystem.AppDataDirectory, HistoryFileName);
        // Attempt to get Supabase services from the static service provider if available
        try
        {
            _db = MauiProgram.Services.GetService(typeof(SupabaseDatabaseService)) as SupabaseDatabaseService;
            _auth = MauiProgram.Services.GetService(typeof(SupabaseAuthService)) as SupabaseAuthService;
        }
        catch
        {
            _db = null;
            _auth = null;
        }
    }

    public async Task SaveRunAsync(RunSession run)
    {
        try
        {
            var allRuns = await GetAllRunsAsync();
            // set owner on run when available
            try
            {
                if (_auth != null && !string.IsNullOrEmpty(_auth.UserId))
                    run.UserId = _auth.UserId;
            }
            catch { }
            allRuns.Add(run);

            var json = JsonSerializer.Serialize(allRuns, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_dataPath, json);
            // Also try to sync to Supabase if available
            try
            {
                if (_db != null && _auth != null && !string.IsNullOrEmpty(_auth.UserId))
                {
                    var (ok, err) = await _db.CreateRunFromSessionAsync(run);
                    if (!ok)
                    {
                        Console.WriteLine($"Supabase create run failed: {err}");
                        // Surface error to the user on the UI thread
                        try
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                var page = Application.Current?.MainPage;
                                if (page != null)
                                    await page.DisplayAlert("Sync error", "Failed to upload run to server: " + (err ?? "Unknown error"), "OK");
                            });
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing run to Supabase: {ex.Message}");
            }
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
            // If Supabase available and user signed in, fetch remote runs
            if (_db != null && _auth != null && !string.IsNullOrEmpty(_auth.UserId))
            {
                var (ok, err, runs) = await _db.GetRunsForCurrentUserAsync();
                if (ok && runs != null)
                {
                    // Convert RunData to RunSession
                    var list = runs.Select(r => {
                        var session = new RunSession
                        {
                            Id = r.Id.ToString(),
                            StartTime = r.CreatedAt,
                            EndTime = r.CreatedAt.AddSeconds(r.DurationSeconds),
                            Duration = TimeSpan.FromSeconds(r.DurationSeconds),
                            DistanceMeters = r.DistanceKm * 1000
                        };
                        // set owner id if available
                        try { session.UserId = r.UserId.ToString(); } catch { }
                        // map route points if present
                        if (r.RoutePoints != null)
                        {
                            session.RoutePoints = r.RoutePoints.Select(p => new RunSession.RoutePoint { Latitude = p.Latitude, Longitude = p.Longitude }).ToList();
                        }
                        return session;
                    }).OrderByDescending(r => r.StartTime).ToList();
                    return list;
                }
            }

            if (!File.Exists(_dataPath))
                return new List<RunSession>();

            var json = await File.ReadAllTextAsync(_dataPath);
            var runsLocal = JsonSerializer.Deserialize<List<RunSession>>(json) ?? new List<RunSession>();

            // If signed in, filter local runs to only those owned by the current user
            try
            {
                if (_auth != null && !string.IsNullOrEmpty(_auth.UserId))
                {
                    runsLocal = runsLocal.Where(r => r.UserId == _auth.UserId).ToList();
                }
            }
            catch { }

            return runsLocal.OrderByDescending(r => r.StartTime).ToList();
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
            // Try to update remotely
            try
            {
                if (_db != null && _auth != null && !string.IsNullOrEmpty(_auth.UserId))
                {
                    var (ok, err) = await _db.UpdateRunFromSessionAsync(run);
                    if (!ok)
                    {
                        Console.WriteLine($"Supabase update run failed: {err}");
                        try
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                var page = Application.Current?.MainPage;
                                if (page != null)
                                    await page.DisplayAlert("Sync error", "Failed to update run on server: " + (err ?? "Unknown error"), "OK");
                            });
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing run update to Supabase: {ex.Message}");
            }
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
            // Try to delete remotely
            try
            {
                if (_db != null && _auth != null && !string.IsNullOrEmpty(_auth.UserId) && Guid.TryParse(id, out var gid))
                {
                    var (ok, err) = await _db.DeleteRunAsync(gid);
                    if (!ok)
                    {
                        Console.WriteLine($"Supabase delete run failed: {err}");
                        try
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                var page = Application.Current?.MainPage;
                                if (page != null)
                                    await page.DisplayAlert("Sync error", "Failed to delete run on server: " + (err ?? "Unknown error"), "OK");
                            });
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing run delete to Supabase: {ex.Message}");
            }
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

    /// <summary>
    /// Remove local runs that are owned by a different user than the supplied userId.
    /// Keeps runs that have null/empty UserId (unsynced local runs).
    /// </summary>
    public async Task RemoveLocalRunsNotOwnedAsync(string userId)
    {
        try
        {
            if (!File.Exists(_dataPath))
                return;

            var json = await File.ReadAllTextAsync(_dataPath);
            var runsLocal = JsonSerializer.Deserialize<List<RunSession>>(json) ?? new List<RunSession>();

            var filtered = runsLocal.Where(r => string.IsNullOrEmpty(r.UserId) || r.UserId == userId).ToList();

            var outJson = JsonSerializer.Serialize(filtered, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_dataPath, outJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing local runs not owned: {ex.Message}");
        }
    }
}
