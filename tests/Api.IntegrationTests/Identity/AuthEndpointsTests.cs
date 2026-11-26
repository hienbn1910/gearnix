using System.Net;
using System.Net.Http.Json;

using Gearnix.Api.IntegrationTests.Infrastructure;

namespace Gearnix.Api.IntegrationTests.Identity;

/// <summary>Register, login, logout and me with the real Identity cookie (no test authentication header).</summary>
public sealed class AuthEndpointsTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery";

    [Fact]
    public async Task Register_NewEmail_Returns201AndSignsInAsCustomer()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var client = factory.CreateApiClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, fullName = "Nguyễn Văn An", phone = "0901234567", city = "Hà Nội" },
            TestContext.Current.CancellationToken);
        var me = await client.GetFromJsonAsync<MeBody>("/api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(me);
        Assert.Equal(email, me.Email);
        Assert.Equal("Nguyễn Văn An", me.FullName);
        Assert.Equal("Customer", me.Role);
    }

    [Fact]
    public async Task Register_ExistingEmailInOtherCase_Returns409EmailTaken()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        var email = UniqueEmail();
        await RegisterAsync(factory, email);
        using var client = factory.CreateApiClient();

        var problem = await ProblemResponse.ReadAsync(await client.PostAsJsonAsync("/api/auth/register",
            new { email = email.ToUpperInvariant(), password = Password, fullName = "Trần Bình" },
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Conflict, problem.Status);
        Assert.Equal("email_taken", problem.Code);
    }

    [Fact]
    public async Task Register_SameEmailInParallel_ExactlyOneSucceeds()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        var email = UniqueEmail();
        var clients = Enumerable.Range(0, 10).Select(_ => factory.CreateApiClient()).ToList();

        var responses = await Task.WhenAll(clients.Select(client => client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, fullName = "Lê Cường" }, TestContext.Current.CancellationToken)));
        clients.ForEach(client => client.Dispose());

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(9, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Login_CorrectPassword_Returns200AndSignsIn()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        var email = UniqueEmail();
        await RegisterAsync(factory, email);
        using var client = factory.CreateApiClient();

        var response = await LoginAsync(client, email, Password);
        var me = await client.GetFromJsonAsync<MeBody>("/api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, me?.Email);
    }

    [Fact]
    public async Task Login_WrongPasswordOrUnknownEmail_ReturnSameInvalidCredentials()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        var email = UniqueEmail();
        await RegisterAsync(factory, email);
        using var client = factory.CreateApiClient();

        var wrongPassword = await ProblemResponse.ReadAsync(await LoginAsync(client, email, "wrong password"));
        var unknownEmail = await ProblemResponse.ReadAsync(await LoginAsync(client, UniqueEmail(), Password));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.Status);
        Assert.Equal("invalid_credentials", wrongPassword.Code);
        Assert.Equal(wrongPassword.Status, unknownEmail.Status);
        Assert.Equal(wrongPassword.Code, unknownEmail.Code);
        Assert.Equal("Email or password is incorrect.", Detail(wrongPassword));
        Assert.Equal(Detail(wrongPassword), Detail(unknownEmail));
    }

    [Fact]
    public async Task Login_AfterFiveFailures_CorrectPasswordStillReturns401()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        var email = UniqueEmail();
        await RegisterAsync(factory, email);
        using var client = factory.CreateApiClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(client, email, "wrong password");
        }

        var problem = await ProblemResponse.ReadAsync(await LoginAsync(client, email, Password));

        Assert.Equal(HttpStatusCode.Unauthorized, problem.Status);
        Assert.Equal("invalid_credentials", problem.Code);
    }

    [Fact]
    public async Task Logout_SignedIn_Returns204AndMeReturns401()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var client = factory.CreateApiClient();
        await RegisterAsync(client, UniqueEmail());

        var logout = await client.PostAsync("/api/auth/logout", null, TestContext.Current.CancellationToken);
        var me = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Logout_NotSignedIn_Returns204()
    {
        await using var factory = new GearnixApiFactory(postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        var logout = await client.PostAsync("/api/auth/logout", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    private static async Task RegisterAsync(GearnixApiFactory factory, string email)
    {
        using var client = factory.CreateApiClient();
        await RegisterAsync(client, email);
    }

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, fullName = "Phạm Dung" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password }, TestContext.Current.CancellationToken);

    private static string? Detail(ProblemResponse problem) =>
        problem.Body.TryGetProperty("detail", out var detail) ? detail.GetString() : null;

    internal static string UniqueEmail() => $"user-{Guid.CreateVersion7():N}@test.local";

    private sealed record MeBody(Guid Id, string Email, string FullName, string Role);
}
