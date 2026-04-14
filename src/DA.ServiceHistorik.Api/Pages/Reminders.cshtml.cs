using DA.ServiceHistorik.Api.Data;
using DA.ServiceHistorik.Api.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DA.ServiceHistorik.Api.Pages;

public class RemindersModel(AppDbContext db) : PageModel
{
    public string? Plate { get; private set; }
    public List<SentReminder> Reminders { get; private set; } = [];

    public async Task OnGetAsync(string? plate)
    {
        Plate = plate?.ToUpper().Trim();
        if (string.IsNullOrWhiteSpace(Plate)) return;

        Reminders = await db.SentReminders
            .Where(r => r.LicensePlate == Plate)
            .OrderByDescending(r => r.SentAt)
            .ToListAsync();
    }
}
