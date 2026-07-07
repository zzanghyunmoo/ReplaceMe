using DevAutomation.Core.Abstractions;

namespace DevAutomation.Core.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
