namespace RhodesSuki.Services;

/// <summary>
/// Collapses bursts of save requests into the latest operation while preserving an awaitable flush path.
/// </summary>
public sealed class LatestAsyncOperationQueue : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<Task> _operation;
    private readonly TimeSpan _debounceDelay;
    private readonly Action<Exception>? _onError;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private TaskCompletionSource<bool> _progress = CreateProgressSource();
    private Task? _workerTask;
    private long _requestedVersion;
    private long _completedVersion;
    private long _failedVersion;
    private bool _disposed;

    public LatestAsyncOperationQueue(
        Func<Task> operation,
        TimeSpan debounceDelay,
        Action<Exception>? onError = null)
    {
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        _debounceDelay = debounceDelay < TimeSpan.Zero ? TimeSpan.Zero : debounceDelay;
        _onError = onError;
    }

    public void Request()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _requestedVersion++;
            EnsureWorkerLocked();
        }
    }

    public Task<bool> FlushAsync()
    {
        long targetVersion;
        lock (_gate)
        {
            if (_disposed)
                return Task.FromResult(false);

            targetVersion = ++_requestedVersion;
            EnsureWorkerLocked();
        }

        return WaitForVersionAsync(targetVersion);
    }

    public void Dispose()
    {
        TaskCompletionSource<bool> progress;
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            progress = _progress;
            _progress = CreateProgressSource();
        }

        _disposeCancellation.Cancel();
        progress.TrySetResult(true);
    }

    private void EnsureWorkerLocked()
    {
        if (_workerTask is null || _workerTask.IsCompleted)
            _workerTask = Task.Run(ProcessAsync);
    }

    private async Task ProcessAsync()
    {
        var applyDebounce = true;
        try
        {
            while (true)
            {
                if (applyDebounce && _debounceDelay > TimeSpan.Zero)
                    await Task.Delay(_debounceDelay, _disposeCancellation.Token).ConfigureAwait(false);
                applyDebounce = false;

                long targetVersion;
                lock (_gate)
                {
                    if (_disposed)
                    {
                        _workerTask = null;
                        return;
                    }

                    // Include every request that arrived during the debounce window.
                    targetVersion = _requestedVersion;
                }

                Exception? error = null;
                try
                {
                    await _operation().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    error = ex;
                    try
                    {
                        _onError?.Invoke(ex);
                    }
                    catch
                    {
                        // An error reporter must not stop later save requests.
                    }
                }

                TaskCompletionSource<bool> progress;
                bool stopWorker;
                lock (_gate)
                {
                    if (error is null)
                        _completedVersion = Math.Max(_completedVersion, targetVersion);
                    else
                        _failedVersion = Math.Max(_failedVersion, targetVersion);

                    progress = _progress;
                    _progress = CreateProgressSource();

                    stopWorker = _disposed || _requestedVersion <= targetVersion;
                    if (stopWorker)
                        _workerTask = null;
                }

                progress.TrySetResult(true);
                if (stopWorker)
                    return;
            }
        }
        catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
        {
            TaskCompletionSource<bool> progress;
            lock (_gate)
            {
                _workerTask = null;
                progress = _progress;
                _progress = CreateProgressSource();
            }
            progress.TrySetResult(true);
        }
    }

    private async Task<bool> WaitForVersionAsync(long targetVersion)
    {
        while (true)
        {
            Task progress;
            lock (_gate)
            {
                if (_completedVersion >= targetVersion)
                    return true;
                if (_failedVersion >= targetVersion || _disposed)
                    return false;

                progress = _progress.Task;
            }

            await progress.ConfigureAwait(false);
        }
    }

    private static TaskCompletionSource<bool> CreateProgressSource()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
