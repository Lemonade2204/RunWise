using Microsoft.Maui.Platform;
using RunWise.ViewModels;
using System.Globalization;

namespace RunWise.Pages;

public partial class RunPage : ContentPage
{
    private bool _mapLoaded = false;
    private RunViewModel? _viewModel;

    public RunPage()
    {
        InitializeComponent();
        _viewModel = new RunViewModel();
        BindingContext = _viewModel;
        _viewModel.OnFirstLocationFixed += OnFirstLocationFixed;
        _viewModel.OnLocationUpdated += OnLocationUpdated;
        _viewModel.OnTrackingStopped += ClearMapDisplay;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Show the map centered on last known location, but do NOT start tracking/listening yet.
        try
        {
            var last = await Geolocation.Default.GetLastKnownLocationAsync();
            if (last != null && !_mapLoaded)
            {
                LoadMap(last.Latitude, last.Longitude);
                _mapLoaded = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting last known location: {ex.Message}");
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_viewModel?.IsTracking == true)
        {
            await _viewModel.StopTrackingCommand.ExecuteAsync(null);
        }
    }

    private void OnFirstLocationFixed(double latitude, double longitude)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_mapLoaded)
            {
                LoadMap(latitude, longitude);
                _mapLoaded = true;
            }
        });
    }

    private async Task OnLocationUpdated(double latitude, double longitude)
    {
        if (_mapLoaded)
        {
            var latStr = latitude.ToString(CultureInfo.InvariantCulture);
            var lonStr = longitude.ToString(CultureInfo.InvariantCulture);
            try
            {
                await MapWebView.EvaluateJavaScriptAsync($"addPoint({latStr},{lonStr});");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JS Error: {ex.Message}");
            }
        }
    }

    private void LoadMap(double lat, double lon)
    {
        var latStr = lat.ToString(CultureInfo.InvariantCulture);
        var lonStr = lon.ToString(CultureInfo.InvariantCulture);

        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
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
        var startLat = lat;
        var startLon = lon;
        var map = L.map('map').setView([lat, lon], 16);
        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
            attribution: '© OpenStreetMap'
        }}).addTo(map);
        var polyline = L.polyline([[lat, lon]], {{color:'red', weight: 3}}).addTo(map);
        var marker = L.marker([lat, lon]).addTo(map);

        function addPoint(lat, lon) {{
            polyline.addLatLng([lat, lon]);
            marker.setLatLng([lat, lon]);
            try {{
                if (polyline.getLatLngs().length > 1) {{
                    map.fitBounds(polyline.getBounds());
                }} else {{
                    map.setView([lat, lon]);
                }}
            }} catch(e) {{ console.log(e); }}
        }}

        function clearMap() {{
            try {{
                // Remove old polyline and create a fresh one
                map.removeLayer(polyline);
                polyline = L.polyline([], {{color:'red', weight: 3}}).addTo(map);
                
                // Reset marker to start position
                marker.setLatLng([startLat, startLon]);
                map.setView([startLat, startLon], 16);
            }} catch(e) {{ console.log(e); }}
        }}
    </script>
</body>
</html>";
        MapWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private async Task ClearMapDisplay()
    {
        if (_mapLoaded)
        {
            try
            {
                await MapWebView.EvaluateJavaScriptAsync("clearMap();");

                // Reset map state so next start loads fresh
                _mapLoaded = false;

                // Clear the webview entirely
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MapWebView.Source = new HtmlWebViewSource { Html = "<html><body style='background:#f5f5f5;'></body></html>" };
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing map: {ex.Message}");
            }
        }
    }
}
