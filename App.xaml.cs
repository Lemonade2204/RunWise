using Microsoft.Extensions.DependencyInjection;

namespace RunWise
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // Show the LoginPage when the app starts. Wrap in a NavigationPage so we can push the RegisterPage.
            return new Window(new NavigationPage(new Pages.LoginPage()));
        }
    }
}