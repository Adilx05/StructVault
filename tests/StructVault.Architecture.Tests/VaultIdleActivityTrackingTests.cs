using StructVault.Application.Abstractions.IdleLock;
using StructVault.Application.IdleLock;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultIdleActivityTrackingTests
{
    private static readonly DateTimeOffset InitialUtc = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordUserActivityCommand_updates_last_activity_time()
    {
        ManualTimeProvider timeProvider = new(InitialUtc);
        IIdleActivityTracker tracker = new IdleActivityTracker(timeProvider);
        RecordUserActivityCommandHandler handler = new(tracker);

        DateTimeOffset activityUtc = InitialUtc.AddMinutes(2);
        timeProvider.SetUtcNow(activityUtc);

        DateTimeOffset recordedUtc = await handler.Handle(new RecordUserActivityCommand(), CancellationToken.None);

        Assert.Equal(activityUtc, recordedUtc);
        Assert.Equal(activityUtc, tracker.LastActivityUtc);
    }

    [Fact]
    public async Task GetIdleActivityStateQuery_reports_active_before_timeout()
    {
        ManualTimeProvider timeProvider = new(InitialUtc);
        IIdleActivityTracker tracker = new IdleActivityTracker(timeProvider);
        GetIdleActivityStateQueryHandler handler = new(tracker);

        IdleActivityState state = await handler.Handle(
            new GetIdleActivityStateQuery(TimeSpan.FromMinutes(5), InitialUtc.AddMinutes(4)),
            CancellationToken.None);

        Assert.False(state.IsIdle);
        Assert.Equal(TimeSpan.FromMinutes(4), state.IdleDuration);
        Assert.Equal(InitialUtc, state.LastActivityUtc);
        Assert.Equal(InitialUtc.AddMinutes(4), state.ObservedAtUtc);
    }

    [Fact]
    public async Task GetIdleActivityStateQuery_reports_idle_when_timeout_is_reached()
    {
        ManualTimeProvider timeProvider = new(InitialUtc);
        IIdleActivityTracker tracker = new IdleActivityTracker(timeProvider);
        GetIdleActivityStateQueryHandler handler = new(tracker);

        IdleActivityState state = await handler.Handle(
            new GetIdleActivityStateQuery(TimeSpan.FromMinutes(5), InitialUtc.AddMinutes(5)),
            CancellationToken.None);

        Assert.True(state.IsIdle);
        Assert.Equal(TimeSpan.FromMinutes(5), state.IdleDuration);
    }

    [Fact]
    public async Task Activity_after_idle_period_resets_idle_state()
    {
        ManualTimeProvider timeProvider = new(InitialUtc);
        IIdleActivityTracker tracker = new IdleActivityTracker(timeProvider);
        RecordUserActivityCommandHandler recordHandler = new(tracker);
        GetIdleActivityStateQueryHandler stateHandler = new(tracker);

        IdleActivityState idleState = await stateHandler.Handle(
            new GetIdleActivityStateQuery(TimeSpan.FromMinutes(5), InitialUtc.AddMinutes(6)),
            CancellationToken.None);
        Assert.True(idleState.IsIdle);

        DateTimeOffset activityUtc = InitialUtc.AddMinutes(6);
        timeProvider.SetUtcNow(activityUtc);
        await recordHandler.Handle(new RecordUserActivityCommand(), CancellationToken.None);

        IdleActivityState activeState = await stateHandler.Handle(
            new GetIdleActivityStateQuery(TimeSpan.FromMinutes(5), activityUtc.AddSeconds(30)),
            CancellationToken.None);

        Assert.False(activeState.IsIdle);
        Assert.Equal(TimeSpan.FromSeconds(30), activeState.IdleDuration);
        Assert.Equal(activityUtc, activeState.LastActivityUtc);
    }

    [Fact]
    public void GetIdleActivityStateQuery_rejects_non_positive_timeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GetIdleActivityStateQuery(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GetIdleActivityStateQuery(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void RecordActivity_rejects_future_activity_time()
    {
        ManualTimeProvider timeProvider = new(InitialUtc);
        IIdleActivityTracker tracker = new IdleActivityTracker(timeProvider);

        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.RecordActivity(InitialUtc.AddTicks(1)));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow.ToUniversalTime();
        }

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void SetUtcNow(DateTimeOffset value)
        {
            utcNow = value.ToUniversalTime();
        }
    }
}
