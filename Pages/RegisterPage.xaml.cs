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

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        try { await Navigation.PopAsync(); } catch { }
    }

    // Shows or hides both password fields together
    private void OnTogglePasswordTapped(object? sender, EventArgs e)
    {
        var hide = !PasswordEntry.IsPassword;
        PasswordEntry.IsPassword = hide;
        ConfirmEntry.IsPassword = hide;
        EyeIcon.Stroke = hide ? Color.FromArgb("#6B7489") : Color.FromArgb("#4A6CF7");
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;

        var fullName = NameEntry.Text?.Trim() ?? string.Empty;
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;
        var confirm = ConfirmEntry.Text ?? string.Empty;

        if (fullName.Length == 0) { ShowError("Please enter your full name"); return; }
        if (email.Length == 0) { ShowError("Please enter your email address"); return; }
        if (password != confirm) { ShowError("Passwords do not match"); return; }
        if (!TermsCheckBox.IsChecked) { ShowError("Please agree to the Terms of Service and Privacy Policy"); return; }

        CreateButton.IsEnabled = false;
        BusyIndicator.IsVisible = true;
        BusyIndicator.IsRunning = true;

        try
        {

            var result = await _authService.SignUpAsync(email, password);

            if (result.Success)
            {
                // Try to create the profile row in Supabase (non-fatal)
                try
                {
                    var (ok, err) = await _authService.CreateProfileAsync(email, fullName);
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
            CreateButton.IsEnabled = true;
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
        }
    }
}
