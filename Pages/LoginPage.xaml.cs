using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using RunWise.Services;

namespace RunWise.Pages;

public partial class LoginPage : ContentPage
{
	private readonly SupabaseAuthService _authService;

	public LoginPage()
	{
		InitializeComponent();
		_authService = MauiProgram.Services.GetService(typeof(SupabaseAuthService)) as SupabaseAuthService
			?? throw new InvalidOperationException("SupabaseAuthService is not registered. Register it in MauiProgram.cs");
	}

	private async void OnLoginClicked(object? sender, EventArgs e)
	{
		ErrorLabel.IsVisible = false;
		BusyIndicator.IsVisible = true;
		BusyIndicator.IsRunning = true;

		var email = EmailEntry.Text?.Trim() ?? string.Empty;
		var password = PasswordEntry.Text ?? string.Empty;

		var result = await _authService.SignInAsync(email, password);
		BusyIndicator.IsRunning = false;
		BusyIndicator.IsVisible = false;

		if (result.Success)
		{
			// Clean local runs that belong to other users, then show main UI
			try
			{
				var tracker = MauiProgram.Services.GetService(typeof(RunWise.Services.RunTrackerService)) as RunWise.Services.RunTrackerService
					?? new RunWise.Services.RunTrackerService();
				if (!string.IsNullOrEmpty(_authService.UserId))
				{
					await tracker.RemoveLocalRunsNotOwnedAsync(_authService.UserId);
				}
			}
			catch { }

			// Replace the app's MainPage with the AppShell to show the main app UI
			// Ensure this runs on the UI thread to avoid fragment/view attach timing issues on Android
			MainThread.BeginInvokeOnMainThread(() => Application.Current.MainPage = new AppShell());
		}
		else
		{
			ErrorLabel.Text = result.Error ?? "Login failed";
			ErrorLabel.IsVisible = true;
		}
	}

	private async void OnRegisterTapped(object? sender, EventArgs e)
	{
		// Push the register page on the navigation stack
		await Navigation.PushAsync(new RegisterPage());
	}
}
