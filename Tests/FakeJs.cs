using Microsoft.JSInterop;

namespace LinkInBio.Tests;

/// <summary>Records module imports and lets tests control when they complete.</summary>
internal sealed class FakeJs : IJSRuntime
{
    public List<string> Imports { get; } = [];

    public Func<string, Task<FakeJsObject>> OnImport { get; set; } = _ => Task.FromResult(new FakeJsObject());

    public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (identifier != "import")
        {
            throw new JSException($"Only modules are imported here, not '{identifier}' called.");
        }

        string path = (string)args![0]!;
        Imports.Add(path);
        return (TValue)(object)await OnImport(path);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);
}

/// <summary>Records JavaScript calls and delegates their results to <see cref="OnCall"/>.</summary>
internal sealed class FakeJsObject : IJSObjectReference
{
    public List<string> Calls { get; } = [];

    public bool IsReleased { get; private set; }

    /// <summary>Returns a result by method name. Defaults to a completed call with no value.</summary>
    public Func<string, Task<object?>> OnCall { get; set; } = _ => Task.FromResult<object?>(null);

    public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (IsReleased)
        {
            throw new JSException($"'{identifier}' was called on a released object.");
        }

        Calls.Add(identifier);
        return (TValue)(await OnCall(identifier))!;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);

    public ValueTask DisposeAsync()
    {
        IsReleased = true;
        return ValueTask.CompletedTask;
    }
}
