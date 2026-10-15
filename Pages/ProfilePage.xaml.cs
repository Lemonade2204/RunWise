using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Maui.Controls;
using RunWise.Services;
using RunWise.Models;

namespace RunWise.Pages;

public partial class ProfilePage : ContentPage
{
	private readonly SupabaseAuthService? _auth;
	private Profile? _loadedProfile;

	private List<RunSession> _allRuns = new List<RunSession>();

	public ProfilePage()
	{
		InitializeComponent();
		_auth = MauiProgram.Services.GetService(typeof(SupabaseAuthService)) as SupabaseAuthService;
	}

	private string MapDisplayGenderToStored(string display)
	{
		if (string.IsNullOrWhiteSpace(display)) return string.Empty;
		switch (display.Trim())
		{
			case "Male": return "male";
			case "Female": return "female";
			case "Other": return "other";
			case "Prefer not to say": return "prefer_not_to_say";
			default: return display.Trim().ToLowerInvariant();
		}
	}

	private string MapStoredGenderToDisplay(string stored)
	{
		if (string.IsNullOrWhiteSpace(stored)) return string.Empty;
		switch (stored.Trim().ToLowerInvariant())
		{
			case "male": return "Male";
			case "female": return "Female";
			case "other": return "Other";
			case "prefer_not_to_say": return "Prefer not to say";
			default: return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(stored.Trim());
		}
	}

	private DateTime ToLocal(DateTime dt)
	{
		if (dt.Kind == DateTimeKind.Local)
			return dt;
		if (dt.Kind == DateTimeKind.Utc)
			return dt.ToLocalTime();
		return DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime();
	}

	private static DateTime StartOfWeek(DateTime now)
	{
		var start = now.Date;
		while (start.DayOfWeek != DayOfWeek.Monday) start = start.AddDays(-1);
		return start;
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
				_loadedProfile = profile;
				ShowProfile(profile);
				SetEditing(false);
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

		try
		{
			var runService = new RunTrackerService();
			_allRuns = await runService.GetAllRunsAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Failed to load runs: {ex}");
			_allRuns = new List<RunSession>();
		}

		UpdateSummary();

		// hook up range picker
		RangePicker.SelectedIndexChanged -= RangePicker_SelectedIndexChanged;
		RangePicker.SelectedIndexChanged += RangePicker_SelectedIndexChanged;
		if (RangePicker.SelectedIndex < 0) RangePicker.SelectedIndex = 0; // default All Time
		else RangePicker_SelectedIndexChanged(RangePicker, EventArgs.Empty);
	}

	private void ShowProfile(Profile profile)
	{
		EmailLabel.Text = profile.Email ?? "";
		HeaderEmailLabel.Text = profile.Email ?? "";
		NameEntry.Text = profile.Username ?? "";
		WeightEntry.Text = profile.WeightKg?.ToString() ?? "";
		HeightEntry.Text = profile.HeightCm?.ToString() ?? "";
		DobPicker.Date = profile.DateOfBirth?.Date ?? DateTime.Now.AddYears(-25);
		GenderPicker.SelectedItem = string.IsNullOrEmpty(profile.Gender) ? null : MapStoredGenderToDisplay(profile.Gender);
		UpdateHeader(profile);
	}

	// Header shows name plus a compact "age · weight · height" line when those are known
	private void UpdateHeader(Profile profile)
	{
		HeaderNameLabel.Text = string.IsNullOrWhiteSpace(profile.Username) ? "Your name" : profile.Username;

		var parts = new List<string>();
		if (profile.DateOfBirth.HasValue)
		{
			var dob = profile.DateOfBirth.Value.Date;
			var age = DateTime.Today.Year - dob.Year;
			if (dob > DateTime.Today.AddYears(-age)) age--;
			if (age > 0) parts.Add($"{age} yrs");
		}
		if (profile.WeightKg.HasValue) parts.Add($"{profile.WeightKg:0.#} kg");
		if (profile.HeightCm.HasValue) parts.Add($"{profile.HeightCm:0.#} cm");

		HeaderDetailsLabel.Text = string.Join("  ·  ", parts);
		HeaderDetailsLabel.IsVisible = parts.Count > 0;
	}

	private void UpdateSummary()
	{
		var allRuns = _allRuns ?? new List<RunSession>();

		var weekStart = StartOfWeek(DateTime.Now);
		var runsThisWeek = allRuns.Where(r => ToLocal(r.StartTime).Date >= weekStart).ToList();
		SetSummaryCard(runsThisWeek, WeeklyDistanceLabel, WeeklyTimeLabel);
		SetSummaryCard(allRuns, AllTimeDistanceLabel, AllTimeTimeLabel);
	}

	private void SetSummaryCard(List<RunSession> runs, Label distanceLabel, Label detailLabel)
	{
		if (runs.Count == 0)
		{
			distanceLabel.Text = "0 km";
			detailLabel.Text = "No runs yet";
			return;
		}

		var km = runs.Sum(r => r.DistanceKm);
		var seconds = (int)Math.Round(runs.Sum(r => r.Duration.TotalSeconds));
		distanceLabel.Text = $"{km:0.00} km";
		detailLabel.Text = $"{SecondsToHMS(seconds)} · {runs.Count} {(runs.Count == 1 ? "run" : "runs")}";
	}

	private void RangePicker_SelectedIndexChanged(object? sender, EventArgs e)
	{
		var idx = RangePicker.SelectedIndex;
		string range = idx switch { 1 => "month", 2 => "week", _ => "all" };
		UpdateStatistics(range);
	}

	private void UpdateStatistics(string range)
	{
		try
		{
			IEnumerable<RunSession> runs = _allRuns ?? new List<RunSession>();
			var now = DateTime.Now;
			switch (range)
			{
				case "month":
					runs = runs.Where(r => ToLocal(r.StartTime).Year == now.Year && ToLocal(r.StartTime).Month == now.Month);
					break;
				case "week":
					var startOfWeek = StartOfWeek(now);
					runs = runs.Where(r => ToLocal(r.StartTime).Date >= startOfWeek);
					break;
			}

			var runList = runs.ToList();
			var count = runList.Count;
			StatTotalRuns.Text = $"{count}";

			if (count == 0)
			{
				foreach (var label in new[] { StatTotalHours, StatTotalKm, StatAvgPace, StatAvgKmPerRun, StatAvgHoursPerRun,
					StatLongestRun, StatLongestDuration, StatBestPace, StatTotalCalories, StatTotalSteps, StatAvgStepsPerRun })
				{
					label.Text = "-";
				}
				return;
			}

			var totalKm = runList.Sum(r => r.DistanceKm);
			var totalSeconds = runList.Sum(r => r.Duration.TotalSeconds);
			var totalHours = totalSeconds / 3600.0;
			StatTotalHours.Text = SecondsToHMS((int)Math.Round(totalSeconds));
			StatTotalKm.Text = $"{totalKm:0.##} km";

			// Average speed = total distance / total hours (km/h)
			var avgSpeed = totalHours > 0 ? (totalKm / totalHours) : 0;
			StatAvgPace.Text = $"{avgSpeed:0.##} km/h";

			StatAvgKmPerRun.Text = $"{totalKm / count:0.##} km";
			StatAvgHoursPerRun.Text = SecondsToHMS((int)Math.Round(totalSeconds / count));

			StatLongestRun.Text = $"{runList.Max(r => r.DistanceKm):0.##} km";
			StatLongestDuration.Text = runList.OrderByDescending(r => r.Duration.TotalSeconds).First().DurationFormatted;

			var timedRuns = runList.Where(r => r.Duration.TotalHours > 0).ToList();
			StatBestPace.Text = timedRuns.Count > 0 ? $"{timedRuns.Max(r => r.AveragePaceKmh):0.##} km/h" : "-";

			var totalSteps = runList.Sum(r => r.Steps);
			StatTotalCalories.Text = $"{runList.Sum(r => r.Calories):N0} kcal";
			StatTotalSteps.Text = $"{totalSteps:N0}";
			StatAvgStepsPerRun.Text = $"{(double)totalSteps / count:N0}";
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Stats compute failed: {ex}");
		}
	}

	private static string SecondsToHMS(int? seconds)
	{
		if (seconds == null || seconds == 0)
			return "-";
		var ts = TimeSpan.FromSeconds(seconds.Value);
		return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
	}

	private void SetEditing(bool editing)
	{
		NameEntry.IsEnabled = editing;
		WeightEntry.IsEnabled = editing;
		HeightEntry.IsEnabled = editing;
		DobPicker.IsEnabled = editing;
		GenderPicker.IsEnabled = editing;
		SaveButton.IsVisible = editing;
		CancelButton.IsVisible = editing;
		EditButton.IsVisible = !editing;

		// outline the card while editing so it's clear the fields are live
		PersonalInfoCard.Stroke = editing ? (Color)Resources["Primary"] : Colors.Transparent;

		// live-update header as user types name
		NameEntry.TextChanged -= NameEntry_TextChanged;
		if (editing) NameEntry.TextChanged += NameEntry_TextChanged;
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

			// date of birth and gender
			profile.DateOfBirth = DobPicker.Date;
			if (GenderPicker.SelectedItem is string gsel)
			{
				// normalize display value to stored representation expected by DB (lowercase, underscored)
				profile.Gender = MapDisplayGenderToStored(gsel);
			}

			var (ok, err) = await _auth.UpdateProfileAsync(profile);
			if (!ok)
			{
				await DisplayAlert("Error", "Failed to save profile: " + err, "OK");
			}
			else
			{
				await DisplayAlert("Saved", "Profile saved successfully.", "OK");
				_loadedProfile = profile;
				UpdateHeader(profile);
				SetEditing(false);
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", "Save failed: " + ex.Message, "OK");
		}
	}

	private void OnEditClicked(object? sender, EventArgs e)
	{
		SetEditing(true);
	}

	private void NameEntry_TextChanged(object? sender, TextChangedEventArgs e)
	{
		HeaderNameLabel.Text = string.IsNullOrWhiteSpace(e.NewTextValue) ? "Your name" : e.NewTextValue;
	}

	private void OnCancelClicked(object? sender, EventArgs e)
	{
		SetEditing(false);

		// revert to loaded profile values
		if (_loadedProfile != null)
			ShowProfile(_loadedProfile);
	}

	private async void OnLogoutClicked(object? sender, EventArgs e)
	{
		try
		{
			await AppShell.LogOutAsync();
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", "Logout failed: " + ex.Message, "OK");
		}
	}
}
