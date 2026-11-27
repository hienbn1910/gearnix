using System.Net;

using Gearnix.Api.IntegrationTests.Infrastructure;

namespace Gearnix.Api.IntegrationTests.Errors;

/// <summary>
/// Query-string errors are keyed by the camelCase name the client sent (Workflow.md §9), whether
/// FluentValidation or model binding rejected the value (#20).
/// </summary>
public sealed class QueryValidationKeysTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("/api/admin/categories?pageSize=101", "pageSize")] // FluentValidation
    [InlineData("/api/admin/categories?page=0", "page")] // FluentValidation
    [InlineData("/api/admin/categories?pageSize=abc", "pageSize")] // model binding
    [InlineData("/api/admin/products?isActive=abc", "isActive")] // model binding
    [InlineData("/api/admin/products?categoryId=xyz", "categoryId")] // model binding
    public async Task Get_InvalidQueryValue_ErrorIsKeyedByCamelCaseName(string url, string expectedKey)
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(await admin.GetAsync(url, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("validation_failed", problem.Code);
        var keys = problem.Body.GetProperty("errors").EnumerateObject().Select(error => error.Name);
        Assert.Equal([expectedKey], keys);
    }

    [Fact]
    public async Task Get_QueryTooLong_ErrorIsKeyedByQ()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var admin = factory.CreateApiClientAs(Guid.CreateVersion7(), "Admin");

        var problem = await ProblemResponse.ReadAsync(await admin.GetAsync(
            $"/api/admin/products?q={new string('a', 201)}", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        var keys = problem.Body.GetProperty("errors").EnumerateObject().Select(error => error.Name);
        Assert.Equal(["q"], keys);
    }
}
