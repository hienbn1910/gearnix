using Gearnix.Api.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// The only source of time for the application (Workflow.md §6.6); tests replace it with a fake clock.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<DatabaseOptions>()
    .BindConfiguration(DatabaseOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart(); // Missing or invalid settings stop the API at startup, not on the first request.

builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    var database = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    options.UseNpgsql(database.BuildConnectionString());
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"], timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();
app.MapControllers();

// Liveness: the process is running. Runs no checks, so it never touches the database.
app.MapHealthChecks("/api/health/live", new() { Predicate = _ => false });

// Readiness: the database is reachable. Unhealthy → 503.
app.MapHealthChecks("/api/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

app.Run();
