using RunWise.Pages;
using Microsoft.Maui.Controls;

namespace RunWise
{
    public partial class AppShell : Shell
    {
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
    }
}
