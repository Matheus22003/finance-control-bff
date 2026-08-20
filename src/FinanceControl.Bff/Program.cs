using System.Diagnostics;
using System.Text;
using FinanceControl.Bff.Auth;
using FinanceControl.Bff.Ai;
using FinanceControl.Bff.Clients;
using FinanceControl.Bff.Clients.Debt;
using FinanceControl.Bff.Clients.Finance;
using FinanceControl.Bff.Endpoints;
using FinanceControl.Bff.Email;
using FinanceControl.Bff.Errors;
using FinanceControl.Bff.OpenApi;
using FinanceControl.Bff.Notifications;
using FinanceControl.Bff.Observability;
using FinanceControl.Bff.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        context.ProblemDetails.Extensions["correlationId"] =
            CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);
    };
});
builder.Services.AddExceptionHandler<FinanceServiceExceptionHandler>();
builder.Services.AddExceptionHandler<DebtServiceExceptionHandler>();
builder.Services.AddExceptionHandler<AiProviderExceptionHandler>();

if (!builder.Environment.IsEnvironment("Testing"))
{
    var bffDatabaseConnection = builder.Configuration.GetConnectionString("BffDatabase")
        ?? throw new InvalidOperationException("ConnectionStrings:BffDatabase must be configured.");
    builder.Services.AddDbContext<BffDbContext>(options => options.UseNpgsql(bffDatabaseConnection));
}
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
    })
    .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
    .AddEntityFrameworkStores<BffDbContext>()
    .AddTokenProvider<Microsoft.AspNetCore.Identity.DataProtectorTokenProvider<ApplicationUser>>(
        Microsoft.AspNetCore.Identity.TokenOptions.DefaultProvider);
builder.Services.AddScoped<BffDatabaseInitializer>();

builder.Services.Configure<Microsoft.AspNetCore.Identity.DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(2);
});

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("FinanceControl.Bff")
    .PersistKeysToDbContext<BffDbContext>();

builder.Services
    .AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options =>
        string.Equals(options.Provider, EmailOptions.SmtpProvider, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(options.Provider, EmailOptions.BrevoProvider, StringComparison.OrdinalIgnoreCase),
        "Email:Provider must be Smtp or Brevo.")
    .Validate(options =>
        options.UsesBrevo ||
        (!string.IsNullOrWhiteSpace(options.Host) &&
         ((string.IsNullOrWhiteSpace(options.UserName) && string.IsNullOrWhiteSpace(options.Password)) ||
          (!string.IsNullOrWhiteSpace(options.UserName) && !string.IsNullOrWhiteSpace(options.Password)))),
        "SMTP requires a host and both credentials must be empty or configured.")
    .Validate(options => !options.UsesBrevo || !string.IsNullOrWhiteSpace(options.ApiKey),
        "Brevo requires Email:ApiKey.")
    .ValidateOnStart();
builder.Services.AddSingleton<EmailLinkFactory>();
builder.Services.AddTransient<SmtpEmailSender>();
builder.Services.AddHttpClient<BrevoEmailSender>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>()
        .Value;
    var baseUrl = options.ApiBaseUrl.EndsWith("/", StringComparison.Ordinal)
        ? options.ApiBaseUrl
        : $"{options.ApiBaseUrl}/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddTransient<IApplicationEmailSender>(serviceProvider =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>()
        .Value;
    return options.UsesBrevo
        ? serviceProvider.GetRequiredService<BrevoEmailSender>()
        : serviceProvider.GetRequiredService<SmtpEmailSender>();
});

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => Encoding.UTF8.GetByteCount(options.Key) >= 32,
        "Jwt:Key must contain at least 32 bytes.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AuthSessionOptions>()
    .Bind(builder.Configuration.GetSection(AuthSessionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<DemoUserOptions>()
    .Bind(builder.Configuration.GetSection(DemoUserOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options =>
        (string.IsNullOrWhiteSpace(options.FriendEmail) &&
         string.IsNullOrWhiteSpace(options.FriendPassword)) ||
        (!string.IsNullOrWhiteSpace(options.FriendEmail) &&
         !string.IsNullOrWhiteSpace(options.FriendPassword) &&
         new System.ComponentModel.DataAnnotations.EmailAddressAttribute()
             .IsValid(options.FriendEmail) &&
         options.FriendPassword.Length >= 8),
        "DemoUser friend credentials must both be empty or contain a valid email and password.")
    .ValidateOnStart();

builder.Services
    .AddOptions<FinanceServiceOptions>()
    .Bind(builder.Configuration.GetSection(FinanceServiceOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
        "FinanceService:BaseUrl must be an absolute HTTP or HTTPS URL.")
    .ValidateOnStart();

builder.Services
    .AddOptions<DebtServiceOptions>()
    .Bind(builder.Configuration.GetSection(DebtServiceOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
        "DebtService:BaseUrl must be an absolute HTTP or HTTPS URL.")
    .ValidateOnStart();

builder.Services
    .AddOptions<AiProviderOptions>()
    .Bind(builder.Configuration.GetSection(AiProviderOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options =>
        options.Provider.Equals(AiProviderOptions.MockProvider, StringComparison.OrdinalIgnoreCase) ||
        options.Provider.Equals(
            AiProviderOptions.OpenAiCompatibleProvider,
            StringComparison.OrdinalIgnoreCase),
        "Ai:Provider must be Mock or OpenAiCompatible.")
    .Validate(options =>
        !options.UsesExternalProvider ||
        (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
         uri.Scheme == Uri.UriSchemeHttps &&
         !string.IsNullOrWhiteSpace(options.ApiKey) &&
         !string.IsNullOrWhiteSpace(options.Model)),
        "External AI requires an HTTPS Ai:BaseUrl, Ai:ApiKey and Ai:Model.")
    .ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
    {
        var jwtOptions = jwtOptionsAccessor.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "email"
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments(
                        "/api/v1/notifications/hub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await JwtProblemDetailsWriter.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "Authentication required",
                    "A valid Bearer token is required to access this resource.");
            },
            OnForbidden = context => JwtProblemDetailsWriter.WriteAsync(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                "Access forbidden",
                "The authenticated user is not allowed to access this resource.")
        };
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NotificationUserIdProvider>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<BudgetAlertService>();
builder.Services.AddScoped<GoalAlertService>();
builder.Services.AddScoped<NotificationAlertSyncService>();
builder.Services.AddScoped<AiAnalysisService>();
builder.Services.AddScoped<AiQuestionService>();
builder.Services.AddSingleton<MockAiAnalysisProvider>();
builder.Services
    .AddHttpClient<OpenAiCompatibleAnalysisProvider>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AiProviderOptions>>()
            .Value;
        var baseUrl = options.BaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? options.BaseUrl
            : $"{options.BaseUrl}/";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });
builder.Services.AddTransient<IAiAnalysisProvider>(serviceProvider =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<AiProviderOptions>>()
        .Value;
    return options.UsesExternalProvider
        ? serviceProvider.GetRequiredService<OpenAiCompatibleAnalysisProvider>()
        : serviceProvider.GetRequiredService<MockAiAnalysisProvider>();
});
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<RefreshTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthenticatedUserHandler>();
builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services
    .AddHttpClient<IFinanceServiceClient, FinanceServiceClient>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<FinanceServiceOptions>>()
            .Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .AddHttpMessageHandler<AuthenticatedUserHandler>();
builder.Services
    .AddHttpClient<IDebtServiceClient, DebtServiceClient>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<DebtServiceOptions>>()
            .Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    })
    .AddHttpMessageHandler<CorrelationIdHandler>()
    .AddHttpMessageHandler<AuthenticatedUserHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(options =>
{
    var sensitivePermitLimit = builder.Environment.IsEnvironment("Testing")
        ? 1_000
        : builder.Configuration.GetValue("RateLimiting:AuthSensitivePermitLimit", 10);
    if (sensitivePermitLimit < 1)
    {
        throw new InvalidOperationException(
            "RateLimiting:AuthSensitivePermitLimit must be greater than zero.");
    }

    var refreshPermitLimit = builder.Environment.IsEnvironment("Testing") ? 1_000 : 30;
    var aiAnalysisPermitLimit = builder.Environment.IsEnvironment("Testing") ? 1_000 : 5;
    var aiQuestionPermitLimit = builder.Environment.IsEnvironment("Testing") ? 1_000 : 15;
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth-sensitive", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = sensitivePermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("auth-refresh", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = refreshPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("ai-analysis", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ??
        context.Connection.RemoteIpAddress?.ToString() ??
        "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = aiAnalysisPermitLimit,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("ai-question", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ??
        context.Connection.RemoteIpAddress?.ToString() ??
        "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = aiQuestionPermitLimit,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.OnRejected = async (context, _) =>
    {
        await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests",
            detail: "Too many requests were made. Try again shortly.")
            .ExecuteAsync(context.HttpContext);
    };
});

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<BearerSecurityRequirementTransformer>();
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Finance Control - BFF",
            Version = "v1",
            Description = "Authentication and aggregation entry point for Finance Control clients."
        };

        return Task.CompletedTask;
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<BffDatabaseInitializer>().InitializeAsync();
}

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    if (app.Environment.IsDevelopment())
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/swagger") &&
                context.User.Identity?.IsAuthenticated != true)
            {
                await context.ChallengeAsync();
                return;
            }

            await next();
        });
    }

    app.MapOpenApi().RequireAuthorization();
    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference().RequireAuthorization();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Finance Control BFF v1");
            options.RoutePrefix = "swagger";
        });
    }
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
})
    .AllowAnonymous();

var apiV1 = app.MapGroup("/api/v1");
apiV1.MapAuthEndpoints();
apiV1.MapDashboardEndpoints();
apiV1.MapAiEndpoints();
apiV1.MapFinanceEndpoints();
apiV1.MapDebtEndpoints();
apiV1.MapUserEndpoints();
apiV1.MapSocialEndpoints();
apiV1.MapNotificationEndpoints();
apiV1.MapHub<NotificationHub>("/notifications/hub").RequireAuthorization();

app.Run();

public partial class Program;
