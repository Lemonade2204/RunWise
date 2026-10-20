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
                        new Exercise { Title = "Bodyweight Butterfly Stretch", Focus = "Inner thighs and hips", Description = "30s", Tips = "Sit and bring soles together, gently press knees toward floor." },
                        new Exercise { Title = "Bodyweight Squat", Focus = "Quads, glutes and hips", Description = "10 reps", Tips = "Feet shoulder-width, hinge hips back, keep chest up." },
                        new Exercise { Title = "Forward Lunges", Focus = "Quads and hip flexors, alternating", Description = "8 reps", Tips = "Step forward, lower until both knees ~90 degrees." },
                        new Exercise { Title = "Calf Raises", Focus = "Calves and ankles", Description = "15 reps", Tips = "Rise onto toes, control the descent." }
                    };
                }
                else if (lvl == "intermediate")
                {
                    list = new System.Collections.Generic.List<Exercise>
                    {
                        new Exercise { Title = "Bodyweight Butterfly Stretch", Focus = "Inner thighs and hips", Description = "30s", Tips = "Sit and bring soles together, gently press knees toward floor." },
                        new Exercise { Title = "Half Kneeling Hip Hinge", Focus = "Hamstrings and glutes", Description = "8 reps", Tips = "From half-kneeling, hinge at hips to feel posterior chain activation." },
                        new Exercise { Title = "Curtsy Lunges", Focus = "Glutes and outer hips, alternating", Description = "8 reps", Tips = "Step one leg behind and across to target glutes." },
                        new Exercise { Title = "Lateral Lunges", Focus = "Adductors and hips, each side", Description = "8 reps", Tips = "Step to the side and push hips back while keeping chest up." },
                        new Exercise { Title = "Bodyweight Swing Lunge", Focus = "Hips and legs, alternating", Description = "8 reps", Tips = "Explosive alternating swing lunge for dynamic warmup." }
                    };
                }
                else // advanced
                {
                    list = new System.Collections.Generic.List<Exercise>
                    {
                        new Exercise { Title = "Bodyweight Butterfly Stretch", Focus = "Inner thighs and hips", Description = "30s", Tips = "Sit and bring soles together, gently press knees toward floor." },
                        new Exercise { Title = "Deadlift Bounces Stretch", Focus = "Hamstrings and glutes", Description = "10 reps", Tips = "Soft bouncy movements to wake up hamstrings and glutes." },
                        new Exercise { Title = "Single Leg Romanian Deadlifts", Focus = "Hamstrings and balance, each leg", Description = "8 each leg", Tips = "Slow hinge on single leg to load posterior chain." },
                        new Exercise { Title = "Forward Lunge", Focus = "Quads and hip flexors, alternating", Description = "10 reps  ", Tips = "Deep controlled forward lunges." },
                        new Exercise { Title = "Jump Squats", Focus = "Quads, glutes and calves", Description = "10 reps", Tips = "Explosive plyometric squats; land softly." }
                    };
                }
            }
            else
            {
                // Post-run cool-down holds per level. "Ns each leg" holds switch sides halfway in the session.
                var lvl = (level ?? SelectedLevel ?? "beginner").Trim().ToLowerInvariant();
                if (lvl == "beginner" || lvl == "novice")
                {
                    list = new System.Collections.Generic.List<Exercise>
                    {
                        new Exercise { Title = "Standing Quad Stretch", Focus = "Quads and hip flexors", Description = "30s each leg", Tips = "Pull your heel toward your glutes, keep knees together and hips level." },
                        new Exercise { Title = "Standing Calf Stretch", Focus = "Calves and Achilles", Description = "30s each leg", Tips = "Hands on a wall, back leg straight, press the back heel into the floor." },
                        new Exercise { Title = "Standing Hamstring Stretch", Focus = "Hamstrings", Description = "30s each leg", Tips = "Heel on a low step, leg straight, hinge forward with a long spine." },
                        new Exercise { Title = "Forward Fold", Focus = "Hamstrings and lower back", Description = "30s", Tips = "Soft knees, let your head and arms hang heavy. Breathe slowly." }
                    };
                }
                else if (lvl == "intermediate")
                {
                    list = new System.Collections.Generic.List<Exercise>
                    {
                        new Exercise { Title = "Standing Quad Stretch", Focus = "Quads and hip flexors", Description = "30s each leg", Tips = "Pull your heel toward your glutes, keep knees together and hips level." },
                        new Exercise { Title = "Kneeling Hip Flexor Stretch", Focus = "Hip flexors and quads", Description = "30s each leg", Tips = "Kneel in a lunge, tuck your pelvis and shift hips gently forward." },
                        new Exercise { Title = "Figure-Four Stretch", Focus = "Glutes and outer hips", Description = "30s each leg", Tips = "Lie on your back, cross ankle over the opposite knee and pull the thigh in." },
                        new Exercise { Title = "Seated Hamstring Stretch", Focus = "Hamstrings and calves", Description = "30s each leg", Tips = "Sit with one leg extended and reach toward your toes without rounding." },
                        new Exercise { Title = "Standing Calf Stretch", Focus = "Calves and Achilles", Description = "30s each leg", Tips = "Hands on a wall, back leg straight, press the back heel into the floor." }
                    };
                }
                else // advanced
                {
                    list = new System.Collections.Generic.List<Exercise>
                    {
                        new Exercise { Title = "Kneeling Hip Flexor Stretch", Focus = "Hip flexors and quads", Description = "45s each leg", Tips = "Kneel in a lunge, tuck your pelvis and shift hips gently forward." },
                        new Exercise { Title = "Pigeon Pose", Focus = "Glutes and piriformis", Description = "45s each leg", Tips = "From all fours, bring one shin forward and sink your hips low and square." },
                        new Exercise { Title = "Half Splits", Focus = "Hamstrings", Description = "45s each leg", Tips = "From a low lunge, shift hips back and straighten the front leg." },
                        new Exercise { Title = "Downward Dog", Focus = "Calves and hamstrings", Description = "45s", Tips = "Hips high, pedal your heels toward the floor one at a time." },
                        new Exercise { Title = "Supine Spinal Twist", Focus = "Lower back and outer hips", Description = "30s each side", Tips = "Lie on your back, drop bent knees to one side, keep shoulders down." },
                        new Exercise { Title = "Child's Pose", Focus = "Lower back and hips", Description = "60s", Tips = "Knees wide, sit back on your heels, arms long. Slow, deep breaths." }
                    };
                }
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
