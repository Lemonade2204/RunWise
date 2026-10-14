using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui;

namespace RunWise.ViewModels;

public partial class MapViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isListening = false;

    [ObservableProperty]
    private bool _mapLoaded = false;

    // Events to tell the Page what to do
    public event Action<double, double>? LocationFirstFixed;
    public event Func<double, double, Task>? LocationUpdated;

    public async Task StartLocationUpdates()
    {
        if (_isListening) return;
        // Ensure location permission is granted
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
            {
                // Notify user and abort starting location updates
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var page = Application.Current?.MainPage;
                    if (page != null)
                        await page.DisplayAlert("Location Permission", "Location permission is required to show the map. Please enable it in app settings.", "OK");
                });
                return;
            }

            // Show last known location instantly
            var last = await Geolocation.Default.GetLastKnownLocationAsync();
            if (last != null)
            {
                LocationFirstFixed?.Invoke(last.Latitude, last.Longitude);
                _mapLoaded = true;
            }

            var listenRequest = new GeolocationListeningRequest(GeolocationAccuracy.Medium);
            Geolocation.Default.LocationChanged += OnLocationChanged;
            _isListening = await Geolocation.Default.StartListeningForegroundAsync(listenRequest);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Listening error: {ex.Message}");
        }
    }

    public async Task StopLocationUpdates()
    {
        if (!_isListening) return;

        Geolocation.Default.LocationChanged -= OnLocationChanged;
        Geolocation.Default.StopListeningForeground();
        _isListening = false;
    }

    private void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!_mapLoaded)
            {
                LocationFirstFixed?.Invoke(e.Location.Latitude, e.Location.Longitude);
                _mapLoaded = true;
            }
            else
            {
                if (LocationUpdated != null)
                    await LocationUpdated.Invoke(e.Location.Latitude, e.Location.Longitude);
            }
        });
    }
}