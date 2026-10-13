using System.Text.RegularExpressions;

namespace UrlShortener.Api.Services;

public static partial class LinkCodeRules
{
    [GeneratedRegex("^[A-Z1-9]{4,20}$", RegexOptions.CultureInvariant)]
    private static partial Regex CustomCodeRegex();

    public static bool IsValidCustomCode(string code) =>
        CustomCodeRegex().IsMatch(code);
}
