namespace DevAutomation.Infrastructure.Agents;

public sealed class SecretRedactor
{
    private readonly string[] _secrets;

    public SecretRedactor(IEnumerable<string?> secrets)
    {
        _secrets = secrets.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct().ToArray();
    }

    public string Redact(string content)
    {
        var result = content ?? string.Empty;
        foreach (var secret in _secrets)
        {
            result = result.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        }

        return result;
    }
}
