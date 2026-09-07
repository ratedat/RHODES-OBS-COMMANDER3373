namespace RhodesSuki.Services;

public sealed class RhodesTournamentRemoteStateTracker
{
    private string _sessionId = "";
    private long _position;
    private bool _hasImported;

    public bool ShouldImport(RhodesTournamentRemoteStatus status)
    {
        if (!status.Active || string.IsNullOrWhiteSpace(status.SessionId))
            return false;

        var position = Math.Max(status.Cursor, status.AppliedSequence);
        return !_hasImported
            || !string.Equals(_sessionId, status.SessionId, StringComparison.Ordinal)
            || position > _position;
    }

    public void MarkImported(RhodesTournamentRemoteStatus status)
    {
        if (!status.Active || string.IsNullOrWhiteSpace(status.SessionId))
            return;

        _sessionId = status.SessionId;
        _position = Math.Max(status.Cursor, status.AppliedSequence);
        _hasImported = true;
    }

    public void Reset()
    {
        _sessionId = "";
        _position = 0;
        _hasImported = false;
    }
}
