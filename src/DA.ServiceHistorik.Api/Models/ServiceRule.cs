namespace DA.ServiceHistorik.Api.Models;

public class ServiceRule
{
    public string Make { get; set; } = string.Empty;
    public string? Model { get; set; }
    public int IntervalMonths { get; set; }
    public int IntervalKm { get; set; }
}
