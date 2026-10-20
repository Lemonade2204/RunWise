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

	private void OnTogglePasswordTapped(object? sender, EventArgs e)
	{
		PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
		EyeIcon.Stroke = PasswordEntry.IsPassword ? Color.FromArgb("#6B7489") : Color.FromArgb("#4A6CF7");
	}

	private async void OnLoginClicked(object? sender, EventArgs e)
	{
		if (!LoginButton.IsEnabled) return;

		ErrorLabel.IsVisible = false;

		var email = EmailEntry.Text?.Trim() ?? string.Empty;
		var password = PasswordEntry.Text ?? string.Empty;
		if (email.Length == 0 || password.Length == 0)
		{
			ErrorLabel.Text = "Please enter your email and password";
			ErrorLabel.IsVisible = true;
			return;
		}

		LoginButton.IsEnabled = false;
		BusyIndicator.IsVisible = true;
		BusyIndicator.IsRunning = true;

		var result = await _authService.SignInAsync(email, password);
		BusyIndicator.IsRunning = false;
		BusyIndicator.IsVisible = false;
		LoginButton.IsEnabled = true;

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

	private async void OnForgotPasswordTapped(object? sender, EventArgs e)
	{
		// Prefer the email already entered, otherwise prompt the user
		var current = EmailEntry.Text?.Trim();
		string? email = current;
		if (string.IsNullOrWhiteSpace(email))
		{
			email = await DisplayPromptAsync("Reset password", "Enter your email to receive reset instructions", "Send", "Cancel", "Email", -1, Keyboard.Email);
			if (string.IsNullOrWhiteSpace(email))
				return; // cancelled or empty
		}

		BusyIndicator.IsVisible = true;
		BusyIndicator.IsRunning = true;
		var (success, error) = await _authService.SendPasswordResetAsync(email!);
		BusyIndicator.IsRunning = false;
		BusyIndicator.IsVisible = false;

		if (success)
		{
			await DisplayAlert("Password reset", "If the email exists, a password reset link has been sent.", "OK");
		}
		else
		{
			await DisplayAlert("Password reset failed", error ?? "Unknown error", "OK");
		}
	}
}
