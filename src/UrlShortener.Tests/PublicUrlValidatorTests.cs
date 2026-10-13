using UrlShortener.Api.Services;

namespace UrlShortener.Tests;

public sealed class PublicUrlValidatorTests
{
    private readonly PublicUrlValidator _validator = new();

    [Theory]
    [InlineData("http://8.8.8.8")]
    [InlineData("https://1.1.1.1/path")]
    public async Task ValidateAsync_AcceptsPublicHttpAndHttpsUrls(string url)
    {
        var error = await _validator.ValidateAsync(url, CancellationToken.None);

        Assert.Null(error);
    }

    [Theory]
    [InlineData("ftp://8.8.8.8")]
    [InlineData("http://127.0.0.1")]
    [InlineData("https://10.0.0.1")]
    [InlineData("not a url")]
    public async Task ValidateAsync_RejectsInvalidOrNonPublicUrls(string url)
    {
        var error = await _validator.ValidateAsync(url, CancellationToken.None);

        Assert.NotNull(error);
    }

    [Fact]
    public async Task ValidateAsync_RejectsUrlsLongerThan2048Characters()
    {
        const string prefix = "https://8.8.8.8/";
        var url = $"{prefix}{new string('a', 2049 - prefix.Length)}";

        var error = await _validator.ValidateAsync(url, CancellationToken.None);

        Assert.Equal("A URL de destino não pode exceder 2048 caracteres.", error);
    }

    [Fact]
    public async Task ValidateAsync_AcceptsUrlExactly2048CharactersLong()
    {
        const string prefix = "https://8.8.8.8/";
        var url = $"{prefix}{new string('a', 2048 - prefix.Length)}";

        var error = await _validator.ValidateAsync(url, CancellationToken.None);

        Assert.Null(error);
    }
}
