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

    private async void OnStartPreRunClicked(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new StretchRoutinePage(RunWise.ViewModels.StretchKind.PreRun));
    }

    private async void OnStartPostRunClicked(object? sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new StretchRoutinePage(RunWise.ViewModels.StretchKind.PostRun));
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
