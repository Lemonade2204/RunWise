using System.Diagnostics;
using System.Globalization;
using Microsoft.Maui.Devices.Sensors;

namespace RunWise.Pages;

public partial class RunPage : ContentPage
{
	private bool _isListening = false;
	private bool _mapLoaded = false;
	private readonly Stopwatch _stopwatch = new Stopwatch();
	private readonly System.Timers.Timer _uiTimer;
	private double _distanceMeters = 0;
	private Location? _lastLocation = null;

	public RunPage()
	{
		InitializeComponent();
		_uiTimer = new System.Timers.Timer(1000);
		_uiTimer.Elapsed += (s, e) => MainThread.BeginInvokeOnMainThread(() => TimeLabel.Text = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss"));
	}

	protected override async void OnDisappearing()
	{
		base.OnDisappearing();
		await StopLocationUpdates();
	}

	private async void StartButton_Clicked(object sender, EventArgs e)
	{
		StartButton.IsEnabled = false;
		PauseButton.IsEnabled = true;
		StopButton.IsEnabled = true;
		_distanceMeters = 0;
		_lastLocation = null;
		DistanceLabel.Text = "Distance: 0.00 km";
		_stopwatch.Restart();
		_uiTimer.Start();
		await StartLocationUpdates();
	}

	private async void PauseButton_Clicked(object sender, EventArgs e)
	{
		if (_stopwatch.IsRunning)
		{
			_stopwatch.Stop();
			PauseButton.Text = "Resume";
			await StopLocationUpdates();
		}
		else
		{
			_stopwatch.Start();
			PauseButton.Text = "Pause";
			await StartLocationUpdates();
		}
	}

	private async void StopButton_Clicked(object sender, EventArgs e)
	{
		_stopwatch.Stop();
		_uiTimer.Stop();
		StartButton.IsEnabled = true;
		PauseButton.IsEnabled = false;
		StopButton.IsEnabled = false;
		PauseButton.Text = "Pause";
		await StopLocationUpdates();
	}

	async Task StartLocationUpdates()
	{
		if (_isListening) return;

		try
		{
			var listenRequest = new GeolocationListeningRequest(GeolocationAccuracy.Best);
			Geolocation.Default.LocationChanged += OnLocationChanged;
			_isListening = await Geolocation.Default.StartListeningForegroundAsync(listenRequest);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Listening error: {ex.Message}");
			var last = await Geolocation.Default.GetLastKnownLocationAsync();
			if (last != null)
			{
				await LoadMap(last.Latitude, last.Longitude);
				_mapLoaded = true;
			}
		}
	}

	async Task StopLocationUpdates()
	{
		if (!_isListening) return;

		Geolocation.Default.LocationChanged -= OnLocationChanged;
		Geolocation.Default.StopListeningForeground();
		_isListening = false;
	}

	void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			var lat = e.Location.Latitude;
			var lon = e.Location.Longitude;

			if (!_mapLoaded)
			{
				await LoadMap(lat, lon);
				_mapLoaded = true;
			}
			else
			{
				var latStr = lat.ToString(CultureInfo.InvariantCulture);
				var lonStr = lon.ToString(CultureInfo.InvariantCulture);
				await MapWebView.EvaluateJavaScriptAsync($"addPoint({latStr},{lonStr});");
			}

			if (_lastLocation != null)
			{
				_distanceMeters += DistanceBetween(_lastLocation.Latitude, _lastLocation.Longitude, lat, lon);
				DistanceLabel.Text = $"Distance: {_distanceMeters / 1000:0.00} km";
			}

			_lastLocation = new Location(lat, lon);
		});
	}

	double DistanceBetween(double lat1, double lon1, double lat2, double lon2)
	{
		double R = 6371000; // meters
		double dLat = (lat2 - lat1) * Math.PI / 180.0;
		double dLon = (lon2 - lon1) * Math.PI / 180.0;
		double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
		double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
		return R * c;
	}

	async Task LoadMap(double lat, double lon)
	{
		var latStr = lat.ToString(CultureInfo.InvariantCulture);
		var lonStr = lon.ToString(CultureInfo.InvariantCulture);

		var html = $@"<!DOCTYPE html>
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
		map = L.map('map').setView([lat, lon], 16);
		L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
			attribution: '© OpenStreetMap'
		}}).addTo(map);
		var polyline = L.polyline([[lat, lon]], {{color:'red'}}).addTo(map);
		var marker = L.marker([lat, lon]).addTo(map);

		function addPoint(lat, lon) {{
			polyline.addLatLng([lat, lon]);
			marker.setLatLng([lat, lon]);
			try {{
				if (polyline.getLatLngs().length > 1) map.fitBounds(polyline.getBounds());
				else map.setView([lat, lon]);
			}} catch(e) {{ console.log(e); }}
		}}
	</script>
</body>
</html>";

		MapWebView.Source = new HtmlWebViewSource { Html = html };
	}
}
