using System;
using System.IO;
using Microsoft.Maui.Controls;
using RunWise.Services;

namespace RunWise.Pages;

public partial class ProfilePage : ContentPage
{
	private readonly SupabaseAuthService? _auth;

	public ProfilePage()
	{
		InitializeComponent();
		_auth = MauiProgram.Services.GetService(typeof(SupabaseAuthService)) as SupabaseAuthService;
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