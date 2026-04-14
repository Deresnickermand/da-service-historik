namespace DA.ServiceHistorik.Api.Models;

public class SentReminder
{
    public int Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public ReminderType ReminderType { get; set; }
    public DateTime ServiceDate { get; set; }
    public DateTime SentAt { get; set; }
}
