#nullable enable

namespace Terminal.Gui.Drivers;

/// <summary>
/// Non-generic base class for all fake console inputs, allowing them to be stored in one field.
/// </summary>
public abstract class FakeConsoleInputBase (CancellationToken hardStopToken) : IDisposable
{
    private readonly CancellationTokenSource _timeoutCts = new (TimeSpan.FromSeconds (30));

    /// <summary>
    /// Cancellation token that signals when the input should stop.
    /// </summary>
    public CancellationToken HardStopToken { get; } = hardStopToken;

    /// <summary>
    ///     Gets the input buffer.
    /// </summary>
    public object? InputBuffer { get; internal set; }

    /// <summary>
    /// Runs until either the supplied token, <see cref="HardStopToken"/>, or timeout is cancelled.
    /// </summary>
    public virtual void Run (CancellationToken token)
    {
        WaitHandle.WaitAny ([token.WaitHandle, HardStopToken.WaitHandle, _timeoutCts.Token.WaitHandle]);
    }

    /// <inheritdoc />
    public virtual void Dispose () { }
}
