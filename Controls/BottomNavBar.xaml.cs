namespace RunWise.Controls;

public partial class BottomNavBar : Grid
{
    private static readonly Color ActiveColor = Color.FromArgb("#4A6CF7");
    private static readonly Color InactiveColor = Color.FromArgb("#9AA0AE");

    public BottomNavBar()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateSelection(ResolveCurrentRoute());
    }

    // Each page hosts its own bar, so the selected tab is the route of the ShellContent owning that page.
    private string? ResolveCurrentRoute()
    {
        Element? element = Parent;
        while (element != null && element is not Page)
            element = element.Parent;

        if (element?.Parent is ShellContent content)
            return content.Route;

        return Shell.Current?.CurrentItem?.CurrentItem?.CurrentItem?.Route;
    }

    private void UpdateSelection(string? route)
    {
        SetState(HomeIcon, HomeLabel, route == "home");
        SetState(MapIcon, MapLabel, route == "maps");
        SetState(HistoryIcon, HistoryLabel, route == "history");
        SetState(ProfileIcon, ProfileLabel, route == "profile");
    }

    private static void SetState(Microsoft.Maui.Controls.Shapes.Path icon, Label label, bool active)
    {
        var color = active ? ActiveColor : InactiveColor;
        icon.Stroke = new SolidColorBrush(color);
        label.TextColor = color;
        label.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
    }

    private async void OnTabTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string route || Shell.Current is null)
            return;

        if (route == ResolveCurrentRoute())
            return;

        await Shell.Current.GoToAsync($"//{route}");
    }

    private void OnMenuTapped(object? sender, TappedEventArgs e)
    {
        if (Shell.Current != null)
            Shell.Current.FlyoutIsPresented = true;
    }
}
