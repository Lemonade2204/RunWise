using RunWise.Models;
using RunWise.ViewModels;

namespace RunWise.Pages;

// Bottom-sheet style modal for logging food to a meal on a given date.
public partial class LogFoodPage : ContentPage
{
    private readonly LogFoodViewModel _vm;

    public LogFoodPage(DateTime date, MealType meal, Func<Task>? onLogged = null)
    {
        InitializeComponent();
        BindingContext = _vm = new LogFoodViewModel(date, meal, onLogged);
    }

    // Picks a sensible default meal for the current time of day
    public static MealType MealForNow()
    {
        var h = DateTime.Now.TimeOfDay;
        if (h < TimeSpan.FromHours(10.5)) return MealType.Breakfast;
        if (h < TimeSpan.FromHours(15)) return MealType.Lunch;
        if (h < TimeSpan.FromHours(21)) return MealType.Dinner;
        return MealType.Snack;
    }

    private async void OnCloseTapped(object? sender, TappedEventArgs e) => await CloseAsync();

    private async Task CloseAsync()
    {
        try { await Navigation.PopModalAsync(); } catch { }
    }

    private void OnMealChoiceTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is MealChoice choice) _vm.SelectedMeal = choice.Type;
    }

    private async void OnAddFoodClicked(object? sender, EventArgs e)
    {
        if (sender is not Button b || b.BindingContext is not FoodOption food) return;
        b.IsEnabled = false;
        try { await _vm.AddFoodAsync(food); }
        catch (Exception ex) { await DisplayAlertAsync("Couldn't add food", ex.Message, "OK"); }
        finally { b.IsEnabled = true; }
    }

    private async void OnAddOwnClicked(object? sender, EventArgs e)
    {
        if (sender is Button b) b.IsEnabled = false;
        try
        {
            var error = await _vm.AddOwnFoodAsync();
            if (error != null) await DisplayAlertAsync("Missing info", error, "OK");
        }
        catch (Exception ex) { await DisplayAlertAsync("Couldn't add food", ex.Message, "OK"); }
        finally { if (sender is Button b2) b2.IsEnabled = true; }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }
}
