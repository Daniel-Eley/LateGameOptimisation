using System;

namespace LateGameOptimisation;

/// <summary>
/// Constant-memory statistics: retain three numbers instead of a list per frame.
/// No Unity dependencies, so this can be tested without the game.
/// Only the main thread owns this object; it is not thread safe.
/// </summary>
internal sealed class FrameTimeWindow
{
    public int Count { get; private set; }
    public double Total { get; private set; }
    public double Maximum { get; private set; }
    public double Mean => Count == 0 ? 0 : Total / Count;
    public double FramesPerSecond => Total == 0 ? 0 : Count * 1000.0 / Total;

    // True means a ten-second summary is ready. Keep long stalls: dropping
    // them would make a laggy game look better than it actually is.
    public bool Add(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds <= 0)
            return false;
        Count++;
        Total += milliseconds;
        Maximum = Math.Max(Maximum, milliseconds);
        return Total >= 10_000;
    }

    public void Reset()
    {
        Count = 0;
        Total = 0;
        Maximum = 0;
    }
}
