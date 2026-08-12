namespace RhodesSuki.Services;

public sealed class RhodesDeferredRefreshGate
{
    private readonly object _sync = new();
    private int _deferralDepth;
    private bool _pending;

    public IDisposable Defer()
    {
        lock (_sync)
            _deferralDepth++;
        return new Scope(this);
    }

    public bool Request()
    {
        lock (_sync)
        {
            if (_deferralDepth == 0)
            {
                _pending = false;
                return true;
            }

            _pending = true;
            return false;
        }
    }

    public bool Flush()
    {
        lock (_sync)
        {
            if (!_pending)
                return false;
            _pending = false;
            return true;
        }
    }

    private void Release()
    {
        lock (_sync)
            _deferralDepth = Math.Max(0, _deferralDepth - 1);
    }

    private sealed class Scope(RhodesDeferredRefreshGate owner) : IDisposable
    {
        private RhodesDeferredRefreshGate? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release();
        }
    }
}
