namespace RunWise.Models
{
    public class FoodItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Calories { get; set; }
        public string Measurement { get; set; } = string.Empty;
        public string CaloriesDisplay => Calories > 0 ? $"{Calories} kcal{(string.IsNullOrEmpty(Measurement) ? "" : $" • {Measurement}")}" : (string.IsNullOrEmpty(Measurement) ? string.Empty : Measurement);
        public string Guidance { get; set; } = string.Empty;
    }
}
