using DevAutomation.Core.Options;

namespace DevAutomation.Infrastructure.RemoteRepositories;

public interface IRemoteRepositoryIntegration
{
    RemoteRepositoryProvider Provider { get; }

    void AddEnvironment(ICollection<string> environment, AgentOptions options);

    string BuildCreateChangeRequestScript();
}
