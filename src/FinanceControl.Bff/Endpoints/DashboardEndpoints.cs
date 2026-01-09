using FinanceControl.Bff.Contracts.Dashboard;

namespace FinanceControl.Bff.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/dashboard", () =>
            {
                return Results.Ok(new DashboardResponse()
                {
                    Balance = 1250.75m,
                    TotalIncome = 5000.00m,
                    TotalExpenses = 3749.25m,
                    DebtsSummary = new DebtsSummary
                    {
                        TotalOwed = 420.00m,
                        TotalToReceive = 180.00m
                    }
                });
            })
            .WithName("Dashboard")
            .WithTags("Dashboard");
    }
}