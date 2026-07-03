using System;
using Microsoft.Maui.Controls;
using RunWise.ViewModels;
using RunWise.Models;

namespace RunWise.Pages;

public partial class HistoryPage : ContentPage
{
    public HistoryPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is HistoryViewModel vm)
            await vm.LoadHistoryCommand.ExecuteAsync(null);
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is RunSession run)
        {
            await Shell.Current.GoToAsync($"editrun?runId={Uri.EscapeDataString(run.Id)}");
        }
    }

    private async void OnRunTapped(object sender, TappedEventArgs e)
    {
        // The GestureRecognizer's sender may be the Frame; use its BindingContext to find the RunSession.
        if (sender is VisualElement ve && ve.BindingContext is RunSession run)
        {
            await Shell.Current.GoToAsync($"editrun?runId={Uri.EscapeDataString(run.Id)}");
        }
    }

    private async void OnViewClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is RunSession run)
        {
            await Shell.Current.GoToAsync($"rundetails?runId={Uri.EscapeDataString(run.Id)}");
        }
    }
}
