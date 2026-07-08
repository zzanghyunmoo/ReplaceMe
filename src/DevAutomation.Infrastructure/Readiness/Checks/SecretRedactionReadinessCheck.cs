using DevAutomation.Core.Options;
using DevAutomation.Core.Readiness;
using DevAutomation.Infrastructure.Agents;
using Microsoft.Extensions.Options;

namespace DevAutomation.Infrastructure.Readiness.Checks;

public sealed class SecretRedactionReadinessCheck : IProfileReadinessCheck
{
    private readonly ISecretCatalog _secretCatalog;
    private readonly SecretRedactor _redactor;
    private readonly ProfileReadinessOptions _options;

    public SecretRedactionReadinessCheck(ISecretCatalog secretCatalog, SecretRedactor redactor, IOptions<ProfileReadinessOptions> options)
    {
        _secretCatalog = secretCatalog;
        _redactor = redactor;
        _options = options.Value;
    }

    public string Id => "secrets.redaction.coverage";

    public Task<ProfileReadinessCheckResult> CheckAsync(ProfileReadinessContext context, CancellationToken cancellationToken)
    {
        var configuredSecrets = _secretCatalog.GetSecrets()
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .ToArray();

        if (configuredSecrets.Length == 0)
        {
            return Task.FromResult(ProfileReadinessCheckResult.Passed(Id, "Safety", _options.Checks.SecretsRedactionSeverity, "No configured secrets were found to redact."));
        }

        var missing = configuredSecrets.Where(x => !_redactor.Covers(x.Value)).Select(x => x.Label).ToArray();
        if (missing.Length == 0)
        {
            return Task.FromResult(ProfileReadinessCheckResult.Passed(Id, "Safety", _options.Checks.SecretsRedactionSeverity, $"Redaction covers {configuredSecrets.Length} configured secret value(s)."));
        }

        var summary = $"Redaction does not cover configured secret label(s): {string.Join(", ", missing)}.";
        return Task.FromResult(_options.Checks.SecretsRedactionSeverity == ProfileReadinessSeverity.Required
            ? ProfileReadinessCheckResult.Failed(Id, "Safety", ProfileReadinessSeverity.Required, summary, "Add missing secret sources to the secret catalog/redactor.")
            : ProfileReadinessCheckResult.Warning(Id, "Safety", summary, "Add missing secret sources to the secret catalog/redactor."));
    }
}
