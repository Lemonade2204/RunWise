using System.Collections.Generic;
using Microsoft.Maui.Controls;
using RunWise.Models;

namespace RunWise.Pages;

public partial class WorkoutPage : ContentPage
{
    private readonly List<Exercise> _exercises;

    public WorkoutPage(IEnumerable<Exercise> exercises, string? title = null)
    {
        InitializeComponent();
        _exercises = exercises?.ToList() ?? new List<Exercise>();
        ExercisesCollection.ItemsSource = _exercises;
        TitleLabel.Text = title ?? "Workout";
    }

    private async void OnStartClicked(object? sender, System.EventArgs e)
    {
        if (_exercises.Count == 0)
        {
            await DisplayAlert("No exercises", "No exercises to start.", "OK");
            return;
        }

        await Navigation.PushAsync(new ExerciseDetailPage(_exercises, 0));
    }

    private async void OnViewClicked(object? sender, System.EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is Exercise ex)
        {
            var idx = _exercises.IndexOf(ex);
            if (idx >= 0)
                await Navigation.PushAsync(new ExerciseDetailPage(_exercises, idx));
        }
    }
}
