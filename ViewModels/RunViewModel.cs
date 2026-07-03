using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RunWise.Models;
using RunWise.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using static RunWise.Models.RunSession;

namespace RunWise.ViewModels;

public partial class RunViewModel : ObservableObject
{
	private readonly RunTrackerService _trackerService;
	private readonly Stopwatch _stopwatch = new();
	private readonly System.Timers.Timer _uiTimer;
	private Location? _lastLocation;
	private bool _isListening = false;

	[ObservableProperty]
	private TimeSpan elapsedTime = TimeSpan.Zero;

	[ObservableProperty]
	private double distanceKm = 0;

	[ObservableProperty]
	private string currentPaceKmh = "0.00 km/h";

	[ObservableProperty]
	private bool isTracking = false;

	[ObservableProperty]
	private bool isPaused = false;

	[ObservableProperty]
	private ObservableCollection<RunSession> runHistory = new();

	public event Func<double, double, Task>? OnLocationUpdated;
	public event Action<double, double>? OnFirstLocationFixed;
	public event Func<Task>? OnTrackingStopped;

	private RunSession? _currentRun;

	public RunViewModel()
	{
		_trackerService = new RunTrackerService();
		_uiTimer = new System.Timers.Timer(1000);
		_uiTimer.Elapsed += (s, e) =>
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				ElapsedTime = _stopwatch.Elapsed;
				if (_currentRun != null && _stopwatch.IsRunning)
				{
					_currentRun.Duration = _stopwatch.Elapsed;
					UpdatePace();
				}
			});
		};
		LoadHistory();
	}

	private async void LoadHistory()
	{
		var runs = await _trackerService.GetAllRunsAsync();
		RunHistory.Clear();
		foreach (var run in runs)
		{
			RunHistory.Add(run);
		}
	}

	private void UpdatePace()
	{
		if (_currentRun != null && _stopwatch.Elapsed.TotalSeconds > 0)
		{
			var pace = _currentRun.DistanceKm / _stopwatch.Elapsed.TotalHours;
			CurrentPaceKmh = $"{Math.Round(pace, 2):F2} km/h";
		}
	}

	[RelayCommand]
	public async Task StartTracking()
	{
		if (IsTracking) return;

		_currentRun = new RunSession();
		_lastLocation = null;
		ElapsedTime = TimeSpan.Zero;
		DistanceKm = 0;
		CurrentPaceKmh = "0.00 km/h";
		IsTracking = true;
		IsPaused = false;

		_stopwatch.Restart();
		_uiTimer.Start();
		await StartLocationUpdates();
	}

	[RelayCommand]
	public async Task PauseTracking()
	{
		if (!IsTracking) return;

		if (IsPaused)
		{
			// Resume
			IsPaused = false;
			_stopwatch.Start();
			_uiTimer.Start();
			await StartLocationUpdates();
		}
		else
		{
			// Pause
			IsPaused = true;
			_stopwatch.Stop();
			_uiTimer.Stop();
			await StopLocationUpdates();
		}
	}

	[RelayCommand]
	public async Task StopTracking()
	{
		if (!IsTracking) return;

		_stopwatch.Stop();
		_uiTimer.Stop();
		await StopLocationUpdates();

		if (_currentRun != null)
		{
			_currentRun.Duration = _stopwatch.Elapsed;
			_currentRun.EndTime = DateTime.UtcNow;
			await _trackerService.SaveRunAsync(_currentRun);

			RunHistory.Insert(0, _currentRun);
			_currentRun = null;
		}

		IsTracking = false;
		IsPaused = false;
		ElapsedTime = TimeSpan.Zero;
		DistanceKm = 0;
		CurrentPaceKmh = "0.00 km/h";

		// Notify that tracking has stopped
		if (OnTrackingStopped != null)
			await OnTrackingStopped.Invoke();
	}

	private async Task StartLocationUpdates()
	{
		if (_isListening) return;

		try
		{
			var last = await Geolocation.Default.GetLastKnownLocationAsync();
			if (last != null)
			{
				OnFirstLocationFixed?.Invoke(last.Latitude, last.Longitude);
				if (_currentRun != null)
				{
					//_currentRun.RoutePoints.Add((last.Latitude, last.Longitude));
                    _currentRun.RoutePoints.Add(new RoutePoint { Latitude = last.Latitude, Longitude = last.Longitude });
                }
			}

			var listenRequest = new GeolocationListeningRequest(GeolocationAccuracy.Best);
			Geolocation.Default.LocationChanged += OnLocationChanged;
			_isListening = await Geolocation.Default.StartListeningForegroundAsync(listenRequest);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Location error: {ex.Message}");
		}
	}

	private async Task StopLocationUpdates()
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
			if (_currentRun == null) return;

			var lat = e.Location.Latitude;
			var lon = e.Location.Longitude;

			//_currentRun.RoutePoints.Add((lat, lon));
            _currentRun.RoutePoints.Add(new RoutePoint { Latitude = lat, Longitude = lon });

            if (_lastLocation != null)
			{
				double distance = DistanceBetween(
					_lastLocation.Latitude, _lastLocation.Longitude,
					lat, lon);

				DistanceKm += distance / 1000.0;
				_currentRun.DistanceMeters += distance;
				UpdatePace();
			}

			_lastLocation = new Location(lat, lon);

			if (OnLocationUpdated != null)
				await OnLocationUpdated.Invoke(lat, lon);
		});
	}

	private double DistanceBetween(double lat1, double lon1, double lat2, double lon2)
	{
		double R = 6371000;
		double dLat = (lat2 - lat1) * Math.PI / 180.0;
		double dLon = (lon2 - lon1) * Math.PI / 180.0;
		double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
				   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
				   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
		double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
		return R * c;
	}



}
