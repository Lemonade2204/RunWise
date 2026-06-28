using System.Globalization;
using RunWise.ViewModels;

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
        await _viewModel.StartLocationUpdates();
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

        var html = $@"<!DOCTYPE html>
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