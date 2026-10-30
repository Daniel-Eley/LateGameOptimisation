using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Il2CppAssets.Scripts.Simulation;

namespace LateGameOptimisation;

// Inclusive elapsed wall time, not CPU time. The original method always runs.
// Only the main thread is measured; background work is outside this scope.
internal static class SimulationProfiler
{
    private static int mainThread;
    private static long ticks;
    private static int calls;
    [ThreadStatic] private static int depth;
    public static bool Installed { get; private set; }
    public static bool InSimulation => depth > 0;
    public static bool Recording { get; set; }

    public static void Install()
    {
        mainThread = Environment.CurrentManagedThreadId;
        MethodInfo? target = null;
        foreach (var method in typeof(Simulation).GetMethods())
        {
            if (method.Name != "Simulate") continue;
            if (target != null) throw new InvalidOperationException("Ambiguous Simulate overloads; profiling disabled.");
            target = method;
        }
        if (target == null) throw new MissingMethodException("Simulation.Simulate unavailable.");
        new HarmonyLib.Harmony("Daniel-Eley.LateGameOptimisation.Profiling").Patch(target,
            prefix: new HarmonyMethod(typeof(SimulationProfiler), nameof(Before)),
            finalizer: new HarmonyMethod(typeof(SimulationProfiler), nameof(After)));
        Installed = true;
    }

    private static void Before(out long __state)
    {
        __state = 0;
        if (!Recording || Environment.CurrentManagedThreadId != mainThread) return;
        // Count outer calls only: recursive inclusive timings must not be added twice.
        __state = depth++ == 0 ? Stopwatch.GetTimestamp() : -1;
    }

    // A void finalizer observes timing without replacing/suppressing exceptions.
    private static void After(long __state)
    {
        if (__state == 0) return;
        depth--;
        if (__state == -1) return;
        ticks += Stopwatch.GetTimestamp() - __state;
        calls++;
    }

    public static (double Milliseconds, int Calls) Drain()
    {
        var result = (ticks * 1000.0 / Stopwatch.Frequency, calls);
        ticks = 0;
        calls = 0;
        return result;
    }
}
