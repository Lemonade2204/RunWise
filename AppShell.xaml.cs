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
        }
    }
}
