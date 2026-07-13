using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DevAutomation.Infrastructure.Telemetry;

public static class DevAutomationTelemetry
{
    public const string ActivitySourceName = "DevAutomation";
    public const string MeterName = "DevAutomation";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> TicketsCreated = Meter.CreateCounter<long>("devautomation.tickets.created");
    public static readonly Counter<long> AgentJobsStarted = Meter.CreateCounter<long>("devautomation.agent_jobs.started");
    public static readonly Counter<long> AgentJobsCompleted = Meter.CreateCounter<long>("devautomation.agent_jobs.completed");
    public static readonly Counter<long> AgentJobsFailed = Meter.CreateCounter<long>("devautomation.agent_jobs.failed");
    public static readonly Counter<long> KafkaAgentJobsRetried = Meter.CreateCounter<long>("devautomation.kafka.agent_jobs.retried");
    public static readonly Counter<long> KafkaAgentJobsDlqPublished = Meter.CreateCounter<long>("devautomation.kafka.agent_jobs.dlq_published");
    public static readonly Counter<long> ExecutionLogsFlushed = Meter.CreateCounter<long>("devautomation.execution_logs.flushed");
    public static readonly Histogram<double> AgentJobDuration = Meter.CreateHistogram<double>("devautomation.agent_jobs.duration", "s");
}
