using System.Security.Cryptography;
using System.Text;
using DevAutomation.Core.Options;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Slack;

public interface ISlackSignatureVerifier
{
    bool Verify(string timestamp, string body, string signature);
}

public sealed class SlackSignatureVerifier : ISlackSignatureVerifier
{
    private readonly SlackOptions _options;

    public SlackSignatureVerifier(IOptions<SlackOptions> options)
    {
        _options = options.Value;
    }

    public bool Verify(string timestamp, string body, string signature)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningSecret) || string.IsNullOrWhiteSpace(timestamp) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        if (!long.TryParse(timestamp, out var unixSeconds))
        {
            return false;
        }

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        if (age.Duration() > TimeSpan.FromMinutes(5))
        {
            return false;
        }

        var basestring = $"v0:{timestamp}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningSecret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(basestring))).ToLowerInvariant();
        var expected = $"v0={hash}";
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }
}
