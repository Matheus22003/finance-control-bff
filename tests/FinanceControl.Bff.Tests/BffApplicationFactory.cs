using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
                ["DemoUser:Password"] = DemoPassword
            });
        });
    }
}
