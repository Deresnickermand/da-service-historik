using System.Text;
using Hangfire.Dashboard;

namespace DA.ServiceHistorik.Api.Jobs;

public class HangfireBasicAuthFilter(string username, string password) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var header = httpContext.Request.Headers["Authorization"].FirstOrDefault();

        if (header == null || !header.StartsWith("Basic "))
        {
            Challenge(httpContext);
            return false;
        }

        var encoded = header["Basic ".Length..].Trim();
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        var parts = decoded.Split(':', 2);

        if (parts.Length != 2 || parts[0] != username || parts[1] != password)
        {
            Challenge(httpContext);
            return false;
        }

        return true;
    }

    private static void Challenge(HttpContext context)
    {
        context.Response.StatusCode = 401;
        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Hangfire Dashboard\"";
    }
}
