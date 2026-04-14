namespace DA.ServiceHistorik.Api.Models;

public class ServiceRecord
{
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public DateTime ServiceDate { get; set; }
    public int KmAtService { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
}
