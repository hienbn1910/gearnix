using Gearnix.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace Gearnix.Api.UnitTests.Data;

public sealed class IdentityModelTests
{
    [Theory]
    [InlineData("users")]
    [InlineData("roles")]
    [InlineData("user_roles")]
    [InlineData("user_claims")]
    [InlineData("user_logins")]
    [InlineData("user_tokens")]
    [InlineData("role_claims")]
    [InlineData("data_protection_keys")]
    public void Model_IdentityTables_UseAgreedNames(string expectedTable)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=unused").Options;
        using var context = new AppDbContext(options);

        var tables = context.Model.GetEntityTypes().Select(entity => entity.GetTableName()).ToHashSet();

        Assert.Contains(expectedTable, tables);
    }
}
