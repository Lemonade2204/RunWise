using RunWise.ViewModels;

namespace RunWise.Pages
{
    public partial class StretchRoutinePage : ContentPage
    {
        private readonly StretchRoutineViewModel _vm;

        public StretchRoutinePage(StretchKind kind = StretchKind.PreRun)
        {
            InitializeComponent();
            BindingContext = _vm = new StretchRoutineViewModel(kind);

            HeroStart.Color = _vm.HeroStartColor;
            HeroEnd.Color = _vm.HeroEndColor;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = _vm.LoadRecommendationAsync();
        }

        private async void OnBackTapped(object? sender, TappedEventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnStartTapped(object? sender, TappedEventArgs e)
        {
            if (_vm.Routine.Count == 0)
                return;

            await Navigation.PushAsync(new StretchSessionPage(_vm.Routine, _vm.SelectedLevel, _vm.Kind));
        }

        private async void OnMoveTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is StretchMoveItem item)
                await Navigation.PushAsync(new ExerciseDetailPage(_vm.Routine, item.Index));
        }
    }
}
