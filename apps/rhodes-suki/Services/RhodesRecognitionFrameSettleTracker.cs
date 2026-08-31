namespace RhodesSuki.Services;

public sealed class RhodesRecognitionFrameSettleTracker
{
    private const int EquivalentFingerprintDistance = 2;
    private readonly ulong _preSwipeFingerprint;
    private readonly int _stableSampleCount;
    private ulong _previousFingerprint;
    private int _equivalentTransitions;
    private int _viewportChangeObservations;

    public RhodesRecognitionFrameSettleTracker(
        ulong preSwipeFingerprint,
        int stableSampleCount = 2)
    {
        _preSwipeFingerprint = preSwipeFingerprint;
        _previousFingerprint = preSwipeFingerprint;
        _stableSampleCount = Math.Max(1, stableSampleCount);
    }

    public bool SawViewportChange { get; private set; }

    public bool Observe(ulong fingerprint)
    {
        if (RhodesRecognitionFrameFingerprint.Distance(fingerprint, _preSwipeFingerprint)
            > EquivalentFingerprintDistance)
        {
            _viewportChangeObservations++;
            if (_viewportChangeObservations >= 2)
                SawViewportChange = true;
        }
        else if (!SawViewportChange)
        {
            _viewportChangeObservations = 0;
        }

        if (RhodesRecognitionFrameFingerprint.Distance(fingerprint, _previousFingerprint)
            <= EquivalentFingerprintDistance)
        {
            _equivalentTransitions++;
        }
        else
        {
            _equivalentTransitions = 0;
        }

        _previousFingerprint = fingerprint;
        return _equivalentTransitions >= _stableSampleCount;
    }
}
