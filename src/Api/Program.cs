using System.Text.Json.Serialization;

using FluentValidation;

using Gearnix.Api.Common.Errors;
using Gearnix.Api.Common.Security;
using Gearnix.Api.Common.Validation;
using Gearnix.Api.Data;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<FluentValidationFilter>();
        // Every action can fail validation or crash; document both error shapes for the generated frontend types.
        options.Filters.Add(new ProducesResponseTypeAttribute<ValidationProblemDetails>(StatusCodes.Status400BadRequest));
        options.Filters.Add(new ProducesResponseTypeAttribute<ProblemDetails>(StatusCodes.Status500InternalServerError));
    })
    .AddJsonOptions(options =>
    {
        // Enums travel as strings ("PendingPayment"), never as numbers.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Malformed-JSON errors get a generic message instead of the serializer's (which names internal types).
        options.AllowInputFormatterExceptionMessages = false;
    });
// The OpenAPI document reads these options, so enums are described as strings there too.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddGearnixProblemDetails();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();

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

app.UseExceptionHandler();

// Error responses without a body (unknown route, wrong method) become ProblemDetails.
app.UseStatusCodePages();

// State-changing requests must carry X-CSRF: 1 (Workflow.md §11.5).
app.UseMiddleware<CsrfHeaderMiddleware>();

app.MapControllers();

// API documentation, under /api so the reverse proxy exposes it; not in Test or LoadTest.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Demo"))
{
    app.MapOpenApi("/api/openapi/{documentName}.json").AllowAnonymous();
    app.MapScalarApiReference("/api/docs", options => options.WithOpenApiRoutePattern("/api/openapi/{documentName}.json"))
        .AllowAnonymous();
}

// Liveness: the process is running. Runs no checks, so it never touches the database.
app.MapHealthChecks("/api/health/live", new() { Predicate = _ => false })
    .AllowAnonymous();

// Readiness: the database is reachable. Unhealthy → 503.
app.MapHealthChecks("/api/health/ready", new() { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous();

app.Run();
