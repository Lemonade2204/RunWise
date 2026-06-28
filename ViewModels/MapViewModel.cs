using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

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

        // Show last known location instantly
        var last = await Geolocation.Default.GetLastKnownLocationAsync();
        if (last != null)
        {
            LocationFirstFixed?.Invoke(last.Latitude, last.Longitude);
            _mapLoaded = true;
        }

        try
        {
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