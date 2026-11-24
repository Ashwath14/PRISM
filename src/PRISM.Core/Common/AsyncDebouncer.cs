using System;
using System.Threading;
using System.Threading.Tasks;

namespace PRISM.Core.Common;

public class AsyncDebouncer : IDisposable
{
    private readonly int _delayMs;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();
    private bool _isDisposed;

    public AsyncDebouncer(int delayMs)
    {
        _delayMs = delayMs;
    }

    public void Debounce(Action action)
    {
        Debounce(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    public void Debounce(Func<Task> action)
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Delay(_delayMs, token).ContinueWith(async t =>
            {
                if (!t.IsCanceled && !token.IsCancellationRequested)
                {
                    try
                    {
                        await action();
                    }
                    catch
                    {
                        // Squelch background unhandled exceptions
                    }
                }
            }, token, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _isDisposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
