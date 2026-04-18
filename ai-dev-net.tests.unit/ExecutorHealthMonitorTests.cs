using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AiDevNet.Tests.Unit;

public sealed class ExecutorHealthMonitorTests
{
    [Fact]
    public async Task RefreshAsync_WhenExecutorReturnsHealth_UpdatesCacheAndMetadata()
    {
        var executor = new TestExecutor("exec-a", _ => Task.FromResult(new ExecutorHealthResult(true, "ok")));
        var monitor = CreateMonitor(executor);

        await monitor.RefreshAsync(TestContext.Current.CancellationToken);

        var result = monitor.GetHealth("exec-a");
        result.IsHealthy.ShouldBeTrue();
        result.Message.ShouldBe("ok");
        result.CheckedAt.ShouldNotBeNull();
        result.Duration.ShouldNotBeNull();
        monitor.LastChecked.ShouldNotBeNull();
    }

    [Fact]
    public async Task RefreshAsync_WhenCalled_RaisesChangedAndSupportsUnsubscribe()
    {
        var executor = new TestExecutor("exec-a", _ => Task.FromResult(new ExecutorHealthResult(true, "ok")));
        var monitor = CreateMonitor(executor);
        var count = 0;

        using var subscription = monitor.SubscribeChanged(() => Interlocked.Increment(ref count));

        await monitor.RefreshAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(1);

        subscription.Dispose();

        await monitor.RefreshAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshAsync_WhenHealthTransitions_RaisesTransitioned()
    {
        var healthy = true;
        var executor = new TestExecutor("exec-a", _ => Task.FromResult(new ExecutorHealthResult(healthy, healthy ? "up" : "down")));
        var monitor = CreateMonitor(executor);
        var transitions = new ConcurrentQueue<(string Name, bool From, bool To)>();

        using var subscription = monitor.SubscribeTransitioned((name, previous, current) =>
            transitions.Enqueue((name, previous.IsHealthy, current.IsHealthy)));

        await monitor.RefreshAsync(TestContext.Current.CancellationToken);

        healthy = false;
        await monitor.RefreshAsync(TestContext.Current.CancellationToken);

        transitions.Count.ShouldBe(1);
        transitions.TryDequeue(out var transition).ShouldBeTrue();
        transition.Name.ShouldBe("exec-a");
        transition.From.ShouldBeTrue();
        transition.To.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshAsync_WhenTransitionSubscriptionDisposed_DoesNotRaiseTransitioned()
    {
        var healthy = true;
        var executor = new TestExecutor("exec-a", _ => Task.FromResult(new ExecutorHealthResult(healthy, healthy ? "up" : "down")));
        var monitor = CreateMonitor(executor);
        var count = 0;

        var subscription = monitor.SubscribeTransitioned((_, _, _) => Interlocked.Increment(ref count));

        await monitor.RefreshAsync(TestContext.Current.CancellationToken);
        subscription.Dispose();

        healthy = false;
        await monitor.RefreshAsync(TestContext.Current.CancellationToken);

        count.ShouldBe(0);
    }

    private static ExecutorHealthMonitor CreateMonitor(params IAgentExecutor[] executors)
        => new(executors, NullLogger<ExecutorHealthMonitor>.Instance);

    private sealed class TestExecutor : IAgentExecutor
    {
        private readonly string _name;
        private readonly Func<CancellationToken, Task<ExecutorHealthResult>> _checkHealthAsync;

        public TestExecutor(string name, Func<CancellationToken, Task<ExecutorHealthResult>> checkHealthAsync)
        {
            _name = name;
            _checkHealthAsync = checkHealthAsync;
        }

        public string Name => _name;

        public string DisplayName => _name;

        public IReadOnlyList<ExecutorSkill> AvailableSkills => [];

        public IReadOnlyList<ModelDescriptor> KnownModels => [];

        public Task<ExecutorHealthResult> CheckHealthAsync(CancellationToken ct = default)
            => _checkHealthAsync(ct);

        public Task<ExecutorResult> RunAsync(ExecutorContext context, ChannelWriter<string> output)
            => Task.FromResult(new ExecutorResult(1, ErrorMessage: "Not implemented"));
    }
}
