using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Il2CppAssets.Scripts.Simulation;

namespace LateGameOptimisation;

// Eight explicitly selected entry points, never a patch on every behavior.
// Inclusive timings can overlap across categories; do NOT sum them.
internal static class DetailedProfiler
{
    private const string Owner = "Daniel-Eley.LateGameOptimisation.DetailedProfiling";
    public const int ProbeCount = 8;
    private static readonly long[] ticks = new long[ProbeCount];
    private static readonly int[] calls = new int[ProbeCount];
    private static readonly int[] depth = new int[ProbeCount];
    private static int mainThread;
    public static int InstalledMask { get; private set; }
    public static string Status { get; private set; } = "disabled";
    public static int InstalledCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < ProbeCount; i++) if ((InstalledMask & (1 << i)) != 0) count++;
            return count;
        }
    }

    public static void Install()
    {
        mainThread = Environment.CurrentManagedThreadId;
        var harmony = new HarmonyLib.Harmony(Owner);
        string[] types = {
            "Il2CppAssets.Scripts.Simulation.Physics.CollisionChecker",
            "Il2CppAssets.Scripts.Simulation.Towers.Behaviors.Attack.Attack",
            "Il2CppAssets.Scripts.Simulation.Towers.Weapons.Weapon",
            "Il2CppAssets.Scripts.Simulation.Bloons.Bloon",
            "Il2CppAssets.Scripts.Simulation.Towers.Projectiles.Behaviors.TravelStrait",
            "Il2CppAssets.Scripts.Simulation.Towers.Projectiles.Behaviors.TrackTarget",
            "Il2CppAssets.Scripts.Simulation.Towers.Projectiles.Behaviors.Age",
            "Il2CppAssets.Scripts.Simulation.Simulation"
        };
        string[] methods = { "Process", "FindTarget", "Process", "Process", "Process", "Process", "Process", "FlushProcessList" };
        for (int i = 0; i < types.Length; i++)
        {
            MethodInfo? target = null;
            try
            {
                var type = typeof(Simulation).Assembly.GetType(types[i], true)!;
                foreach (var candidate in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (candidate.Name != methods[i]) continue;
                    if (target != null) throw new InvalidOperationException("Ambiguous method overloads");
                    target = candidate;
                }
                if (target == null) throw new MissingMethodException(types[i], methods[i]);
                harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(DetailedProfiler), "Before" + i),
                    finalizer: new HarmonyMethod(typeof(DetailedProfiler), "After" + i));
                InstalledMask |= 1 << i;
            }
            catch (Exception error)
            {
                if (target != null) harmony.Unpatch(target, HarmonyPatchType.All, Owner);
                BTD_Mod_Helper.ModHelper.Msg<LateGameOptimisation>($"Detailed profiler skipped {types[i]}.{methods[i]}: {error.Message}");
            }
        }
        Status = $"mask_{InstalledMask}";
    }

    private static long Begin(int index)
    {
        if (!SimulationProfiler.Recording || !SimulationProfiler.InSimulation || Environment.CurrentManagedThreadId != mainThread) return 0;
        return depth[index]++ == 0 ? Stopwatch.GetTimestamp() : -1;
    }

    private static void End(int index, long start)
    {
        if (start == 0) return;
        depth[index]--;
        if (start == -1) return;
        ticks[index] += Stopwatch.GetTimestamp() - start;
        calls[index]++;
    }

    // Dedicated wrappers avoid reflection/dictionary lookups during hot calls.
    private static void Before0(out long __state) => __state = Begin(0);
    private static void After0(long __state) => End(0, __state);
    private static void Before1(out long __state) => __state = Begin(1);
    private static void After1(long __state) => End(1, __state);
    private static void Before2(out long __state) => __state = Begin(2);
    private static void After2(long __state) => End(2, __state);
    private static void Before3(out long __state) => __state = Begin(3);
    private static void After3(long __state) => End(3, __state);
    private static void Before4(out long __state) => __state = Begin(4);
    private static void After4(long __state) => End(4, __state);
    private static void Before5(out long __state) => __state = Begin(5);
    private static void After5(long __state) => End(5, __state);
    private static void Before6(out long __state) => __state = Begin(6);
    private static void After6(long __state) => End(6, __state);
    private static void Before7(out long __state) => __state = Begin(7);
    private static void After7(long __state) => End(7, __state);

    public static DetailTimings Drain()
    {
        double scale = 1000.0 / Stopwatch.Frequency;
        var result = new DetailTimings(ticks[0] * scale, calls[0], ticks[1] * scale, calls[1],
            ticks[2] * scale, calls[2], ticks[3] * scale, calls[3],
            ticks[4] * scale, calls[4], ticks[5] * scale, calls[5],
            ticks[6] * scale, calls[6], ticks[7] * scale, calls[7]);
        Array.Clear(ticks, 0, ticks.Length);
        Array.Clear(calls, 0, calls.Length);
        return result;
    }
}
