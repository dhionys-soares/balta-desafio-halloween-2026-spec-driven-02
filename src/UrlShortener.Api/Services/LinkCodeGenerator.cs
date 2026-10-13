using System.Security.Cryptography;

namespace UrlShortener.Api.Services;

public sealed class LinkCodeGenerator : ILinkCodeGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";

    public string Generate()
    {
        Span<char> code = stackalloc char[7];

        for (var index = 0; index < code.Length; index++)
        {
            code[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(code);
    }
}
