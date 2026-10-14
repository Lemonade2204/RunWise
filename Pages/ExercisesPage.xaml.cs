using Microsoft.Maui.Controls;
using RunWise.ViewModels;

namespace RunWise.Pages
{
    public partial class ExercisesPage : ContentPage
    {
        public ExercisesPage()
        {
            InitializeComponent();
            // Try to resolve the viewmodel from DI so it can use the ExercisesApiService
            var vm = MauiProgram.Services.GetService(typeof(ExercisesViewModel)) as ExercisesViewModel;
            if (vm is null)
            {
                BindingContext = new ExercisesViewModel();
            }
            else
            {
                BindingContext = vm;
            }
        }

    private async void OnStartPreRunClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheet("Select level", "Cancel", null, "Beginner", "Intermediate", "Advanced");
        if (string.IsNullOrEmpty(choice) || choice == "Cancel")
            return;
        // Map UI labels to API level values expected by MuscleWiki
        var level = choice switch
        {
            "Beginner" => "beginner",
            "Intermediate" => "intermediate",
            "Advanced" => "advance",
            _ => "beginner",
        };

        // show simple loading indicator
        try
        {
            // Use local routine generator instead of external API
            if (BindingContext is RunWise.ViewModels.ExercisesViewModel vm)
            {
                var list = await vm.CreateRoutineAsync("pre", level).ConfigureAwait(false);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (list == null || list.Count == 0)
                    {
                        return;
                    }

                    await Navigation.PushAsync(new WorkoutPage(list, $"{choice} Pre-workout"));
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create routine: {ex.Message}", "OK");
        }
    }

    private async void OnStartPostRunClicked(object? sender, EventArgs e)
    {
        // Immediately generate a post-workout routine (no level selection)
        try
        {
            if (BindingContext is RunWise.ViewModels.ExercisesViewModel vm)
            {
                var list = await vm.CreateRoutineAsync("post", null).ConfigureAwait(false);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (list == null || list.Count == 0)
                    {
                        return;
                    }

                    await Navigation.PushAsync(new WorkoutPage(list, "Post-workout"));
                });
            }
        }
        catch (Exception ex)
        {
           
        }
    }

    private async void OnStartExerciseClicked(object? sender, EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is RunWise.Models.Exercise ex)
        {
            if (BindingContext is ExercisesViewModel vm)
            {
                
                var pre = vm.PreRunExercises?.ToList() ?? new System.Collections.Generic.List<RunWise.Models.Exercise>();
                var post = vm.PostRunExercises?.ToList() ?? new System.Collections.Generic.List<RunWise.Models.Exercise>();
                if (pre.Contains(ex))
                {
                    await Navigation.PushAsync(new ExerciseDetailPage(pre, pre.IndexOf(ex)));
                    return;
                }
                if (post.Contains(ex))
                {
                    await Navigation.PushAsync(new ExerciseDetailPage(post, post.IndexOf(ex)));
                    return;
                }

                // fallback: open single exercise
                await Navigation.PushAsync(new ExerciseDetailPage(new[] { ex }, 0));
            }
        }
    }
           

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is ExercisesViewModel vm)
            {
                // Fire-and-forget loading of remote exercises if available
                _ = vm.LoadRemoteExercisesAsync();
            }
        }
    }
}
