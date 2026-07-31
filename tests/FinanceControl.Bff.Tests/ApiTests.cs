using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceControl.Bff.Contracts.Auth;
using FinanceControl.Bff.Contracts.Dashboard;

namespace FinanceControl.Bff.Tests;

public sealed class ApiTests(BffApplicationFactory factory) : IClassFixture<BffApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_IsPublicAndHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", payload.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Dashboard_WithoutToken_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(401, payload.RootElement.GetProperty("status").GetInt32());
        Assert.True(payload.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Login_WithInvalidInput_ReturnsValidationProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "not-an-email",
            password = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, payload.RootElement.GetProperty("status").GetInt32());
        Assert.True(payload.RootElement.GetProperty("errors").TryGetProperty("email", out _));
        Assert.True(payload.RootElement.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Login_WithWrongCredentials_ReturnsProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = BffApplicationFactory.DemoEmail,
            password = "WrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(401, payload.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Invalid credentials", payload.RootElement.GetProperty("title").GetString());
        Assert.True(payload.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task ValidLogin_AllowsDashboardAccess()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = BffApplicationFactory.DemoEmail,
            password = BffApplicationFactory.DemoPassword
        });

        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.Equal("Bearer", login.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var dashboardResponse = await _client.SendAsync(request);

        dashboardResponse.EnsureSuccessStatusCode();
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.NotNull(dashboard);
        Assert.Equal(1_250.75m, dashboard.Balance);
        Assert.Equal(5_000.00m, dashboard.TotalIncome);
        Assert.Equal(3_749.25m, dashboard.TotalExpenses);
        Assert.Equal(420.00m, dashboard.DebtsSummary.TotalOwed);
        Assert.Equal(180.00m, dashboard.DebtsSummary.TotalToReceive);
    }
}
