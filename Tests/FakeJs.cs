using Microsoft.JSInterop;

namespace LinkInBio.Tests;

/// <summary>
/// A JavaScript runtime for the interop classes: <c>import</c> answers with <see cref="OnImport"/>, any other call fails.
/// Unlike bUnit's JS interop, a test decides exactly when each import or call finishes.
/// </summary>
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

/// <summary>A JavaScript object, a module or a handle it returned: records the calls it gets and answers with <see cref="OnCall"/>.</summary>
internal sealed class FakeJsObject : IJSObjectReference
{
    public List<string> Calls { get; } = [];

    public bool IsReleased { get; private set; }

    /// <summary>Answers a call by its method name. By default every call finishes at once without a value.</summary>
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
