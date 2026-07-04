using System;
using Microsoft.Maui.Controls;
using RunWise.Services;

namespace RunWise.Pages;

public partial class RegisterPage : ContentPage
{
    private readonly SupabaseAuthService _authService;

    public RegisterPage()
    {
        InitializeComponent();
        _authService = MauiProgram.Services.GetService(typeof(SupabaseAuthService)) as SupabaseAuthService
            ?? throw new InvalidOperationException("SupabaseAuthService is not registered. Register it in MauiProgram.cs");
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        BusyIndicator.IsVisible = true;
        BusyIndicator.IsRunning = true;

        try
        {
            var email = EmailEntry.Text?.Trim() ?? string.Empty;
            var password = PasswordEntry.Text ?? string.Empty;
            var confirm = ConfirmEntry.Text ?? string.Empty;

            if (password != confirm)
            {
                ErrorLabel.Text = "Passwords do not match";
                ErrorLabel.IsVisible = true;
                return;
            }

            var result = await _authService.SignUpAsync(email, password);

            if (result.Success)
            {
                // Try to create the profile row in Supabase (non-fatal)
                try
                {
                    var (ok, err) = await _authService.CreateProfileAsync(email, null);
                    if (!ok)
                    {
                        // non-fatal: show message but still return to login
                        await Application.Current.MainPage.DisplayAlert("Profile", "Profile creation warning: " + err, "OK");
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal: report error to the user but continue to login
                    await Application.Current.MainPage.DisplayAlert("Profile", "Profile creation error: " + ex.Message, "OK");
                }

                // After successful register, pop back to the LoginPage and show confirmation
                await Navigation.PopAsync();
                await DisplayAlert("Registered", "Registration successful. Please log in.", "OK");
            }
            else
            {
                ErrorLabel.Text = result.Error ?? "Registration failed";
                ErrorLabel.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            // TEMPORARY: verbose error detail so we can see the real cause.
            // Revert to a friendly generic message once the issue is diagnosed.
            var detail = ex.InnerException != null
                ? $"{ex.GetType().Name}: {ex.Message} | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}"
                : $"{ex.GetType().Name}: {ex.Message}";

            System.Diagnostics.Debug.WriteLine($"[RegisterPage] Registration failed: {ex}");

            ErrorLabel.Text = detail;
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
        }
    }
}
