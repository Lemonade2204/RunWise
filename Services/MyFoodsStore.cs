using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Maui.Storage;
using RunWise.Models;

namespace RunWise.Services
{
    // User-defined foods ("My foods"), persisted on the device so they show up in Log food search.
    public static class MyFoodsStore
    {
        private const string PrefKey = "runwise_my_foods";

        public static List<FoodItem> GetAll()
        {
            try
            {
                var txt = Preferences.Get(PrefKey, string.Empty);
                if (string.IsNullOrWhiteSpace(txt)) return new List<FoodItem>();
                return JsonSerializer.Deserialize<List<FoodItem>>(txt) ?? new List<FoodItem>();
            }
            catch
            {
                return new List<FoodItem>();
            }
        }

        public static void Save(FoodItem food)
        {
            var list = GetAll();
            // replace an existing entry with the same name
            list.RemoveAll(f => string.Equals(f.Title.Trim(), food.Title.Trim(), StringComparison.OrdinalIgnoreCase));
            list.Insert(0, food);
            try { Preferences.Set(PrefKey, JsonSerializer.Serialize(list.Take(100).ToList())); } catch { }
        }
    }
}
