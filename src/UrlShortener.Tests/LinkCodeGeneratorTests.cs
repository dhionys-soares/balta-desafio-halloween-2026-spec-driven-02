using UrlShortener.Api.Services;

namespace UrlShortener.Tests;

public sealed class LinkCodeGeneratorTests
{
    [Fact]
    public void Generate_ReturnsSevenAllowedCharacters()
    {
        var generator = new LinkCodeGenerator();

        var code = generator.Generate();

        Assert.Equal(7, code.Length);
        Assert.Matches("^[A-Z1-9]{7}$", code);
    }

    [Theory]
    [InlineData("ABCD")]
    [InlineData("A1B2C3D4E5F6G7H8I9J1")]
    public void IsValidCustomCode_AcceptsAllowedCodes(string code) =>
        Assert.True(LinkCodeRules.IsValidCustomCode(code));

    [Theory]
    [InlineData("ABC")]
    [InlineData("abcD")]
    [InlineData("AB0D")]
    [InlineData("AB D")]
    [InlineData("AB-D")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public void IsValidCustomCode_RejectsDisallowedCodes(string code) =>
        Assert.False(LinkCodeRules.IsValidCustomCode(code));
}
