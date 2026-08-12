using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Clients.Finance;
using FinanceControl.Bff.Email;
using FinanceControl.Bff.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Bff.Tests;

public sealed class BffApplicationFactory : WebApplicationFactory<Program>
{
    public const string DemoEmail = "demo@test.local";
    public const string DemoPassword = "TestPassword123!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "finance-control-bff-tests",
                ["Jwt:Audience"] = "finance-control-bff-tests",
                ["Jwt:Key"] = "integration-test-key-with-at-least-32-bytes-2026",
                ["Jwt:ExpiresMinutes"] = "5",
                ["DemoUser:Email"] = DemoEmail,
                ["DemoUser:Password"] = DemoPassword,
                ["DemoUser:FriendEmail"] = "friend@test.local",
                ["DemoUser:FriendPassword"] = DemoPassword,
                ["ConnectionStrings:BffDatabase"] = "Host=localhost;Database=bff_tests;Username=test;Password=test",
                ["FinanceService:BaseUrl"] = "http://finance-service.test",
                ["FinanceService:TimeoutSeconds"] = "2",
                ["DebtService:BaseUrl"] = "http://debt-service.test",
                ["DebtService:TimeoutSeconds"] = "2",
                ["Email:Host"] = "smtp.test.local",
                ["Email:Port"] = "1025",
                ["Email:FromAddress"] = "no-reply@test.local",
                ["Email:FromName"] = "Finance Control Tests",
                ["Email:Security"] = "None",
                ["Email:FrontendBaseUrl"] = "http://frontend.test"
            });
        });
        builder.ConfigureServices(services =>
        {
            var databaseName = $"bff-tests-{Guid.NewGuid()}";
            services.RemoveAll<DbContextOptions<BffDbContext>>();
            services.RemoveAll<BffDbContext>();
            services.AddDbContext<BffDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            services.RemoveAll<IFinanceServiceClient>();
            services.AddSingleton<IFinanceServiceClient, FakeFinanceServiceClient>();
            services.RemoveAll<IDebtServiceClient>();
            services.AddSingleton<IDebtServiceClient, FakeDebtServiceClient>();
            services.RemoveAll<IApplicationEmailSender>();
            services.AddSingleton<TestEmailSender>();
            services.AddSingleton<IApplicationEmailSender>(provider =>
                provider.GetRequiredService<TestEmailSender>());
        });
    }

    private sealed class FakeFinanceServiceClient : FinanceServiceClientStub
    {
        public override Task<FinanceSummaryResponse> GetMonthlySummaryAsync(
            string? month,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new FinanceSummaryResponse(
                "2026-07",
                5_000.00m,
                3_749.25m,
                1_250.75m));
        }

        public override Task<IReadOnlyList<FinanceCategoryResponse>> GetCategoriesAsync(
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            IReadOnlyList<FinanceCategoryResponse> categories =
            [
                new(1, "FOOD", "Alimentação", true, now, now),
                new(2, "TRANSPORT", "Transporte", true, now, now),
                new(3, "OTHER", "Outros", true, now, now)
            ];
            return Task.FromResult(categories);
        }

        public override Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IncomeResponse>>([]);

        public override Task<IReadOnlyList<ExpenseResponse>> GetExpensesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExpenseResponse>>([
                new ExpenseResponse(
                    Guid.NewGuid(),
                    "Private expense description",
                    1_200m,
                    new DateOnly(2026, 7, 10),
                    "FOOD",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow)
            ]);
    }

    private sealed class FakeDebtServiceClient : DebtServiceClientStub
    {
        public override Task<DebtSummaryResponse> GetSummaryAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new DebtSummaryResponse(
                420.00m,
                180.00m,
                3));
        }

        public override Task<DebtAnalysisContextResponse> GetAnalysisContextAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(new DebtAnalysisContextResponse(
                DateTimeOffset.UtcNow,
                420m,
                180m,
                3,
                1,
                1,
                1,
                [new DebtAnalysisCategoryResponse("FOOD", 420m, 180m, 3)],
                [new DebtAnalysisGroupResponse(
                    Guid.Parse("00000000-0000-0000-0000-000000000123"),
                    "Private family group",
                    420m,
                    180m,
                    3)],
                [new DebtAnalysisDriverResponse(
                    "FOOD",
                    Guid.Parse("00000000-0000-0000-0000-000000000123"),
                    "Private family group",
                    420m,
                    0m,
                    new DateOnly(2026, 7, 1),
                    true)]));

        public override Task<SimplifiedSettlementResponse> GetSimplifiedSettlementsAsync(
            Guid? groupId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SimplifiedSettlementResponse(600m, 4, 2, []));

        public override Task<IReadOnlyList<PersonResponse>> GetPeopleAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PersonResponse>>([]);

        public override Task<IReadOnlyList<DebtResponse>> GetDebtsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DebtResponse>>([]);

        public override Task<IReadOnlyList<FriendResponse>> GetFriendsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FriendResponse>>([]);

        public override Task<IReadOnlyList<FriendshipResponse>> GetIncomingFriendRequestsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FriendshipResponse>>([]);

        public override Task<IReadOnlyList<FriendshipResponse>> GetOutgoingFriendRequestsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FriendshipResponse>>([]);

        public override Task<IReadOnlyList<GroupResponse>> GetGroupsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GroupResponse>>([]);
    }
}
