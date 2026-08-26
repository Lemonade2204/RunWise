using System;
using System.Linq;
using System.IO;
using Microsoft.Maui.Controls;
using RunWise.Services;
using RunWise.Models;

namespace RunWise.Pages;

public partial class ProfilePage : ContentPage
{
	private readonly SupabaseAuthService? _auth;
	private Profile? _loadedProfile;

	public ProfilePage()
	{
		InitializeComponent();
		_auth = MauiProgram.Services.GetService(typeof(SupabaseAuthService)) as SupabaseAuthService;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		if (_auth == null || string.IsNullOrEmpty(_auth.UserId))
			return;

		try
		{
			var (ok, err, profile) = await _auth.GetProfileAsync();
			if (ok && profile != null)
			{
				EmailLabel.Text = profile.Email ?? "";
				NameEntry.Text = profile.Username ?? "";
				WeightEntry.Text = profile.WeightKg?.ToString() ?? "";
				HeightEntry.Text = profile.HeightCm?.ToString() ?? "";

				// remember loaded profile for cancel
				_loadedProfile = profile;

				// ensure editing UI state: disabled until Edit pressed
				NameEntry.IsEnabled = false;
				WeightEntry.IsEnabled = false;
				HeightEntry.IsEnabled = false;
				SaveButton.IsVisible = false;
				CancelButton.IsVisible = false;
				EditButton.IsVisible = true;

				// Compute weekly and all-time aggregates from runs (like History)
				try
				{
					var runService = new RunTrackerService();
					var allRuns = await runService.GetAllRunsAsync();

					// Only runs for current user should be returned by service, but ensure UTC comparison
					var weekAgo = DateTime.UtcNow.AddDays(-7);
					var runsThisWeek = allRuns.Where(r => r.StartTime.ToUniversalTime() >= weekAgo).ToList();

					if (runsThisWeek.Count > 0)
					{
						var avgDist = runsThisWeek.Average(r => r.DistanceMeters) / 1000.0; // meters -> km
						var avgTimeSec = runsThisWeek.Average(r => r.Duration.TotalSeconds);
						WeeklyDistanceLabel.Text = $"Distance: {avgDist:0.00} km";
						WeeklyTimeLabel.Text = $"Time: {SecondsToHMS((int)Math.Round(avgTimeSec))}";
					}
					else
					{
						WeeklyDistanceLabel.Text = "Distance: -";
						WeeklyTimeLabel.Text = "Time: -";
					}

					if (allRuns.Count > 0)
					{
						var totalDist = allRuns.Sum(r => r.DistanceMeters) / 1000.0;
						var totalTime = (int)allRuns.Sum(r => r.Duration.TotalSeconds);
						AllTimeDistanceLabel.Text = $"Distance: {totalDist:0.00} km";
						AllTimeTimeLabel.Text = $"Time: {SecondsToHMS(totalTime)}";
					}
					else
					{
						AllTimeDistanceLabel.Text = "Distance: -";
						AllTimeTimeLabel.Text = "Time: -";
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Failed to compute aggregates from runs: {ex}");
					WeeklyDistanceLabel.Text = "Distance: -";
					WeeklyTimeLabel.Text = "Time: -";
					AllTimeDistanceLabel.Text = "Distance: -";
					AllTimeTimeLabel.Text = "Time: -";
				}
			}
			else if (!ok)
			{
				await DisplayAlert("Error", "Failed to load profile: " + err, "OK");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Profile load failed: {ex}");
		}
	}

	private static string SecondsToHMS(int? seconds)
	{
		if (seconds == null || seconds == 0)
			return "-";
		var ts = TimeSpan.FromSeconds(seconds.Value);
		return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
	}

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		if (_auth == null || string.IsNullOrEmpty(_auth.UserId))
		{
			await DisplayAlert("Error", "Not signed in", "OK");
			return;
		}

		try
		{
			var profile = new Profile
			{
				Id = _auth.UserId,
				Email = EmailLabel.Text,
				Username = NameEntry.Text,
			};

			if (double.TryParse(WeightEntry.Text, out var w))
				profile.WeightKg = w;

			if (double.TryParse(HeightEntry.Text, out var h))
				profile.HeightCm = h;

			var (ok, err) = await _auth.UpdateProfileAsync(profile);
			if (!ok)
			{
				await DisplayAlert("Error", "Failed to save profile: " + err, "OK");
			}
			else
			{
				await DisplayAlert("Saved", "Profile saved successfully.", "OK");

				// update loaded profile and disable editing
				_loadedProfile = profile;
				NameEntry.IsEnabled = false;
				WeightEntry.IsEnabled = false;
				HeightEntry.IsEnabled = false;
				SaveButton.IsVisible = false;
				CancelButton.IsVisible = false;
				EditButton.IsVisible = true;
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", "Save failed: " + ex.Message, "OK");
		}
	}

	private void OnEditClicked(object? sender, EventArgs e)
	{
		// enable fields for editing
		NameEntry.IsEnabled = true;
		WeightEntry.IsEnabled = true;
		HeightEntry.IsEnabled = true;
		SaveButton.IsVisible = true;
		CancelButton.IsVisible = true;
		EditButton.IsVisible = false;
	}

	private void OnCancelClicked(object? sender, EventArgs e)
	{
		// revert to loaded profile values and disable editing
		if (_loadedProfile != null)
		{
			NameEntry.Text = _loadedProfile.Username ?? "";
			WeightEntry.Text = _loadedProfile.WeightKg?.ToString() ?? "";
			HeightEntry.Text = _loadedProfile.HeightCm?.ToString() ?? "";
		}

		NameEntry.IsEnabled = false;
		WeightEntry.IsEnabled = false;
		HeightEntry.IsEnabled = false;
		SaveButton.IsVisible = false;
		CancelButton.IsVisible = false;
		EditButton.IsVisible = true;
	}
	

	private async void OnLogoutClicked(object? sender, EventArgs e)
	{
		try
		{
			if (_auth != null)
				await _auth.SignOutAsync();

			// Clear local runs for the signed out session (optional)
			try
			{
				var dataPath = Path.Combine(FileSystem.AppDataDirectory, "run_history.json");
				if (File.Exists(dataPath))
					File.Delete(dataPath);
			}
			catch { }

			// Return to login screen
			Application.Current.MainPage = new NavigationPage(new Pages.LoginPage());
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", "Logout failed: " + ex.Message, "OK");
		}
	}
}
