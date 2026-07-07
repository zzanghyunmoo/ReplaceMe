using DevAutomation.Core.Abstractions;
using DevAutomation.Core.Entities;
using DevAutomation.Core.Options;
using DevAutomation.Core.Services;
using Microsoft.Extensions.Options;

namespace DevAutomation.Tests;

public sealed class ApprovalServiceTests
{
    [Fact]
    public async Task RequestApprovalAsync_returns_allow_when_request_is_approved()
    {
        var repository = new InMemoryApprovalRequestRepository();
        var notifier = new CapturingApprovalNotifier();
        var service = CreateService(repository, notifier, TimeSpan.FromSeconds(2));
        var notification = new ApprovalNotification(Guid.NewGuid(), "Ticket", "Bash", "Run command", "{\"command\":\"dotnet test\"}");

        var pendingTask = service.RequestApprovalAsync(notification, CancellationToken.None);
        var request = await repository.WaitForRequestAsync();
        request.Approve("U123", DateTimeOffset.UtcNow);
        await repository.SaveChangesAsync(CancellationToken.None);

        var decision = await pendingTask;

        Assert.Equal("allow", decision.Behavior);
        Assert.NotNull(decision.UpdatedInput);
        Assert.Equal("dotnet test", decision.UpdatedInput!.Value.GetProperty("command").GetString());
        Assert.Equal("message-ts", request.SlackMessageTs);
    }

    [Fact]
    public async Task RequestApprovalAsync_times_out_pending_request_as_deny()
    {
        var repository = new InMemoryApprovalRequestRepository();
        var notifier = new CapturingApprovalNotifier();
        var service = CreateService(repository, notifier, TimeSpan.FromMilliseconds(30));
        var notification = new ApprovalNotification(Guid.NewGuid(), "Ticket", "Bash", "Run command", "{}");

        var decision = await service.RequestApprovalAsync(notification, CancellationToken.None);
        var request = repository.Requests.Single();

        Assert.Equal("deny", decision.Behavior);
        Assert.Equal(ApprovalStatus.TimedOut, request.Status);
        Assert.Single(notifier.UpdatedRequests);
    }

    private static ApprovalService CreateService(InMemoryApprovalRequestRepository repository, CapturingApprovalNotifier notifier, TimeSpan timeout)
    {
        return new ApprovalService(
            repository,
            notifier,
            new SystemClock(),
            Options.Create(new ApprovalOptions { ApprovalTimeout = timeout, PollInterval = TimeSpan.FromMilliseconds(1) }));
    }

    private sealed class InMemoryApprovalRequestRepository : IApprovalRequestRepository
    {
        private readonly TaskCompletionSource<ApprovalRequest> _created = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<ApprovalRequest> Requests { get; } = [];

        public Task<ApprovalRequest> AddAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken)
        {
            Requests.Add(approvalRequest);
            _created.TrySetResult(approvalRequest);
            return Task.FromResult(approvalRequest);
        }

        public Task<ApprovalRequest?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Requests.FirstOrDefault(x => x.Id == id));
        }

        public Task<ApprovalRequest?> MarkTimedOutAsync(Guid id, DateTimeOffset respondedAt, CancellationToken cancellationToken)
        {
            var request = Requests.FirstOrDefault(x => x.Id == id);
            if (request?.Status == ApprovalStatus.Pending)
            {
                request.TimeOut(respondedAt);
            }

            return Task.FromResult(request);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ApprovalRequest> WaitForRequestAsync() => _created.Task;
    }

    private sealed class CapturingApprovalNotifier : IApprovalNotifier
    {
        public List<ApprovalRequest> UpdatedRequests { get; } = [];

        public Task<SlackMessageRef> SendApprovalRequestAsync(ApprovalRequest approvalRequest, ApprovalNotification notification, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SlackMessageRef("channel", "message-ts"));
        }

        public Task UpdateApprovalResultAsync(ApprovalRequest approvalRequest, CancellationToken cancellationToken)
        {
            UpdatedRequests.Add(approvalRequest);
            return Task.CompletedTask;
        }
    }
}
