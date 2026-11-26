using Gearnix.Api.Modules.Identity.Dtos;
using Gearnix.Api.Modules.Identity.Validators;

namespace Gearnix.Api.UnitTests.Identity;

public sealed class AuthValidatorsTests
{
    private static readonly RegisterRequest Valid =
        new("an@example.vn", "12345678", "Nguyễn Văn An", "0901234567", "Hà Nội");

    private readonly RegisterRequestValidator _register = new();
    private readonly LoginRequestValidator _login = new();

    [Fact]
    public void Register_ValidRequest_IsValid()
    {
        Assert.True(_register.Validate(Valid).IsValid);
    }

    [Fact]
    public void Register_OptionalFieldsMissingAndEmailWithSpaces_IsValid()
    {
        var request = Valid with { Email = "  an@example.vn  ", Phone = null, City = null };

        Assert.True(_register.Validate(request).IsValid);
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(128, true)]
    [InlineData(7, false)]
    [InlineData(129, false)]
    public void Register_PasswordLength_ValidOnlyFrom8To128(int length, bool expectedValid)
    {
        var request = Valid with { Password = new string('a', length) };

        Assert.Equal(expectedValid, _register.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("not-an-email", "Email")]
    [InlineData("", "Email")]
    public void Register_BadEmail_FailsOnEmail(string email, string field)
    {
        AssertSingleFailure(_register.Validate(Valid with { Email = email }), field);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public void Register_BlankFullName_FailsOnFullName(string fullName)
    {
        AssertSingleFailure(_register.Validate(Valid with { FullName = fullName }), "FullName");
    }

    [Fact]
    public void Register_FullNameOver100_FailsOnFullName()
    {
        AssertSingleFailure(_register.Validate(Valid with { FullName = new string('a', 101) }), "FullName");
    }

    [Theory]
    [InlineData("901234567")] // no leading 0
    [InlineData("09012345678")] // 11 digits
    [InlineData("0901-234-567")]
    public void Register_BadPhone_FailsOnPhone(string phone)
    {
        AssertSingleFailure(_register.Validate(Valid with { Phone = phone }), "Phone");
    }

    [Fact]
    public void Register_CityOver100_FailsOnCity()
    {
        AssertSingleFailure(_register.Validate(Valid with { City = new string('a', 101) }), "City");
    }

    [Fact]
    public void Login_ValidRequest_IsValid()
    {
        Assert.True(_login.Validate(new LoginRequest("an@example.vn", "anything")).IsValid);
    }

    [Theory]
    [InlineData("", "secret", "Email")]
    [InlineData("an@example.vn", "", "Password")]
    public void Login_MissingField_FailsOnThatField(string email, string password, string field)
    {
        AssertSingleFailure(_login.Validate(new LoginRequest(email, password)), field);
    }

    private static void AssertSingleFailure(FluentValidation.Results.ValidationResult result, string field)
    {
        var failure = Assert.Single(result.Errors);
        Assert.Equal(field, failure.PropertyName);
    }
}
