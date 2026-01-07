using FinanceControl.Bff.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Health
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "finance-control-bff"
}));

// Endpoints
app.MapAuthEndpoints();
app.MapDashboardEndpoints();

app.Run();