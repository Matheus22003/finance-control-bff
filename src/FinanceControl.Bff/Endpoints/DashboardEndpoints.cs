using FinanceControl.Bff.Contracts.Dashboard;

namespace FinanceControl.Bff.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/dashboard", () => Results.Ok(new DashboardResponse
        {
            Balance = 1_250.75m,
            TotalIncome = 5_000.00m,
            TotalExpenses = 3_749.25m,
            DebtsSummary = new DebtsSummary
            {
                TotalOwed = 420.00m,
                TotalToReceive = 180.00m
            }
        }))
            .WithName("GetDashboard")
            .WithTags("Dashboard")
            .Produces<DashboardResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        return group;
    }
}
