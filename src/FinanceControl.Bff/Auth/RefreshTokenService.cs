using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace FinanceControl.Bff.Auth;

public sealed class RefreshTokenService
{
    public RefreshToken Create()
    {
        var value = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        return new RefreshToken(value, Hash(value));
    }

    public string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}

public sealed record RefreshToken(string Value, string Hash);
