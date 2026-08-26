using System;
namespace RunWise.Models;

public class Profile
{
    public string? Id { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public double? WeightKg { get; set; }
    public double? HeightCm { get; set; }
    public double? WeeklyAverageDistanceKm { get; set; }
    public int? WeeklyAverageTimeSeconds { get; set; }
    public double? AllTimeDistanceKm { get; set; }
    public int? AllTimeTimeSeconds { get; set; }
}
