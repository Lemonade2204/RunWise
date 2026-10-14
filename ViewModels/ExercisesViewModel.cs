using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using RunWise.Models;
using System.Text.Json;
using System.Linq;
using Microsoft.Maui.ApplicationModel;

namespace RunWise.ViewModels
{
    public class ExercisesViewModel : INotifyPropertyChanged
    {
        
        // MuscleWiki API removed; no remote service dependency
        public ObservableCollection<Exercise> AllExercises { get; } = new ObservableCollection<Exercise>();
        public ObservableCollection<Exercise> FilteredExercises { get; } = new ObservableCollection<Exercise>();
        public ObservableCollection<Exercise> PreRunExercises { get; } = new ObservableCollection<Exercise>();
        public ObservableCollection<Exercise> PostRunExercises { get; } = new ObservableCollection<Exercise>();

        private Difficulty _selectedDifficulty = Difficulty.Easy;
        private string _selectedLevel = "novice"; // novice|beginner|intermediate|advance
        public string SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                if (_selectedLevel == value) return;
                _selectedLevel = value;
                OnPropertyChanged(nameof(SelectedLevel));
            }
        }
        public Difficulty SelectedDifficulty
        {
            get => _selectedDifficulty;
            set
            {
                if (_selectedDifficulty == value) return;
                _selectedDifficulty = value;
                OnPropertyChanged(nameof(SelectedDifficulty));
                OnPropertyChanged(nameof(IsEasy));
                OnPropertyChanged(nameof(IsMedium));
                OnPropertyChanged(nameof(IsHard));
                ApplyFilter();
            }
        }

        public bool IsEasy => SelectedDifficulty == Difficulty.Easy;
        public bool IsMedium => SelectedDifficulty == Difficulty.Medium;
        public bool IsHard => SelectedDifficulty == Difficulty.Hard;

        public ICommand SetDifficultyCommand { get; private set; }
        public ICommand SetMuscleCommand { get; private set; }

        public ExercisesViewModel()
        {
            // initialize commands and sample data for offline/local usage
            InitializeCommands();
            SeedSampleData();
            ApplyFilter();
        }

        
        private void InitializeCommands()
        {
            SetDifficultyCommand = new RelayCommand<object>(p =>
            {
                if (p is string s && System.Enum.TryParse<Difficulty>(s, out var d)) SelectedDifficulty = d;
                else if (p is Difficulty dd) SelectedDifficulty = dd;
            });

            RefreshCommand = new RelayCommand(async _ => await LoadRemoteExercisesAsync());

            // Commands to generate pre/post routines using local generator
            GeneratePreRunCommand = new RelayCommand(async _ => await CreateRoutineAsync("pre", SelectedLevel));
            GeneratePostRunCommand = new RelayCommand(async _ => await CreateRoutineAsync("post", SelectedLevel));

            SetMuscleCommand = new RelayCommand<object>(async p =>
            {
                if (p is string s)
                {
                    if (string.IsNullOrWhiteSpace(s))
                    {
                        
                        await LoadRemoteExercisesAsync();
                    }
                    else if (string.Equals(s, "legs", System.StringComparison.OrdinalIgnoreCase))
                    {
                       
                
                    }
                    else
                    {
                       
                        await LoadRemoteExercisesAsync(muscle: s);
                    }
                }
            });
        }

        public ICommand? RefreshCommand { get; private set; }
        public ICommand? GeneratePreRunCommand { get; private set; }
        public ICommand? GeneratePostRunCommand { get; private set; }

        private void SeedSampleData()
        {
            AllExercises.Clear();
            AllExercises.Add(new Exercise { Title = "Calf Stretch", Description = "Stand facing a wall, place hands on wall and step one foot back. Press heel down to stretch calf.", Difficulty = Difficulty.Easy, Tips = "Hold 20-30s per leg." });
            AllExercises.Add(new Exercise { Title = "Quadriceps Stretch", Description = "Standing, pull your heel to your glutes keeping knees close. Use support if needed.", Difficulty = Difficulty.Easy, Tips = "Avoid arching low back; keep hips level." });
            AllExercises.Add(new Exercise { Title = "Hamstring Stretch (seated)", Description = "Sit with one leg extended, hinge at hips reaching toward toes.", Difficulty = Difficulty.Easy, Tips = "Keep spine long; don't bounce." });
            AllExercises.Add(new Exercise { Title = "Hip Flexor Lunge", Description = "Lunge forward and gently press hips down to feel stretch in front of rear hip.", Difficulty = Difficulty.Medium, Tips = "Squeeze glute of rear leg for deeper stretch." });
            AllExercises.Add(new Exercise { Title = "IT Band Slide", Description = "Cross one foot over the other and lean to the side to feel outer thigh stretch.", Difficulty = Difficulty.Medium, Tips = "Do not force; keep knees soft." });
            AllExercises.Add(new Exercise { Title = "Dynamic Leg Swings", Description = "Front-to-back leg swings to loosen hips and hamstrings.", Difficulty = Difficulty.Medium, Tips = "Start slow, increase range gradually; hold support if needed." });
            AllExercises.Add(new Exercise { Title = "Ankle Circles", Description = "Rotate ankles clockwise and counter-clockwise to warm up joints.", Difficulty = Difficulty.Easy, Tips = "Perform 10 each direction per ankle." });
            AllExercises.Add(new Exercise { Title = "Walking Lunges", Description = "Step forward into a lunge and alternate legs while moving forward.", Difficulty = Difficulty.Hard, Tips = "Keep torso upright; use controlled steps for balance." });
            AllExercises.Add(new Exercise { Title = "Single-Leg Romanian Deadlift", Description = "Balance on one leg, hinge at hips keeping back flat to feel hamstring/glute engagement.", Difficulty = Difficulty.Hard, Tips = "Use light support or a small weight if stable." });
        }

        private void ApplyFilter()
        {
            FilteredExercises.Clear();
            foreach (var ex in AllExercises)
            {
                if (ex.Difficulty == SelectedDifficulty)
                {
                    FilteredExercises.Add(ex);
                }
            }
        }

        public async System.Threading.Tasks.Task LoadRemoteExercisesAsync(string? muscle = null, int limit = 30)
        {
           
        }
        // Create a routine locally using built-in fallback exercises. No external API calls.
        public async System.Threading.Tasks.Task<System.Collections.Generic.List<Exercise>> CreateRoutineAsync(string when, string? level)
        {
            var dest = when == "pre" ? PreRunExercises : PostRunExercises;
            dest.Clear();

            System.Collections.Generic.List<Exercise> list;

            if (string.Equals(when, "pre", System.StringComparison.OrdinalIgnoreCase))
            {
                // Use user-provided pre-run routines per level
                var lvl = (level ?? SelectedLevel ?? "beginner").Trim().ToLowerInvariant();
                if (lvl == "beginner" || lvl == "novice")
                {
                    list = new System.Collections.Generic.List<Exercise>
                    {
                        new Exercise { Title = "Bodyweight Butterfly Stretch", Description = "30s", Tips = "Sit and bring soles together, gently press knees toward floor." },
                        new Exercise { Title = "Bodyweight Squat", Description = "10 reps", Tips = "Feet shoulder-width, hinge hips back, keep chest up." },
                        new Exercise { Title = "Forward Lunges", Description = "8 reps", Tips = "Step forward, lower until both knees ~90 degrees." },
                        new Exercise { Title = "Calf Raises", Description = "15 reps", Tips = "Rise onto toes, control the descent." }
                    };
                }
                else if (lvl == "intermediate")
                {
                    list = new System.Collections.Generic.List<Exercise>
                    {
                        new Exercise { Title = "Bodyweight Butterfly Stretch", Description = "30s", Tips = "Sit and bring soles together, gently press knees toward floor." },
                        new Exercise { Title = "Half Kneeling Hip Hinge", Description = "8 reps", Tips = "From half-kneeling, hinge at hips to feel posterior chain activation." },
                        new Exercise { Title = "Curtsy Lunges", Description = "8 reps", Tips = "Step one leg behind and across to target glutes." },
                        new Exercise { Title = "Lateral Lunges", Description = "8 reps", Tips = "Step to the side and push hips back while keeping chest up." },
                        new Exercise { Title = "Bodyweight Swing Lunge", Description = "8 reps", Tips = "Explosive alternating swing lunge for dynamic warmup." }
                    };
                }
                else // advanced
                {
                    list = new System.Collections.Generic.List<Exercise>
                    {
                        new Exercise { Title = "Bodyweight Butterfly Stretch", Description = "30s", Tips = "Sit and bring soles together, gently press knees toward floor." },
                        new Exercise { Title = "Deadlift Bounces Stretch", Description = "10 reps", Tips = "Soft bouncy movements to wake up hamstrings and glutes." },
                        new Exercise { Title = "Single Leg Romanian Deadlifts", Description = "8 each leg", Tips = "Slow hinge on single leg to load posterior chain." },
                        new Exercise { Title = "Forward Lunge", Description = "10 reps  ", Tips = "Deep controlled forward lunges." },
                        new Exercise { Title = "Jump Squats", Description = "10 reps", Tips = "Explosive plyometric squats; land softly." }
                    };
                }
            }
            else
            {
                
                list = new System.Collections.Generic.List<Exercise>
                {
                    new Exercise { Title = "Standing quad stretch left", Description = "30s", Tips = "Stand, pull heel to glutes and hold." },
                    new Exercise { Title = "Standing quad stretch right", Description = "30s", Tips = "Stand, pull heel to glutes and hold." },
                    new Exercise { Title = "Lunging Hip Flexor Stretch left", Description = "30s", Tips = "Kneel and push hips forward to stretch hip flexor." },
                    new Exercise { Title = "Lunging Hip Flexor Stretch right", Description = "30s", Tips = "Kneel and push hips forward to stretch hip flexor." },
                    new Exercise { Title = "Standing hamstring stretch", Description = "30s", Tips = "Extend leg and hinge at hips to stretch hamstring." },
                    new Exercise { Title = "Calf stretch left", Description = "30s", Tips = "Press heel to floor with straight leg to feel calf stretch." },
                    new Exercise { Title = "Calf stretch right", Description = "30s", Tips = "Press heel to floor with straight leg to feel calf stretch." },
                    new Exercise { Title = "Forward bend", Description = "30s", Tips = "Hinge at hips and reach toward toes; keep knees soft." },
                    new Exercise { Title = "Sitting hamstring stretch left", Description = "30s", Tips = "Sit and reach toward toes of extended leg." },
                    new Exercise { Title = "Sitting hamstring stretch right", Description = "30s", Tips = "Sit and reach toward toes of extended leg." }
                };
            }

      
            foreach (var e in list)
            {
                e.Difficulty = MapLevelToDifficulty(level ?? SelectedLevel);
                e.DurationSeconds = ParseDurationSeconds(e.Description) ?? ParseDurationSeconds(e.Tips);
                e.InstructionVideoUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ"; // placeholder
                dest.Add(e);
            }

            await System.Threading.Tasks.Task.CompletedTask;
            return list;
        }

        private static Difficulty MapLevelToDifficulty(string? level)
        {
            if (string.IsNullOrEmpty(level)) return Difficulty.Medium;
            var s = level.Trim().ToLowerInvariant();
            return s switch
            {
                "novice" => Difficulty.Easy,
                "beginner" => Difficulty.Easy,
                "intermediate" => Difficulty.Medium,
                "advance" => Difficulty.Hard,
                "advanced" => Difficulty.Hard,
                _ => Difficulty.Medium,
            };
        }

       
        private static int? ParseDurationSeconds(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try
            {
                var s = text.ToLowerInvariant();
                // find number
                var numStr = System.Text.RegularExpressions.Regex.Match(s, "\\d+").Value;
                if (int.TryParse(numStr, out var n))
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



        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // Minimal RelayCommand implementation
    public class RelayCommand<T> : ICommand
    {
        private readonly System.Action<T?> _execute;
        private readonly System.Predicate<T?>? _canExecute;

        public RelayCommand(System.Action<T?> execute, System.Predicate<T?>? canExecute = null)
        {
            _execute = execute ?? throw new System.ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

        public void Execute(object? parameter) => _execute((T?)parameter);

        public event System.EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, System.EventArgs.Empty);
    }

    public class RelayCommand : RelayCommand<object>
    {
        public RelayCommand(System.Action<object?> execute, System.Predicate<object?>? canExecute = null) : base(execute, canExecute) { }
    }
}
