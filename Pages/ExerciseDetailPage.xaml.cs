using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using RunWise.Models;

namespace RunWise.Pages;

public partial class ExerciseDetailPage : ContentPage
{
    private readonly List<Exercise> _exercises;
    private int _index;
    private int _remainingSeconds = 0;
    private bool _timerRunning = false;

    public ExerciseDetailPage(IEnumerable<Exercise> exercises, int startIndex = 0)
    {
        InitializeComponent();
        _exercises = exercises?.ToList() ?? new List<Exercise>();
        _index = startIndex >= 0 && startIndex < _exercises.Count ? startIndex : 0;
        LoadExercise(_index);
    }

    private void LoadExercise(int idx)
    {
        if (idx < 0 || idx >= _exercises.Count) return;
        var ex = _exercises[idx];
        TitleLabel.Text = ex.Title;
        DescLabel.Text = ex.Description;
        TipsLabel.Text = ex.Tips;

        // Show placeholder instruction video when available
        if (!string.IsNullOrWhiteSpace(ex.InstructionVideoUrl))
        {
            var v = ex.InstructionVideoUrl!;
            // If youtube watch link, convert to embed
            if (v.Contains("youtube.com/watch?v="))
            {
                var id = System.Text.RegularExpressions.Regex.Match(v, "(?<=v=)[^&]+").Value;
                if (!string.IsNullOrEmpty(id))
                    VideoWebView.Source = $"https://www.youtube.com/embed/{id}";
                else
                    VideoWebView.Source = v;
            }
            else if (v.Contains("youtu.be/"))
            {
                var id = System.Text.RegularExpressions.Regex.Match(v, "(?<=youtu.be/)[^?&]+").Value;
                if (!string.IsNullOrEmpty(id))
                    VideoWebView.Source = $"https://www.youtube.com/embed/{id}";
                else
                    VideoWebView.Source = v;
            }
            else
            {
                VideoWebView.Source = v;
            }
            VideoWebView.IsVisible = true;
        }
        else
        {
            VideoWebView.IsVisible = false;
        }

        // Setup timer duration
        _timerRunning = false;
        _remainingSeconds = ex.DurationSeconds ?? ParseDurationSecondsFromText(ex.Description) ?? ParseDurationSecondsFromText(ex.Tips) ?? 30;
        UpdateTimerLabel();
        StartCountdown();

        // Allow navigation to previous exercise; Next enabled only if there are remaining exercises
        PrevButton.IsEnabled = idx > 0;
        NextButton.IsEnabled = idx < _exercises.Count - 1;
    }

    private async void OnPrevClicked(object? sender, System.EventArgs e)
    {
        if (_index > 0)
        {
            _index--;
            LoadExercise(_index);
        }
    }

    private async void OnNextClicked(object? sender, System.EventArgs e)
    {
        if (_index < _exercises.Count - 1)
        {
            _index++;
            LoadExercise(_index);
        }
        else
        {
            await Navigation.PopAsync();
        }
    }

    private async void OnDoneClicked(object? sender, System.EventArgs e)
    {
        // Stop timer and advance to the next exercise; allow going back via Prev button
        _timerRunning = false;

        if (_exercises.Count == 0)
        {
            await Navigation.PopAsync();
            return;
        }

        // If not the last exercise, advance to next
        if (_index < _exercises.Count - 1)
        {
            _index++;
            LoadExercise(_index);
        }
        else
        {
            // Last exercise completed -> return to previous page
            await Navigation.PopAsync();
        }
    }

    private int? ParseDurationSecondsFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            var s = text.ToLowerInvariant();
            var m = System.Text.RegularExpressions.Regex.Match(s, "\\d+");
            if (m.Success && int.TryParse(m.Value, out var n))
            {
                if (s.Contains("min") || s.Contains("minute")) return n * 60;
                if (s.Contains("sec") || s.Contains("second") || s.Contains("s")) return n;
                if (n <= 60) return n;
                return n;
            }
        }
        catch { }
        return null;
    }

    private void UpdateTimerLabel()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TimerLabel.Text = _remainingSeconds > 0 ? $"Time: {_remainingSeconds}s" : string.Empty;
        });
    }

    private void StartCountdown()
    {
        _timerRunning = true;
        Device.StartTimer(System.TimeSpan.FromSeconds(1), () =>
        {
            if (!_timerRunning) return false;
            if (_remainingSeconds > 0)
            {
                _remainingSeconds--;
                UpdateTimerLabel();
                return true;
            }
            else
            {
                // Auto-advance to next exercise
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (_index < _exercises.Count - 1)
                    {
                        _index++;
                        LoadExercise(_index);
                    }
                    else
                    {
                        _timerRunning = false;
                        await Navigation.PopAsync();
                    }
                });
                return false;
            }
        });
    }
}
