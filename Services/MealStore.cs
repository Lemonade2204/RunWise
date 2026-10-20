using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using RunWise.Models;

namespace RunWise.Services
{
    // Simple meal store: persists meals per-date using Preferences (fallback). Exposes events for changes.
    public class MealStore
    {
        private const string PrefKeyPrefix = "runwise_meals_";

        public event Action? MealsChanged;

        public MealStore()
        {
        }

        private string KeyForDate(DateTime date) => PrefKeyPrefix + date.ToString("yyyy-MM-dd");

        public async Task<List<MealItem>> GetMealsForDateAsync(DateTime date)
        {
            // Try server-backed meals first (if user signed in)
            try
            {
                var sup = MauiProgram.Services.GetService(typeof(SupabaseDatabaseService)) as SupabaseDatabaseService;
                if (sup != null)
                {
                    var (suc, err, meals) = await sup.GetMealsForUserDateAsync(date.Date);
                    if (suc && meals != null)
                        return meals;
                }
            }
            catch { }

            var key = KeyForDate(date.Date);
            if (!Preferences.ContainsKey(key)) return new List<MealItem>();
            try
            {
                var txt = Preferences.Get(key, string.Empty);
                if (string.IsNullOrWhiteSpace(txt)) return new List<MealItem>();
                var list = JsonSerializer.Deserialize<List<MealItem>>(txt);
                return list ?? new List<MealItem>();
            }
            catch
            {
                return new List<MealItem>();
            }
        }

        private async Task SaveMealsForDateAsync(DateTime date, List<MealItem> list)
        {
            var key = KeyForDate(date.Date);
            try
            {
                var txt = JsonSerializer.Serialize(list);
                Preferences.Set(key, txt);
                MealsChanged?.Invoke();
            }
            catch { }
            await Task.CompletedTask;
        }

        public async Task AddMealAsync(MealItem meal)
        {
            var list = await GetMealsForDateAsync(meal.Date.Date);
            // prevent duplicate by name+type+calories — if exists increment quantity
            var existing = list.FirstOrDefault(m => string.Equals(m.Name?.Trim(), meal.Name?.Trim(), StringComparison.OrdinalIgnoreCase) && m.Type == meal.Type && m.Calories == meal.Calories);
            if (existing != null)
            {
                existing.Quantity += meal.Quantity;
            }
            else
            {
                list.Add(meal);
            }
            await SaveMealsForDateAsync(meal.Date.Date, list);
        }

        // Returns an error message when the server could not be updated, otherwise null.
        public async Task<string?> UpdateMealAsync(MealItem meal)
        {
            // Update the server first: when signed in, meals are read back from Supabase
            var error = await SyncToServerAsync(sup => sup.UpdateMealAsync(meal));
            if (error != null) return error;

            var list = await GetMealsForDateAsync(meal.Date.Date);
            var idx = list.FindIndex(m => m.Id == meal.Id);
            if (idx >= 0)
            {
                list[idx] = meal;
                await SaveMealsForDateAsync(meal.Date.Date, list);
            }
            return null;
        }

        // Returns an error message when the server could not be updated, otherwise null.
        public async Task<string?> DeleteMealAsync(MealItem meal)
        {
            // Delete on the server first, otherwise the meal comes back on the next load
            var error = await SyncToServerAsync(sup => sup.DeleteMealAsync(meal));
            if (error != null) return error;

            var list = await GetMealsForDateAsync(meal.Date.Date);
            var idx = list.FindIndex(m => m.Id == meal.Id);
            if (idx >= 0)
            {
                list.RemoveAt(idx);
                await SaveMealsForDateAsync(meal.Date.Date, list);
            }
            else
            {
                MealsChanged?.Invoke();
            }
            return null;
        }

        private static async Task<string?> SyncToServerAsync(Func<SupabaseDatabaseService, Task<(bool Success, string? Error)>> action)
        {
            try
            {
                var auth = MauiProgram.Services.GetService(typeof(SupabaseAuthService)) as SupabaseAuthService;
                var sup = MauiProgram.Services.GetService(typeof(SupabaseDatabaseService)) as SupabaseDatabaseService;
                // signed out: local storage only
                if (sup == null || string.IsNullOrEmpty(auth?.UserId)) return null;

                var (ok, err) = await action(sup);
                return ok ? null : err ?? "Server sync failed";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<int> GetTotalCaloriesForDateAsync(DateTime date)
        {
            var list = await GetMealsForDateAsync(date.Date);
            return list.Sum(m => m.Calories * (int)Math.Max(1, Math.Round(m.Quantity)));
        }
    }
}
