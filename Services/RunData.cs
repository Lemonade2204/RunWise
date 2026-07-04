using System;

namespace RunWise.Services;

public class RunData
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public double DistanceKm { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RoutePointDto>? RoutePoints { get; set; }
}

public class RoutePointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
