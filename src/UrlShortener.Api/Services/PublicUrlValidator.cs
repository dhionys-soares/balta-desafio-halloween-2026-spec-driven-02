using System.Net;
using System.Net.Sockets;

namespace UrlShortener.Api.Services;

public sealed class PublicUrlValidator : IUrlValidator
{
    private const int MaximumUrlLength = 2048;

    public async Task<string?> ValidateAsync(
        string? value,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "A URL de destino é obrigatória.";
        }

        if (value.Length > MaximumUrlLength)
        {
            return "A URL de destino não pode exceder 2048 caracteres.";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return "A URL de destino deve ser uma URL absoluta HTTP ou HTTPS.";
        }

        IPAddress[] addresses;

        if (IPAddress.TryParse(uri.Host, out var address))
        {
            addresses = [address];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            }
            catch (SocketException)
            {
                return "A URL de destino não está publicamente acessível.";
            }
        }

        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            return "A URL de destino não está publicamente acessível.";
        }

        return null;
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var first = bytes[0];
            var second = bytes[1];

            return first != 0
                && first != 10
                && first != 127
                && first < 224
                && !(first == 100 && second is >= 64 and <= 127)
                && !(first == 169 && second == 254)
                && !(first == 172 && second is >= 16 and <= 31)
                && !(first == 192 && second is 0 or 2 or 88 or 168)
                && !(first == 198 && second is 18 or 19 or 51)
                && !(first == 203 && second == 0);
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6
            && bytes[0] is >= 0x20 and <= 0x3f
            && !(bytes[0] == 0x20 && bytes[1] == 0x01
                && bytes[2] == 0x0d && bytes[3] == 0xb8);
    }
}
