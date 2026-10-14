using System;

namespace RunWise.Models
{
    public enum MealType
    {
        Breakfast,
        Lunch,
        Dinner,
        Snack
    }

    public class MealItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int Calories { get; set; }
        public MealType Type { get; set; } = MealType.Breakfast;
        public double Quantity { get; set; } = 1.0; // servings
        public DateTime Date { get; set; } = DateTime.Today;
        public string? Notes { get; set; }

        public override string ToString()
        {
            var q = Quantity > 0 ? $" x{Quantity}" : string.Empty;
            return string.IsNullOrWhiteSpace(Name) ? "(item)" : Name + q + (Calories > 0 ? $" ({Calories} kcal)" : string.Empty);
        }
    }
}
