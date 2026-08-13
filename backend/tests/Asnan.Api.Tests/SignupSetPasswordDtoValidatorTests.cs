using Asnan.Application.Auth;

namespace Asnan.Api.Tests;

public class SignupSetPasswordDtoValidatorTests
{
    private readonly SignupSetPasswordDtoValidator _validator = new();

    [Theory]
    [InlineData("short1")]
    [InlineData("1234567")]
    public async Task TooShort_IsRejected(string password)
    {
        var result = await _validator.ValidateAsync(new SignupSetPasswordDto("token", password));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("password123")]
    [InlineData("qwerty123")]
    [InlineData("letmein")]
    public async Task CommonPassword_IsRejected(string password)
    {
        var result = await _validator.ValidateAsync(new SignupSetPasswordDto("token", password));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ReasonablyStrongPassword_IsAccepted()
    {
        var result = await _validator.ValidateAsync(new SignupSetPasswordDto("token", "correct horse battery staple"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task EmptySignupToken_IsRejected()
    {
        var result = await _validator.ValidateAsync(new SignupSetPasswordDto("", "correct horse battery staple"));

        Assert.False(result.IsValid);
    }
}
