using Gearnix.Api.IntegrationTests.Infrastructure;

using Microsoft.Extensions.Options;

namespace Gearnix.Api.IntegrationTests.Startup;

public sealed class StartupTests
{
    [Fact]
    public async Task Start_ConnectionStringMissing_FailsWithValidationError()
    {
        await using var factory = new GearnixApiFactory(connectionString: null);

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionString", error.Message);
    }
}
