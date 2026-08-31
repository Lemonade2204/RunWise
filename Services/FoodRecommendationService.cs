using System.Collections.Generic;
using System.Linq;
using RunWise.Models;

namespace RunWise.Services
{
    // Simple, deterministic recommender that picks a meal based on remaining calories.
    // Strategy:
    // 1) Prefer the meal with the highest calories that does not exceed the remainingCalories.
    // 2) If none fit, return the lowest-calorie meal as a fallback.
    public class FoodRecommendationService
    {
        public FoodItem? RecommendByCalories(IEnumerable<FoodItem> candidates, int remainingCalories)
        {
            if (candidates is null) return null;

            var list = candidates.ToList();
            if (!list.Any()) return null;

            var fitting = list.Where(f => f.Calories <= remainingCalories).ToList();
            if (fitting.Any())
            {
                // choose the richest option that still fits the calorie budget
                return fitting.OrderByDescending(f => f.Calories).First();
            }

            // fallback: return the lowest calorie item
            return list.OrderBy(f => f.Calories).First();
        }
    }
}
