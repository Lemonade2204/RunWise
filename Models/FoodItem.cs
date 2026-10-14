namespace RunWise.Models
{
    public class FoodItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Calories { get; set; }
        // Optional URL to an image for the food
        public string ImageUrl { get; set; } = string.Empty;
        // Short nutrients summary if available (e.g. "Protein: 10g • Fat: 5g")
        public string NutrientsSummary { get; set; } = string.Empty;
        public string Measurement { get; set; } = string.Empty;
        public string Guidance { get; set; } = string.Empty;
        public string CaloriesDisplay => Calories > 0 ? $"{Calories} kcal" : "N/A";
    }
}