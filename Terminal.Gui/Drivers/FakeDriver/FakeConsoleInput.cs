#nullable enable
using System.Collections.Concurrent;

namespace Terminal.Gui.Drivers;

/// <summary>
///     Fake console input for testing that does not produce any input events.
/// </summary>
/// <typeparam name="T"></typeparam>
public class FakeConsoleInput<T> : FakeConsoleInputBase, IConsoleInput<T>
{
    /// <summary>
    ///     Create a timeout-based cancellation token too to prevent tests ever fully hanging
    /// </summary>
    /// <param name="hardStopToken"></param>
    public FakeConsoleInput (CancellationToken hardStopToken) : base (hardStopToken) { }

    /// <summary>
    /// The typed input buffer.
    /// </summary>
    public ConcurrentQueue<T>? TypedInputBuffer { get; private set; }

    /// <inheritdoc />
    public void Initialize (ConcurrentQueue<T> inputBuffer)
    {
        InputBuffer = TypedInputBuffer = inputBuffer;
    }
}
