using Microsoft.Maui.Controls;
using RunWise.Models;
using RunWise.ViewModels;

namespace RunWise.Pages;

public partial class MealPlannerPage : ContentPage
{
    public MealPlannerPage()
    {
        InitializeComponent();
    }

    private MealPlannerViewModel? Vm => BindingContext as MealPlannerViewModel;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // pick up items added from the Food page
        if (Vm != null) await Vm.RefreshAsync();
    }

    private void OnDateTapped(object? sender, TappedEventArgs e)
    {
        try { MainThread.BeginInvokeOnMainThread(() => DatePicker.Focus()); } catch { }
    }

    private void OnDayTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is WeekDayItem day && Vm != null) Vm.SelectedDate = day.Date;
    }

    private async void OnGoalTapped(object? sender, TappedEventArgs e)
    {
        if (Vm == null) return;
        var input = await DisplayPromptAsync("Daily calorie goal", "Calories per day:",
            initialValue: Vm.CalorieGoal.ToString(), keyboard: Keyboard.Numeric, maxLength: 5);
        if (string.IsNullOrWhiteSpace(input)) return;
        if (!int.TryParse(input, out var goal) || goal <= 0)
        {
            await DisplayAlertAsync("Invalid", "Goal must be a positive number", "OK");
            return;
        }
        Vm.CalorieGoal = goal;
    }

    private async void OnAddItemClicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is MealSection section)
            await OpenLogFoodAsync(section.Type);
    }

    private async void OnLogFoodClicked(object? sender, EventArgs e) => await OpenLogFoodAsync(LogFoodPage.MealForNow());

    private async Task OpenLogFoodAsync(MealType meal)
    {
        try
        {
            var date = Vm?.SelectedDate ?? DateTime.Today;
            await Navigation.PushModalAsync(new LogFoodPage(date, meal, () => Vm?.RefreshAsync() ?? Task.CompletedTask));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't open Log food", ex.Message, "OK");
        }
    }

    private async void OnDeleteTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not MealRow row || Vm == null) return;
        var ok = await DisplayAlertAsync("Remove item", $"Remove {row.Name}?", "Remove", "Cancel");
        if (!ok) return;
        var error = await Vm.DeleteMealAsync(row.Item);
        if (error != null) await DisplayAlertAsync("Couldn't remove item", error, "OK");
    }

    private async void OnMealTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not MealRow row || Vm == null) return;
        var meal = row.Item;
        var newName = await DisplayPromptAsync("Edit", "Name:", initialValue: meal.Name);
        if (string.IsNullOrWhiteSpace(newName)) return;
        var calStr = await DisplayPromptAsync("Edit", "Calories:", initialValue: meal.Calories.ToString(), keyboard: Keyboard.Numeric);
        if (!int.TryParse(calStr, out var cal) || cal < 0)
        {
            await DisplayAlertAsync("Invalid", "Calories must be a non-negative number", "OK");
            return;
        }
        meal.Name = newName.Trim();
        meal.Calories = cal;
        var error = await Vm.UpdateMealAsync(meal);
        if (error != null) await DisplayAlertAsync("Couldn't save changes", error, "OK");
    }
}
