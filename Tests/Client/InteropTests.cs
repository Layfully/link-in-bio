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
    public async Task StartAsync_ShouldStopTheRunningAnimation_WhenCalledAgain()
    {
        // Arrange
        FakeJsObject first = new(), second = new();
        Queue<FakeJsObject> animations = new([first, second]);
        _module.OnCall = _ => Task.FromResult<object?>(animations.Dequeue());
        await using TypedTextInterop sut = new(_js, NullLogger.Instance);

        // Act
        await sut.StartAsync(default, Taglines);
        await sut.StartAsync(default, Taglines);

        // Assert
        AssertStopped(first);
        second.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task StartAsync_ShouldStopTheOlderAnimation_WhenItFinishesAfterANewerStartBegan()
    {
        // Arrange
        TaskCompletionSource<object?> olderStart = new(), newerStart = new();
        Queue<Task<object?>> starts = new([olderStart.Task, newerStart.Task]);
        _module.OnCall = _ => starts.Dequeue();
        FakeJsObject older = new(), newer = new();
        await using TypedTextInterop sut = new(_js, NullLogger.Instance);

        // Act
        ValueTask olderTask = sut.StartAsync(default, Taglines);
        ValueTask newerTask = sut.StartAsync(default, Taglines);
        olderStart.SetResult(older);
        newerStart.SetResult(newer);
        await olderTask;
        await newerTask;

        // Assert
        AssertStopped(older);
        newer.Calls.ShouldBeEmpty();
        newer.IsReleased.ShouldBeFalse();
    }

    [Fact]
    public async Task StopAsync_ShouldEndTheAnimation_WhenOneIsRunning()
    {
        // Arrange
        FakeJsObject animation = new();
        _module.OnCall = _ => Task.FromResult<object?>(animation);
        await using TypedTextInterop sut = new(_js, NullLogger.Instance);

        // Act
        await sut.StartAsync(default, Taglines);
        await sut.StopAsync();

        // Assert
        AssertStopped(animation);
    }

    [Fact]
    public async Task DisposeAsync_ShouldStopTheAnimationAndReleaseTheModule_WhenThePageIsLeft()
    {
        // Arrange
        FakeJsObject animation = new();
        _module.OnCall = _ => Task.FromResult<object?>(animation);
        TypedTextInterop sut = new(_js, NullLogger.Instance);

        // Act
        await sut.StartAsync(default, Taglines);
        await sut.DisposeAsync();

        // Assert
        AssertStopped(animation);
        _module.IsReleased.ShouldBeTrue();
    }

    [Fact]
    public async Task StartAsync_ShouldImportTheModuleAgain_WhenTheFirstImportFailed()
    {
        // Arrange
        int imports = 0;
        _js.OnImport = _ => ++imports == 1 ? Task.FromException<FakeJsObject>(new JSException("Network error.")) : Task.FromResult(_module);
        await using TypedTextInterop sut = new(_js, NullLogger.Instance);

        // Act
        await sut.StartAsync(default, Taglines); // logged, not thrown
        await sut.StartAsync(default, Taglines);

        // Assert
        _js.Imports.Count.ShouldBe(2);
        _module.Calls.ShouldBe(["start"]);
    }

    private static void AssertStopped(FakeJsObject animation)
    {
        animation.Calls.ShouldBe(["stop"]);
        animation.IsReleased.ShouldBeTrue();
    }
}
