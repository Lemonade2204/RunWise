using CommunityToolkit.Mvvm.ComponentModel;
using RunWise.Models;

namespace RunWise.ViewModels;

public partial class StretchSessionViewModel : ObservableObject
{
    private readonly List<Exercise> _moves;
    private IDispatcherTimer? _timer;
    private DateTime _lastTick;

    public event Action? Completed;
    public event Action? MoveChanged;

    public StretchSessionViewModel(IEnumerable<Exercise> moves, string level, StretchKind kind = StretchKind.PreRun)
    {
        _moves = moves.ToList();
        Level = level;
        Kind = kind;
        LoadMove(0);
    }

    public string Level { get; }
    public StretchKind Kind { get; }
    public bool IsPostRun => Kind == StretchKind.PostRun;
    public string SessionTitle => IsPostRun ? "Cool-Down" : "Warm-Up";
    public int MoveCount => _moves.Count;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MoveCounterText), nameof(HasNext), nameof(UpNextTitle), nameof(UpNextAmount), nameof(UpNextLabel))]
    private int _currentIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeText), nameof(Progress), nameof(TimeCaption))]
    private double _remainingSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Progress))]
    private int _totalSeconds;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _focus = string.Empty;

    [ObservableProperty]
    private string _instructions = string.Empty;

    public Exercise Current => _moves[CurrentIndex];
    public string MoveCounterText => $"{(IsPostRun ? "Stretch" : "Move")} {CurrentIndex + 1} of {MoveCount}";
    public bool HasNext => CurrentIndex < MoveCount - 1;
    public string UpNextLabel => HasNext ? "UP NEXT" : IsPostRun ? "LAST STRETCH" : "LAST MOVE";
    public string UpNextTitle => HasNext ? _moves[CurrentIndex + 1].Title : IsPostRun ? "Finish and recover" : "Finish and go run";
    public string UpNextAmount => HasNext ? new StretchMoveItem(_moves[CurrentIndex + 1], CurrentIndex + 1).AmountText : string.Empty;

    // 1 = full ring at start of a move, 0 = empty when time is up
    public double Progress => TotalSeconds <= 0 ? 0 : Math.Clamp(RemainingSeconds / TotalSeconds, 0, 1);

    // Per-side holds ("30s each leg") run as one timer that switches legs halfway.
    public bool IsSecondSide => StretchTiming.IsPerSideHold(Current) && RemainingSeconds <= TotalSeconds / 2.0;

    public string TimeCaption
    {
        get
        {
            var e = Current;
            if (StretchTiming.IsRepBased(e))
                return $"{new StretchMoveItem(e, CurrentIndex).AmountText} · steady pace";
            if (StretchTiming.IsPerSideHold(e))
                return IsSecondSide ? "second side" : "first side · switch halfway";
            return "seconds left";
        }
    }

    public string TimeText
    {
        get
        {
            var s = (int)Math.Ceiling(RemainingSeconds);
            return $"{s / 60}:{s % 60:00}";
        }
    }

    private void LoadMove(int index)
    {
        CurrentIndex = index;
        var e = Current;
        Title = e.Title;
        Focus = e.Focus;
        Instructions = string.IsNullOrWhiteSpace(e.Tips) ? e.Description : e.Tips;
        TotalSeconds = StretchTiming.MoveSeconds(e);
        RemainingSeconds = TotalSeconds;
        OnPropertyChanged(nameof(TimeCaption));
        MoveChanged?.Invoke();
    }

    public void Start()
    {
        if (_timer == null)
        {
            _timer = Application.Current!.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += OnTick;
        }
        _lastTick = DateTime.UtcNow;
        _timer.Start();
        IsRunning = true;
    }

    public void Pause()
    {
        _timer?.Stop();
        IsRunning = false;
    }

    public void TogglePlayPause()
    {
        if (IsRunning) Pause();
        else Start();
    }

    public void Next()
    {
        if (HasNext)
            LoadMove(CurrentIndex + 1);
        else
            Finish();
    }

    // Restart the current move if it's under way, otherwise step back one move.
    public void Previous()
    {
        if (TotalSeconds - RemainingSeconds > 3 || CurrentIndex == 0)
            LoadMove(CurrentIndex);
        else
            LoadMove(CurrentIndex - 1);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        var wasSecondSide = IsSecondSide;
        RemainingSeconds = Math.Max(0, RemainingSeconds - elapsed);

        // Short buzz when it's time to switch to the other leg.
        if (!wasSecondSide && IsSecondSide && RemainingSeconds > 0)
        {
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
        }

        if (RemainingSeconds > 0) return;

        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
        Next();
    }

    private void Finish()
    {
        Pause();
        RemainingSeconds = 0;
        Completed?.Invoke();
    }
}
