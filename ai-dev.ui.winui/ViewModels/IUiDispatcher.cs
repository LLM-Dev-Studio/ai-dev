namespace AiDev.WinUI.ViewModels;

/// <summary>
/// Provides a UI-thread dispatch abstraction for view models.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Enqueues an action to run on the UI thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    void Enqueue(Action action);
}

/// <summary>
/// Dispatches work to the current WinUI dispatcher queue.
/// </summary>
public sealed class DispatcherQueueUiDispatcher : IUiDispatcher
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    /// <summary>
    /// Enqueues an action to run on the UI thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _dispatcherQueue.TryEnqueue(() => action());
    }
}
