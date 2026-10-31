namespace LateGameOptimisation;

// These are elapsed times, not CPU times or successful-hit counts. The remainder
// includes loop/interop overhead as well as searching; it is NOT pure MoveNext time.
internal readonly record struct CollisionTimings(
    double ParentMs, int ParentCalls, double LoopMs, int LoopCalls,
    double HitMs, int HitCalls, double LoopRemainderMs);

/// <summary>
/// Main-thread, game-independent accounting for three nested timing scopes.
/// Clock ticks are supplied by the caller, so tests can exercise exact nesting.
/// No game objects, lists of candidates, or per-call allocations are stored here.
/// </summary>
internal sealed class CollisionTimingAccumulator
{
    private int parentDepth, loopDepth, hitDepth;
    private long parentTicks, loopTicks, hitTicks, remainderTicks;
    private int parentCalls, loopCalls, hitCalls;
    private long currentLoopHitTicks;
    private int currentLoopHitCalls;

    // 0 = ignored; -1 = nested (already inside an inclusive timer); >0 = start.
    public long BeginParent(long now) => parentDepth++ == 0 ? now : -1;

    public long BeginLoop(long now)
    {
        if (parentDepth == 0) return 0;
        if (loopDepth++ != 0) return -1;
        currentLoopHitTicks = 0;
        currentLoopHitCalls = 0;
        return now;
    }

    public long BeginHit(long now)
    {
        // Only CollideBloon calls inside the selected grid-iterator loop count.
        // Calls from explosions or other paths outside it are deliberately excluded.
        if (loopDepth == 0) return 0;
        return hitDepth++ == 0 ? now : -1;
    }

    public void EndParent(long start, long now)
    {
        if (start == 0) return;
        parentDepth--;
        if (start == -1) return;
        parentTicks += now - start;
        parentCalls++;
    }

    public void EndLoop(long start, long now)
    {
        if (start == 0) return;
        loopDepth--;
        if (start == -1) return;
        long elapsed = now - start;
        loopTicks += elapsed;
        loopCalls++;
        // Commit child counters together with their enclosing loop. Draining
        // completed scopes mid-call cannot separate a loop from its child totals.
        hitTicks += currentLoopHitTicks;
        hitCalls += currentLoopHitCalls;
        remainderTicks += elapsed - currentLoopHitTicks;
    }

    public void EndHit(long start, long now)
    {
        if (start == 0) return;
        hitDepth--;
        if (start == -1) return;
        currentLoopHitTicks += now - start;
        currentLoopHitCalls++;
    }

    public CollisionTimings Drain(double millisecondsPerTick)
    {
        var result = new CollisionTimings(parentTicks * millisecondsPerTick, parentCalls,
            loopTicks * millisecondsPerTick, loopCalls, hitTicks * millisecondsPerTick,
            hitCalls, remainderTicks * millisecondsPerTick);
        parentTicks = loopTicks = hitTicks = remainderTicks = 0;
        parentCalls = loopCalls = hitCalls = 0;
        // Do not reset live depths or current-loop counters: a lifecycle callback
        // may drain completed measurements before an enclosing method returns.
        return result;
    }
}
