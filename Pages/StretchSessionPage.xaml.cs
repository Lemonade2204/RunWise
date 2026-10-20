using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using RunWise.Models;
using RunWise.ViewModels;

namespace RunWise.Pages
{
    public partial class StretchSessionPage : ContentPage
    {
        private static readonly Color Accent = Color.FromArgb("#5B3CDD");
        private static readonly Color Track = Color.FromArgb("#ECE8FB");

        private readonly StretchSessionViewModel _vm;
        private readonly RingDrawable _ring = new();
        private readonly List<Border> _segments = new();

        public StretchSessionPage(IEnumerable<Exercise> moves, string level, StretchKind kind = StretchKind.PreRun)
        {
            InitializeComponent();
            BindingContext = _vm = new StretchSessionViewModel(moves, level, kind);

            Ring.Drawable = _ring;
            BuildProgressBar();

            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.MoveChanged += UpdateProgressBar;
            _vm.Completed += OnCompleted;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _vm.Start();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _vm.Pause();
        }

        private void BuildProgressBar()
        {
            for (var i = 0; i < _vm.MoveCount; i++)
            {
                ProgressSegments.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                var seg = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 3 } };
                Grid.SetColumn(seg, i);
                ProgressSegments.Children.Add(seg);
                _segments.Add(seg);
            }
            UpdateProgressBar();
        }

        // Segments up to and including the current move are filled.
        private void UpdateProgressBar()
        {
            for (var i = 0; i < _segments.Count; i++)
                _segments[i].BackgroundColor = i <= _vm.CurrentIndex ? Accent : Track;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StretchSessionViewModel.Progress))
            {
                _ring.Progress = _vm.Progress;
                Ring.Invalidate();
            }
        }

        private async void OnCompleted()
        {
            if (_vm.IsPostRun)
                await DisplayAlert("Cool-down complete", "Nice work. Grab some water and let your legs recover.", "Done");
            else
                await DisplayAlert("Warm-up complete", "Your legs are ready. Have a great run!", "Let's go");
            await Navigation.PopAsync();
        }

        private async void OnCloseTapped(object? sender, TappedEventArgs e)
        {
            var wasRunning = _vm.IsRunning;
            _vm.Pause();
            var name = _vm.IsPostRun ? "cool-down" : "warm-up";
            if (await DisplayAlert($"End {name}?", $"Your progress on this {name} won't be saved.", "End", "Keep going"))
            {
                await Navigation.PopAsync();
                return;
            }
            if (wasRunning) _vm.Start();
        }

        private void OnPlayPauseTapped(object? sender, TappedEventArgs e) => _vm.TogglePlayPause();
        private void OnPreviousTapped(object? sender, TappedEventArgs e) => _vm.Previous();
        private void OnNextTapped(object? sender, TappedEventArgs e) => _vm.Next();

        private class RingDrawable : IDrawable
        {
            public double Progress { get; set; } = 1;

            public void Draw(ICanvas canvas, RectF rect)
            {
                const float stroke = 12;
                var size = Math.Min(rect.Width, rect.Height) - stroke;
                var x = rect.Center.X - size / 2;
                var y = rect.Center.Y - size / 2;

                canvas.StrokeSize = stroke;
                canvas.StrokeLineCap = LineCap.Round;

                canvas.StrokeColor = Track;
                canvas.DrawEllipse(x, y, size, size);

                if (Progress <= 0) return;
                canvas.StrokeColor = Accent;
                if (Progress >= 0.999)
                {
                    canvas.DrawEllipse(x, y, size, size);
                    return;
                }

                // Arc runs clockwise from 12 o'clock and shrinks as time runs out.
                var sweep = (float)(360 * Progress);
                canvas.DrawArc(x, y, size, size, 90, 90 - sweep, true, false);
            }
        }
    }
}
