using FinanceControl.Bff.Notifications;

namespace FinanceControl.Bff.Tests;

public sealed class WebPushOptionsTests
{
    [Fact]
    public void IsConfigured_AcceptsAValidVapidKeyPair()
    {
        var publicKey = new byte[65];
        publicKey[0] = 4;

        var options = new WebPushOptions
        {
            Enabled = true,
            Subject = "mailto:owner@example.org",
            PublicKey = ToBase64Url(publicKey),
            PrivateKey = ToBase64Url(new byte[32])
        };

        Assert.True(options.IsConfigured);
    }

    [Theory]
    [InlineData("owner@example.org", "public", "private")]
    [InlineData("mailto:owner@example.org", "invalid", "invalid")]
    [InlineData("https://finance.example.org", "", "")]
    public void IsConfigured_RejectsInvalidConfiguration(
        string subject,
        string publicKey,
        string privateKey)
    {
        var options = new WebPushOptions
        {
            Enabled = true,
            Subject = subject,
            PublicKey = publicKey,
            PrivateKey = privateKey
        };

        Assert.False(options.IsConfigured);
    }

    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
