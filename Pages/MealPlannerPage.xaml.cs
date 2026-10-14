using Microsoft.Maui.Controls;
using RunWise.ViewModels;
using RunWise.Models;

namespace RunWise.Pages;

public partial class MealPlannerPage : ContentPage
{
    public MealPlannerPage()
    {
        InitializeComponent();
        // Attach click handlers for Add buttons (avoid XAML event wiring to prevent hot-reload issues)
        try
        {
            var b1 = this.FindByName<Button>("BreakfastAddButton");
            if (b1 != null) b1.Clicked += OnSearchAddBreakfastClicked;
            var b2 = this.FindByName<Button>("LunchAddButton");
            if (b2 != null) b2.Clicked += OnSearchAddLunchClicked;
            var b3 = this.FindByName<Button>("DinnerAddButton");
            if (b3 != null) b3.Clicked += OnSearchAddDinnerClicked;
            // make the date area open the native DatePicker when tapped
            var dateArea = this.FindByName<StackLayout>("DateTapArea");
            if (dateArea != null)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) =>
                {
                    try
                    {
                        var dp = this.FindByName<DatePicker>("DatePicker");
                        Device.BeginInvokeOnMainThread(() => dp?.Focus());
                    }
                    catch { }
                };
                dateArea.GestureRecognizers.Add(tap);
            }
        }
        catch { }
    }

    public void OnDateTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var dp = this.FindByName<DatePicker>("DatePicker");
            if (dp != null)
            {
                Device.BeginInvokeOnMainThread(() => dp.Focus());
            }
        }
        catch { }
    }

    // Overload with EventArgs in case XAML resolves a non-generic signature
    public void OnDateTapped(object sender, EventArgs e)
    {
        try
        {
            var dp = this.FindByName<DatePicker>("DatePicker");
            if (dp != null)
            {
                Device.BeginInvokeOnMainThread(() => dp.Focus());
            }
        }
        catch { }
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        // ensure DatePicker selection is synced when binding context available
        if (BindingContext is RunWise.ViewModels.MealPlannerViewModel vm)
        {
            try
            {
                var dp = this.FindByName<DatePicker>("DatePicker");
                if (dp != null) dp.Date = vm.SelectedDate;
            }
            catch { }
        }
    }

    private async void OnSearchAddBreakfastClicked(object sender, EventArgs e)
    {
        try
        {
            var entry = this.FindByName<Entry>("BreakfastEntry");
            var query = (entry?.Text ?? string.Empty).Trim();
            // If there's text in the quick-add entry, add directly to VM/store; otherwise navigate to Food search
            if (!string.IsNullOrWhiteSpace(query))
            {
                if (BindingContext is RunWise.ViewModels.MealPlannerViewModel vm)
                {
                    // ensure NewBreakfastText is set and execute add
                    vm.NewBreakfastText = query;
                    await vm.AddBreakfastAsync();
                    // clear entry after add
                    entry.Text = string.Empty;
                }
                return;
            }

            await Shell.Current.GoToAsync($"//food?search={Uri.EscapeDataString(query)}&meal=Breakfast");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Navigation error", ex.Message, "OK");
        }
    }

    private async void OnSearchAddLunchClicked(object sender, EventArgs e)
    {
        try
        {
            var entry = this.FindByName<Entry>("LunchEntry");
            var query = (entry?.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(query))
            {
                if (BindingContext is RunWise.ViewModels.MealPlannerViewModel vm)
                {
                    vm.NewLunchText = query;
                    await vm.AddLunchAsync();
                    entry.Text = string.Empty;
                }
                return;
            }

            await Shell.Current.GoToAsync($"//food?search={Uri.EscapeDataString(query)}&meal=Lunch");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Navigation error", ex.Message, "OK");
        }
    }

    private async void OnSearchAddDinnerClicked(object sender, EventArgs e)
    {
        try
        {
            var entry = this.FindByName<Entry>("DinnerEntry");
            var query = (entry?.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(query))
            {
                if (BindingContext is RunWise.ViewModels.MealPlannerViewModel vm)
                {
                    vm.NewDinnerText = query;
                    await vm.AddDinnerAsync();
                    entry.Text = string.Empty;
                }
                return;
            }

            await Shell.Current.GoToAsync($"//food?search={Uri.EscapeDataString(query)}&meal=Dinner");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Navigation error", ex.Message, "OK");
        }
    }

    private async void OnDeleteMealInvoked(object sender, EventArgs e)
    {
        try
        {
            if (sender is SwipeItem si && si.BindingContext is MealItem meal)
            {
                var ok = await DisplayAlert("Delete", $"Delete {meal.Name}?", "Delete", "Cancel");
                if (!ok) return;
                if (BindingContext is MealPlannerViewModel vm)
                {
                    await vm.DeleteMealAsync(meal);
                }
            }
        }
        catch { }
    }

    private async void OnEditMealInvoked(object sender, EventArgs e)
    {
        try
        {
            if (sender is SwipeItem si && si.BindingContext is MealItem meal)
            {
                var newName = await DisplayPromptAsync("Edit", "Name:", initialValue: meal.Name);
                if (string.IsNullOrWhiteSpace(newName)) return;
                var calStr = await DisplayPromptAsync("Edit", "Calories:", initialValue: meal.Calories.ToString(), keyboard: Keyboard.Numeric);
                if (!int.TryParse(calStr, out var cal) || cal < 0)
                {
                    await DisplayAlert("Invalid", "Calories must be a non-negative number", "OK");
                    return;
                }
                meal.Name = newName.Trim();
                meal.Calories = cal;
                if (BindingContext is MealPlannerViewModel vm)
                {
                    await vm.UpdateMealAsync(meal);
                }
            }
        }
        catch { }
    }
}
