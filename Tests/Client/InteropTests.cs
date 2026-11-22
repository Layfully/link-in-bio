using Client.Interop;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace LinkInBio.Tests.Client;

/// <summary>Tests typing animation cleanup and module import retries.</summary>
public sealed class InteropTests : IAsyncDisposable
{
    private static readonly string[] Taglines = ["Developer", "Climber"];

    private readonly FakeJs _js = new();
    private readonly FakeJsObject _module = new();

    public InteropTests() => _js.OnImport = _ => Task.FromResult(_module);

    public ValueTask DisposeAsync() => _module.DisposeAsync();

    [Fact]
    public async Task Starting_again_stops_the_running_animation()
    {
        FakeJsObject first = new(), second = new();
        Queue<FakeJsObject> animations = new([first, second]);
        _module.OnCall = _ => Task.FromResult<object?>(animations.Dequeue());
        await using TypedTextInterop interop = new(_js, NullLogger.Instance);

        await interop.StartAsync(default, Taglines);
        await interop.StartAsync(default, Taglines);

        AssertStopped(first);
        Assert.Empty(second.Calls);
    }

    [Fact]
    public async Task A_start_that_finishes_after_a_newer_one_began_is_stopped()
    {
        TaskCompletionSource<object?> olderStart = new(), newerStart = new();
        Queue<Task<object?>> starts = new([olderStart.Task, newerStart.Task]);
        _module.OnCall = _ => starts.Dequeue();
        FakeJsObject older = new(), newer = new();
        await using TypedTextInterop interop = new(_js, NullLogger.Instance);

        ValueTask olderTask = interop.StartAsync(default, Taglines);
        ValueTask newerTask = interop.StartAsync(default, Taglines);
        olderStart.SetResult(older);
        newerStart.SetResult(newer);
        await olderTask;
        await newerTask;

        AssertStopped(older);
        Assert.Empty(newer.Calls);
        Assert.False(newer.IsReleased);
    }

    [Fact]
    public async Task Stopping_ends_the_animation()
    {
        FakeJsObject animation = new();
        _module.OnCall = _ => Task.FromResult<object?>(animation);
        await using TypedTextInterop interop = new(_js, NullLogger.Instance);

        await interop.StartAsync(default, Taglines);
        await interop.StopAsync();

        AssertStopped(animation);
    }

    [Fact]
    public async Task Leaving_the_page_stops_the_animation_and_releases_the_module()
    {
        FakeJsObject animation = new();
        _module.OnCall = _ => Task.FromResult<object?>(animation);
        TypedTextInterop interop = new(_js, NullLogger.Instance);

        await interop.StartAsync(default, Taglines);
        await interop.DisposeAsync();

        AssertStopped(animation);
        Assert.True(_module.IsReleased);
    }

    [Fact]
    public async Task A_failed_import_is_tried_again_on_the_next_start()
    {
        int imports = 0;
        _js.OnImport = _ => ++imports == 1 ? Task.FromException<FakeJsObject>(new JSException("Network error.")) : Task.FromResult(_module);
        await using TypedTextInterop interop = new(_js, NullLogger.Instance);

        await interop.StartAsync(default, Taglines); // logged, not thrown
        await interop.StartAsync(default, Taglines);

        Assert.Equal(2, _js.Imports.Count);
        Assert.Equal(["start"], _module.Calls);
    }

    private static void AssertStopped(FakeJsObject animation)
    {
        Assert.Equal(["stop"], animation.Calls);
        Assert.True(animation.IsReleased);
    }
}
