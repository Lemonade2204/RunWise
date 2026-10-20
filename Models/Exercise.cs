namespace RunWise.Models
{
    public enum Difficulty
    {
        Easy,
        Medium,
        Hard
    }

    public class Exercise
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Difficulty Difficulty { get; set; } = Difficulty.Easy;
        // Optional short tips or cues
        public string Tips { get; set; } = string.Empty;
        // Placeholder instruction video URL (optional). Populated with a local/sample URL for offline/demo use.
        public string? InstructionVideoUrl { get; set; }
        // Optional duration in seconds for timed exercises
        public int? DurationSeconds { get; set; }
        // Short description of the body area a move targets (e.g. "Hips and hamstrings")
        public string Focus { get; set; } = string.Empty;
    }
}
