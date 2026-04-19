namespace AiDev.WinUI.ViewModels;

public interface IUiDispatcher
{
    void Enqueue(Action action);
}

public sealed class DispatcherQueueUiDispatcher : IUiDispatcher
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _dispatcherQueue.TryEnqueue(() => action());
    }
}
