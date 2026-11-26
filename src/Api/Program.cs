var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapControllers();

// Liveness: the process is running. Runs no checks, so it never touches the database.
app.MapHealthChecks("/api/health/live", new() { Predicate = _ => false });

app.Run();
