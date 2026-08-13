using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FinanceControl.Bff.Clients;
using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Clients.Finance;
using FinanceControl.Bff.Contracts.Auth;
using FinanceControl.Bff.Contracts.Ai;
using FinanceControl.Bff.Contracts.Dashboard;
using FinanceControl.Bff.Contracts.Users;
using FinanceControl.Bff.Notifications;
using FinanceControl.Bff.Observability;
using FinanceControl.Bff.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace FinanceControl.Bff.Tests;

public sealed class ApiTests(BffApplicationFactory factory) : IClassFixture<BffApplicationFactory>
{
    private readonly BffApplicationFactory _factory = factory;
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
        var correlationId = Guid.Parse("202cc9fa-bf56-405a-a0f1-4961344224b8").ToString("D");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(correlationId, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(401, payload.RootElement.GetProperty("status").GetInt32());
        Assert.True(payload.RootElement.TryGetProperty("traceId", out _));
        Assert.Equal(correlationId, payload.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task InvalidCorrelationId_IsReplacedWithGeneratedIdentifier()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, "not-a-valid-id");

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var generatedValue = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.True(Guid.TryParse(generatedValue, out _));
        Assert.NotEqual("not-a-valid-id", generatedValue);
    }

    [Fact]
    public async Task AiAnalysis_RequiresJwtAndReturnsSanitizedInsights()
    {
        var unauthorized = await _client.PostAsJsonAsync(
            "/api/v1/ai/analyze",
            new AiAnalysisRequest("2026-07"));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var token = await AuthenticateAsync(_client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/analyze")
        {
            Content = JsonContent.Create(new AiAnalysisRequest("2026-07"))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var analysis = JsonSerializer.Deserialize<AiAnalysisResponse>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(analysis);
        Assert.Equal("mock", analysis.Provider);
        Assert.Equal(420m, analysis.Metrics.TotalOwed);
        Assert.Contains(analysis.DebtInsights, insight => insight.Title == "Principal origem das dívidas");
        Assert.DoesNotContain("Private family group", body);
        Assert.DoesNotContain("Private expense description", body);
    }

    [Fact]
    public async Task AiQuestion_RequiresJwtAndAnswersFromFinancialContext()
    {
        var unauthorized = await _client.PostAsJsonAsync(
            "/api/v1/ai/ask",
            new AiQuestionRequest("De onde acumulei minhas dívidas?"));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var token = await AuthenticateAsync(_client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/ask")
        {
            Content = JsonContent.Create(new AiQuestionRequest("De onde acumulei minhas dívidas?"))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var answer = await response.Content.ReadFromJsonAsync<AiQuestionResponse>();
        Assert.NotNull(answer);
        Assert.Equal("deterministic", answer.Provider);
        Assert.NotEmpty(answer.Answer);
        Assert.NotEmpty(answer.SuggestedQuestions);
    }

    [Fact]
    public async Task NotificationHub_RequiresJwtAndAcceptsAuthenticatedNegotiation()
    {
        var unauthorized = await _client.PostAsync(
            "/api/v1/notifications/hub/negotiate?negotiateVersion=1",
            null);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var token = await AuthenticateAsync(_client);
        var authenticated = await _client.PostAsync(
            $"/api/v1/notifications/hub/negotiate?negotiateVersion=1&access_token={token}",
            null);

        authenticated.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await authenticated.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty("connectionToken", out _));
        Assert.True(payload.RootElement.TryGetProperty("availableTransports", out _));
    }

    [Fact]
    public async Task Notifications_ArePersistentUserScopedAndCanBeMarkedAsRead()
    {
        var uniqueTitle = $"Notification {Guid.NewGuid()}";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();
            await notifications.PublishAsync(
                [BffDatabaseInitializer.FriendUserId],
                NotificationType.PaymentRecorded,
                uniqueTitle,
                "A payment needs confirmation.",
                "/debts",
                CancellationToken.None);
        }

        var friendToken = await AuthenticateAsync(_client, "friend@test.local");
        using var friendRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications");
        friendRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", friendToken);
        var friendResponse = await _client.SendAsync(friendRequest);
        friendResponse.EnsureSuccessStatusCode();
        var friendNotifications = await friendResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<NotificationResponse>>();
        var created = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyList<NotificationResponse>>(friendNotifications),
            notification => notification.Title == uniqueTitle);
        Assert.False(created.IsRead);
        Assert.Equal("PAYMENT_RECORDED", created.Type);

        using var markReadRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/notifications/{created.Id}/read");
        markReadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", friendToken);
        var markReadResponse = await _client.SendAsync(markReadRequest);
        markReadResponse.EnsureSuccessStatusCode();
        var marked = await markReadResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.True(marked?.IsRead);
        Assert.NotNull(marked?.ReadAt);

        var demoToken = await AuthenticateAsync(_client);
        using var demoRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications");
        demoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", demoToken);
        var demoResponse = await _client.SendAsync(demoRequest);
        var demoNotifications = await demoResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<NotificationResponse>>();
        Assert.DoesNotContain(
            Assert.IsAssignableFrom<IReadOnlyList<NotificationResponse>>(demoNotifications),
            notification => notification.Title == uniqueTitle);
    }

    [Fact]
    public async Task NotificationSync_CreatesGoalAndBudgetAlertsOnlyOnce()
    {
        using var alertFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFinanceServiceClient>();
                services.AddSingleton<IFinanceServiceClient, AlertFinanceServiceClient>();
            });
        });
        using var client = alertFactory.CreateClient();
        var token = await AuthenticateAsync(client);

        async Task<NotificationSyncResponse?> SyncAsync()
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/notifications/sync");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NotificationSyncResponse>();
        }

        var firstSync = await SyncAsync();
        var secondSync = await SyncAsync();

        Assert.Equal(2, firstSync?.CreatedCount);
        Assert.Equal(0, secondSync?.CreatedCount);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResponse = await client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var notifications = await listResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<NotificationResponse>>();
        Assert.Single(notifications!, notification => notification.Type == "GOAL_DUE_SOON");
        var budgetAlert = Assert.Single(
            notifications!,
            notification => notification.Type == "BUDGET_WARNING");
        Assert.Contains("Assinaturas", budgetAlert.Title);
    }

    [Fact]
    public async Task FinanceFacade_WithoutToken_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/finance/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task FinanceFacade_WithToken_ReturnsServiceContract()
    {
        var accessToken = await AuthenticateAsync(_client);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/finance/categories");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var categories = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<FinanceCategoryResponse>>();
        Assert.Equal(["FOOD", "TRANSPORT", "OTHER"], categories?.Select(item => item.Code));
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
        Assert.Equal(3, dashboard.DebtsSummary.OpenDebtsCount);
    }

    [Fact]
    public async Task Register_RequiresEmailConfirmationBeforeLogin()
    {
        var email = $"new-{Guid.NewGuid():N}@test.local";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            displayName = "New user",
            email,
            password = BffApplicationFactory.DemoPassword
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var registration = await response.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.Equal(email, registration?.Email);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));

        var blockedLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = BffApplicationFactory.DemoPassword
        });
        Assert.Equal(HttpStatusCode.Forbidden, blockedLogin.StatusCode);

        var emailSender = _factory.Services.GetRequiredService<TestEmailSender>();
        var confirmationEmail = Assert.Single(emailSender.Messages, message =>
            message.RecipientAddress == email && message.Subject.Contains("Confirme", StringComparison.Ordinal));
        var (userId, token) = ExtractSecurityLink(confirmationEmail.HtmlBody);
        var confirmation = await _client.PostAsJsonAsync("/api/v1/auth/confirm-email", new
        {
            userId,
            token
        });
        Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = BffApplicationFactory.DemoPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        Assert.Contains("finance_control_refresh=", loginResponse.Headers.GetValues("Set-Cookie").Single());
    }

    [Fact]
    public async Task ForgotPassword_SendsResetLinkAndNewPasswordCanBeUsed()
    {
        var email = $"reset-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            displayName = "Reset user",
            email,
            password = BffApplicationFactory.DemoPassword
        });
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);

        var emailSender = _factory.Services.GetRequiredService<TestEmailSender>();
        var confirmationEmail = emailSender.Messages.Last(message =>
            message.RecipientAddress == email && message.Subject.Contains("Confirme", StringComparison.Ordinal));
        var confirmationLink = ExtractSecurityLink(confirmationEmail.HtmlBody);
        var confirmation = await _client.PostAsJsonAsync("/api/v1/auth/confirm-email", new
        {
            userId = confirmationLink.UserId,
            token = confirmationLink.Token
        });
        Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            email
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var resetEmail = emailSender.Messages.Last(message =>
            message.RecipientAddress == email &&
            message.Subject.Contains("Redefina", StringComparison.Ordinal));
        var (userId, token) = ExtractSecurityLink(resetEmail.HtmlBody);
        var newPassword = "NewTestPassword456!";
        var reset = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            userId,
            token,
            newPassword
        });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = newPassword
        });
        login.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ChangePassword_RevokesExistingSessions()
    {
        var email = $"change-{Guid.NewGuid():N}@test.local";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            displayName = "Change password user",
            email,
            password = BffApplicationFactory.DemoPassword
        });
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);

        var emailSender = _factory.Services.GetRequiredService<TestEmailSender>();
        var confirmationEmail = emailSender.Messages.Last(message =>
            message.RecipientAddress == email && message.Subject.Contains("Confirme", StringComparison.Ordinal));
        var confirmationLink = ExtractSecurityLink(confirmationEmail.HtmlBody);
        var confirmation = await _client.PostAsJsonAsync("/api/v1/auth/confirm-email", new
        {
            userId = confirmationLink.UserId,
            token = confirmationLink.Token
        });
        Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);

        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = BffApplicationFactory.DemoPassword
        });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<LoginResponse>();
        var refreshCookie = login.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        using var changeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword = BffApplicationFactory.DemoPassword,
                newPassword = "ChangedPassword789!"
            })
        };
        changeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var changed = await client.SendAsync(changeRequest);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { })
        };
        refreshRequest.Headers.Add("Cookie", refreshCookie);
        var refresh = await client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        var oldPasswordLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = BffApplicationFactory.DemoPassword
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);
        var newPasswordLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "ChangedPassword789!"
        });
        newPasswordLogin.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AccountProfile_SupportsPreferencesAvatarEmailChangeAndExport()
    {
        var email = $"profile-{Guid.NewGuid():N}@test.local";
        var accessToken = await RegisterConfirmAndLoginAsync(email);

        using var profileRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/me/profile")
        {
            Content = JsonContent.Create(new { displayName = "Updated profile" })
        };
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var profileResponse = await _client.SendAsync(profileRequest);
        profileResponse.EnsureSuccessStatusCode();
        var profile = await profileResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal("Updated profile", profile?.DisplayName);

        using var preferencesRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/me/preferences")
        {
            Content = JsonContent.Create(new
            {
                theme = "dark",
                emailNotificationsEnabled = false,
                pushNotificationsEnabled = true
            })
        };
        preferencesRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var preferencesResponse = await _client.SendAsync(preferencesRequest);
        preferencesResponse.EnsureSuccessStatusCode();
        var preferences = await preferencesResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal("dark", preferences?.Preferences.Theme);
        Assert.False(preferences?.Preferences.EmailNotificationsEnabled);

        using var avatarForm = new MultipartFormDataContent();
        var avatarBytes = new ByteArrayContent([0x89, 0x50, 0x4e, 0x47]);
        avatarBytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        avatarForm.Add(avatarBytes, "file", "avatar.png");
        using var avatarRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/me/avatar")
        {
            Content = avatarForm
        };
        avatarRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var avatarResponse = await _client.SendAsync(avatarRequest);
        avatarResponse.EnsureSuccessStatusCode();

        using var getAvatarRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me/avatar");
        getAvatarRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var getAvatarResponse = await _client.SendAsync(getAvatarRequest);
        getAvatarResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/png", getAvatarResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal([0x89, 0x50, 0x4e, 0x47], await getAvatarResponse.Content.ReadAsByteArrayAsync());

        using var exportRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users/me/export")
        {
            Content = JsonContent.Create(new { password = BffApplicationFactory.DemoPassword })
        };
        exportRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var exportResponse = await _client.SendAsync(exportRequest);
        exportResponse.EnsureSuccessStatusCode();
        Assert.Equal("application/json", exportResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(exportResponse.Content.Headers.ContentDisposition?.FileNameStar);

        var newEmail = $"changed-{Guid.NewGuid():N}@test.local";
        using var emailRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users/me/email-change")
        {
            Content = JsonContent.Create(new
            {
                newEmail,
                password = BffApplicationFactory.DemoPassword
            })
        };
        emailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var emailResponse = await _client.SendAsync(emailRequest);
        Assert.Equal(HttpStatusCode.Accepted, emailResponse.StatusCode);

        var emailSender = _factory.Services.GetRequiredService<TestEmailSender>();
        var changeEmail = emailSender.Messages.Last(message => message.RecipientAddress == newEmail);
        var link = ExtractSecurityLink(changeEmail.HtmlBody);
        var parsedLink = new Uri(WebUtility.HtmlDecode(
            Regex.Match(changeEmail.HtmlBody, "href=\"(?<link>[^\"]+)\"").Groups["link"].Value));
        var changeQuery = QueryHelpers.ParseQuery(parsedLink.Query);
        var confirmation = await _client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", new
        {
            userId = link.UserId,
            newEmail = changeQuery["newEmail"].Single(),
            token = link.Token
        });
        Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);

        var oldLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = BffApplicationFactory.DemoPassword
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = newEmail,
            password = BffApplicationFactory.DemoPassword
        });
        newLogin.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Refresh_RotatesTokenAndRejectsReusedTokenFamily()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = BffApplicationFactory.DemoEmail,
            password = BffApplicationFactory.DemoPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        var originalCookie = loginResponse.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { })
        };
        refreshRequest.Headers.Add("Cookie", originalCookie);
        var refreshResponse = await client.SendAsync(refreshRequest);
        refreshResponse.EnsureSuccessStatusCode();
        var rotatedCookie = refreshResponse.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        Assert.NotEqual(originalCookie, rotatedCookie);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { })
        };
        replayRequest.Headers.Add("Cookie", originalCookie);
        var replayResponse = await client.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        using var familyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { })
        };
        familyRequest.Headers.Add("Cookie", rotatedCookie);
        var familyResponse = await client.SendAsync(familyRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, familyResponse.StatusCode);
    }

    [Theory]
    [InlineData(DebtServiceFailure.InvalidResponse, HttpStatusCode.BadGateway, "Invalid Debt Service response")]
    [InlineData(DebtServiceFailure.Unavailable, HttpStatusCode.ServiceUnavailable, "Debt Service unavailable")]
    [InlineData(DebtServiceFailure.Timeout, HttpStatusCode.GatewayTimeout, "Debt Service timeout")]
    public async Task Dashboard_WhenDebtServiceFails_ReturnsProblemDetails(
        DebtServiceFailure failure,
        HttpStatusCode expectedStatusCode,
        string expectedTitle)
    {
        using var failureFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDebtServiceClient>();
                services.AddSingleton<IDebtServiceClient>(new FailingDebtServiceClient(failure));
            });
        });
        using var client = failureFactory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = BffApplicationFactory.DemoEmail,
            password = BffApplicationFactory.DemoPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expectedStatusCode, payload.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, payload.RootElement.GetProperty("title").GetString());
        Assert.True(payload.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task DebtFacade_WhenServiceRejectsRequest_PreservesProblemDetails()
    {
        using var failureFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDebtServiceClient>();
                services.AddSingleton<IDebtServiceClient>(new RejectedDebtServiceClient());
            });
        });
        using var client = failureFactory.CreateClient();
        var accessToken = await AuthenticateAsync(client);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/debts/{Guid.Empty}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, payload.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Debt not found", payload.RootElement.GetProperty("title").GetString());
        Assert.Equal("The requested debt does not exist.", payload.RootElement.GetProperty("detail").GetString());
        Assert.Equal($"/api/v1/debts/{Guid.Empty}", payload.RootElement.GetProperty("instance").GetString());
        Assert.True(payload.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task AccountDeletion_WhenEligible_DeletesUpstreamDataAndIdentity()
    {
        var debtClient = new TrackingDebtServiceClient(canDelete: true);
        var financeClient = new TrackingFinanceServiceClient();
        using var deletionFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDebtServiceClient>();
                services.AddSingleton<IDebtServiceClient>(debtClient);
                services.RemoveAll<IFinanceServiceClient>();
                services.AddSingleton<IFinanceServiceClient>(financeClient);
            });
        });
        using var client = deletionFactory.CreateClient();
        var accessToken = await AuthenticateAsync(client);

        using var eligibilityRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/users/me/deletion-eligibility");
        eligibilityRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var eligibilityResponse = await client.SendAsync(eligibilityRequest);
        eligibilityResponse.EnsureSuccessStatusCode();
        var eligibility = await eligibilityResponse.Content
            .ReadFromJsonAsync<FinanceControl.Bff.Contracts.Users.AccountDeletionEligibilityResponse>();
        Assert.True(eligibility?.CanDelete);

        using var deletionRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/me")
        {
            Content = JsonContent.Create(new
            {
                password = BffApplicationFactory.DemoPassword,
                confirmation = "EXCLUIR"
            })
        };
        deletionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var deletionResponse = await client.SendAsync(deletionRequest);

        Assert.Equal(HttpStatusCode.NoContent, deletionResponse.StatusCode);
        Assert.True(debtClient.DeleteCalled);
        Assert.True(financeClient.DeleteCalled);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = BffApplicationFactory.DemoEmail,
                password = BffApplicationFactory.DemoPassword
            })).StatusCode);
    }

    [Fact]
    public async Task AccountDeletion_WhenDebtHasBlockers_ReturnsConflictWithoutDeleting()
    {
        var debtClient = new TrackingDebtServiceClient(canDelete: false);
        var financeClient = new TrackingFinanceServiceClient();
        using var deletionFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDebtServiceClient>();
                services.AddSingleton<IDebtServiceClient>(debtClient);
                services.RemoveAll<IFinanceServiceClient>();
                services.AddSingleton<IFinanceServiceClient>(financeClient);
            });
        });
        using var client = deletionFactory.CreateClient();
        var accessToken = await AuthenticateAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/me")
        {
            Content = JsonContent.Create(new
            {
                password = BffApplicationFactory.DemoPassword,
                confirmation = "EXCLUIR"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Account deletion blocked", payload.RootElement.GetProperty("title").GetString());
        Assert.Equal(1, payload.RootElement.GetProperty("openDebtsCount").GetInt32());
        Assert.False(debtClient.DeleteCalled);
        Assert.False(financeClient.DeleteCalled);
    }

    private static async Task<string> AuthenticateAsync(
        HttpClient client,
        string email = BffApplicationFactory.DemoEmail)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = BffApplicationFactory.DemoPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        return Assert.IsType<string>(login?.AccessToken);
    }

    private async Task<string> RegisterConfirmAndLoginAsync(string email)
    {
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            displayName = "Profile user",
            email,
            password = BffApplicationFactory.DemoPassword
        });
        Assert.Equal(HttpStatusCode.Accepted, register.StatusCode);
        var emailSender = _factory.Services.GetRequiredService<TestEmailSender>();
        var confirmationEmail = emailSender.Messages.Last(message =>
            message.RecipientAddress == email && message.Subject.Contains("Confirme", StringComparison.Ordinal));
        var confirmationLink = ExtractSecurityLink(confirmationEmail.HtmlBody);
        var confirmation = await _client.PostAsJsonAsync("/api/v1/auth/confirm-email", new
        {
            userId = confirmationLink.UserId,
            token = confirmationLink.Token
        });
        Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = BffApplicationFactory.DemoPassword
        });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<LoginResponse>();
        return Assert.IsType<string>(session?.AccessToken);
    }

    private static (Guid UserId, string Token) ExtractSecurityLink(string htmlBody)
    {
        var match = Regex.Match(htmlBody, "href=\"(?<link>[^\"]+)\"");
        Assert.True(match.Success);
        var link = new Uri(WebUtility.HtmlDecode(match.Groups["link"].Value));
        var query = QueryHelpers.ParseQuery(link.Query);
        return (Guid.Parse(query["userId"].Single()!), query["token"].Single()!);
    }

    private sealed class FailingDebtServiceClient(DebtServiceFailure failure) : DebtServiceClientStub
    {
        public override Task<DebtSummaryResponse> GetSummaryAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromException<DebtSummaryResponse>(new DebtServiceException(
                failure,
                "Debt Service test failure."));
        }
    }

    private sealed class RejectedDebtServiceClient : DebtServiceClientStub
    {
        public override Task<DebtResponse> GetDebtAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            return Task.FromException<DebtResponse>(new DebtServiceException(
                DebtServiceFailure.Rejected,
                "Debt Service returned HTTP 404.",
                upstreamStatusCode: 404,
                upstreamProblem: new UpstreamProblemDetails(
                    "Debt not found",
                    "The requested debt does not exist.",
                    new Dictionary<string, JsonElement>())));
        }
    }

    private sealed class TrackingDebtServiceClient(bool canDelete) : DebtServiceClientStub
    {
        public bool DeleteCalled { get; private set; }

        public override Task<FinanceControl.Bff.Clients.Debt.AccountDeletionEligibilityResponse>
            GetAccountDeletionEligibilityAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(canDelete
                ? new FinanceControl.Bff.Clients.Debt.AccountDeletionEligibilityResponse(
                    true, 0, 0, 0, 0, [])
                : new FinanceControl.Bff.Clients.Debt.AccountDeletionEligibilityResponse(
                    false,
                    1,
                    0,
                    0,
                    0,
                    ["Quite ou remova todas as dívidas abertas das quais você participa."]));

        public override Task DeleteAccountDataAsync(CancellationToken cancellationToken)
        {
            DeleteCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingFinanceServiceClient : FinanceServiceClientStub
    {
        public bool DeleteCalled { get; private set; }

        public override Task DeleteAccountDataAsync(CancellationToken cancellationToken)
        {
            DeleteCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class AlertFinanceServiceClient : FinanceServiceClientStub
    {
        public override Task<IReadOnlyList<FinancialGoalResponse>> GetFinancialGoalsAsync(
            CancellationToken cancellationToken)
        {
            var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
            return Task.FromResult<IReadOnlyList<FinancialGoalResponse>>([
                new FinancialGoalResponse(
                    Guid.Parse("00000000-0000-0000-0000-000000000901"),
                    "Reserva de emergência",
                    10_000m,
                    6_000m,
                    4_000m,
                    60m,
                    targetDate,
                    "ACTIVE",
                    4_000m,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow)
            ]);
        }

        public override Task<MonthlyBudgetResponse> GetMonthlyBudgetAsync(
            string? month,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MonthlyBudgetResponse(
                month ?? DateTime.UtcNow.ToString("yyyy-MM"),
                1_000m,
                850m,
                150m,
                [new BudgetCategoryResponse(
                    "CUSTOM_SUBSCRIPTIONS",
                    "Assinaturas",
                    1_000m,
                    850m,
                    150m,
                    85m)]));
    }
}
