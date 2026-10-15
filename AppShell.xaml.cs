using RunWise.Pages;
using RunWise.Services;
using Microsoft.Maui.Controls;

namespace RunWise
{
    public partial class AppShell : Shell
    {
        private static readonly Color DrawerActiveColor = Color.FromArgb("#4A6CF7");
        private static readonly Color DrawerActiveBackground = Color.FromArgb("#EEF1FE");
        private static readonly Color DrawerIconColor = Color.FromArgb("#5B6275");
        private static readonly Color DrawerTextColor = Color.FromArgb("#2B3040");

        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("editrun", typeof(EditRunPage));
            Routing.RegisterRoute("rundetails", typeof(Pages.RunDetailsPage));
            Routing.RegisterRoute("login", typeof(Pages.LoginPage));
            Routing.RegisterRoute("register", typeof(Pages.RegisterPage));
            Routing.RegisterRoute("runfood", typeof(Pages.RunFoodRecommendationPage));
            Routing.RegisterRoute("mealplanner", typeof(Pages.MealPlannerPage));
        }

        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);
            UpdateDrawerSelection(CurrentItem?.CurrentItem?.CurrentItem?.Route);
        }

        private void UpdateDrawerSelection(string? route)
        {
            SetDrawerState(ExercisesItem, ExercisesIcon, ExercisesLabel, route == "exercises");
            SetDrawerState(FoodItem, FoodIcon, FoodLabel, route == "food");
            SetDrawerState(MealPlannerItem, MealPlannerIcon, MealPlannerLabel, route == "mealplanner");
        }

        private static void SetDrawerState(Border item, Microsoft.Maui.Controls.Shapes.Path icon, Label label, bool active)
        {
            item.BackgroundColor = active ? DrawerActiveBackground : Colors.Transparent;
            icon.Stroke = new SolidColorBrush(active ? DrawerActiveColor : DrawerIconColor);
            label.TextColor = active ? DrawerActiveColor : DrawerTextColor;
            label.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
        }

        private async void OnDrawerItemTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is not string route)
                return;

            FlyoutIsPresented = false;
            await GoToAsync($"//{route}");
        }

        private async void OnLogoutTapped(object? sender, TappedEventArgs e)
        {
            FlyoutIsPresented = false;

            bool confirm = await DisplayAlert("Log Out", "Are you sure you want to log out?", "Log Out", "Cancel");
            if (!confirm)
                return;

            try
            {
                await LogOutAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Logout failed: " + ex.Message, "OK");
            }
        }

        public static async Task LogOutAsync()
        {
            if (MauiProgram.Services.GetService(typeof(SupabaseAuthService)) is SupabaseAuthService auth)
                await auth.SignOutAsync();

            // Clear local runs for the signed out session
            try
            {
                var dataPath = System.IO.Path.Combine(FileSystem.AppDataDirectory, "run_history.json");
                if (File.Exists(dataPath))
                    File.Delete(dataPath);
            }
            catch { }

            // Return to login screen
            Application.Current!.MainPage = new NavigationPage(new Pages.LoginPage());
        }
    }
}
