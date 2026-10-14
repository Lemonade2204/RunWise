namespace RunWise.Models;


//This is like run records Model 
public class RunSession
{


    
    public class RoutePoint
    {   
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
    //This is like list of route points for continuous tracking while running good for trails
    public List<RoutePoint> RoutePoints { get; set; } = new();


    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    // optional owner id (supabase user id) - helps filter local runs per user
    public string? UserId { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public double DistanceMeters { get; set; }
    // estimated steps for this run (approx steps per km)
    public int Steps { get; set; } = 0;
    // calories burned for this run (integer, stored as kcal)
    public int Calories { get; set; } = 0;
    

    public string DurationFormatted
    {
        get { return Duration.ToString(@"hh\:mm\:ss"); }
    }

    public double DistanceKm
    {
        get { return DistanceMeters / 1000.0; }
    }

    public double DistanceFormatted
    {
        get { return Math.Round(DistanceKm, 2); }
    }

    public double AveragePaceKmh
    {
        get
        {
            return Duration.TotalSeconds > 0 ? (DistanceKm / Duration.TotalHours) : 0;
        }
    }

    public string AveragePaceFormatted
    {
        get { return Math.Round(AveragePaceKmh, 2) + " km/h"; }
    }
}
