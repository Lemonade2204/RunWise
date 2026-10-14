using System.Globalization;
using RunWise.ViewModels;
using Microsoft.Maui.ApplicationModel;
using System.Threading.Tasks;

namespace RunWise.Pages;

public partial class MapPage : ContentPage
{
    private readonly MapViewModel _viewModel;

    public MapPage()
    {
        InitializeComponent();
        _viewModel = new MapViewModel();

        // Wire up ViewModel events to UI actions
        _viewModel.LocationFirstFixed += (lat, lon) => LoadMap(lat, lon);
        _viewModel.LocationUpdated += async (lat, lon) =>
        {
            var latStr = lat.ToString(CultureInfo.InvariantCulture);
            var lonStr = lon.ToString(CultureInfo.InvariantCulture);
            await MapWebView.EvaluateJavaScriptAsync(
                $"marker.setLatLng([{latStr}, {lonStr}]); map.setView([{latStr}, {lonStr}]);");
        };

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RequestPermissionAndStartAsync();
    }

    private async Task RequestPermissionAndStartAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                // Show rationale if needed
                if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
                {
                    var allow = await DisplayAlert("Location required", "This app needs access to your location to show your position on the map.", "Allow", "Cancel");
                    if (!allow)
                        return;
                }

                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status == PermissionStatus.Granted)
            {
                await _viewModel.StartLocationUpdates();
            }
            else
            {
                var open = await DisplayAlert("Permission denied", "Location permission was denied. Open app settings to enable it?", "Open Settings", "Cancel");
                if (open)
                {
                    AppInfo.ShowSettingsUI();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Permission request error: {ex.Message}");
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _viewModel.StopLocationUpdates();
    }

    void LoadMap(double lat, double lon)
    {
        var latStr = lat.ToString(CultureInfo.InvariantCulture);
        var lonStr = lon.ToString(CultureInfo.InvariantCulture);


        //Map leaflet html used webview
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        html, body, #map {{ height: 100%; margin: 0; }}
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        var lat = {latStr};
        var lon = {lonStr};
        map = L.map('map').setView([lat, lon], 16);
        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
            attribution: '© OpenStreetMap'
        }}).addTo(map);
        marker = L.marker([lat, lon]).addTo(map)
            .bindPopup('You are here!')
            .openPopup();
    </script>
</body>
</html>";

        MapWebView.Source = new HtmlWebViewSource { Html = html };
    }
}