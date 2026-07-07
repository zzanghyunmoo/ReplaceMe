namespace DevAutomation.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
