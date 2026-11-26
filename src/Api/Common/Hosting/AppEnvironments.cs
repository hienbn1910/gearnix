namespace Gearnix.Api.Common.Hosting;

/// <summary>The project's environment names (Workflow.md §11.6) beyond the built-in Development.</summary>
public static class AppEnvironments
{
    public const string Test = "Test";
    public const string LoadTest = "LoadTest";
    public const string Demo = "Demo";

    /// <summary>Environments with demo data: the seeded demo accounts and the mock gateway.</summary>
    public static bool HasDemoData(this IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment(Demo);
}
