namespace RhodesSuki.Services;

public sealed class RhodesTournamentRemoteStateTracker
{
    private string _sessionId = "";
    private long _cursor;
    private bool _hasImported;

    public bool ShouldImport(RhodesTournamentRemoteStatus status)
    {
        if (!status.Active || string.IsNullOrWhiteSpace(status.SessionId))
            return false;

        return !_hasImported
            || !string.Equals(_sessionId, status.SessionId, StringComparison.Ordinal)
            || status.Cursor > _cursor;
    }

    public void MarkImported(RhodesTournamentRemoteStatus status)
    {
        if (!status.Active || string.IsNullOrWhiteSpace(status.SessionId))
            return;

        _sessionId = status.SessionId;
        _cursor = status.Cursor;
        _hasImported = true;
    }

    public void Reset()
    {
        _sessionId = "";
        _cursor = 0;
        _hasImported = false;
    }
}
