using System;

namespace GatTelemetry;

// A new grace period requires 60 seconds of continuously valid road limits.
// Short valid bursts apply immediately but cannot replenish the missing-limit budget.
internal sealed class RoadLimitState
{
    private double _last = double.NaN;
    private double _missingSeconds;
    private double _validSeconds;
    private double? _previousTime;
    private bool _previousValid;

    public double Resolve(bool connected, double raw, double seconds, out bool preserved)
    {
        preserved = false;
        if (!connected)
        {
            _last = double.NaN;
            _missingSeconds = _validSeconds = 0;
            _previousTime = null;
            return 0;
        }
        double elapsed = _previousTime.HasValue ? Math.Max(0, seconds - _previousTime.Value) : 0;
        if (_previousTime.HasValue)
        {
            if (_previousValid) _validSeconds += elapsed;
            else _missingSeconds += elapsed;
        }
        bool valid = !double.IsNaN(raw) && !double.IsInfinity(raw) && raw > 0 && raw <= 250;
        if (_previousValid && _validSeconds >= 60) _missingSeconds = 0;
        if (valid)
        {
            if (double.IsNaN(_last)) _missingSeconds = 0;
            if (!_previousValid) _validSeconds = 0;
            _last = raw;
        }
        else _validSeconds = 0;
        _previousValid = valid;
        _previousTime = seconds;
        preserved = !valid && !double.IsNaN(_last) && _missingSeconds < 60;
        return valid ? raw : (preserved ? _last : 60);
    }
}
