using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using Il2CppAssets.Scripts.Simulation.Bloons;
using Il2CppAssets.Scripts.Simulation.Physics;
using Il2CppAssets.Scripts.Simulation.Towers.Projectiles;

namespace LateGameOptimisation;

// Separate default-off experiment. We intentionally do NOT hook shared generic
// MoveNext code: a typed wrapper alone does not prove bloon-only native coverage.
internal static class CollisionProfiler
{
    private const string Owner = "Daniel-Eley.LateGameOptimisation.CollisionProfiling";
    private static readonly CollisionTimingAccumulator timings = new();
    private static int mainThread;
    public static bool Installed { get; private set; }
    public static string Status { get; private set; } = "off";

    public static void Install()
    {
        var harmony = new HarmonyLib.Harmony(Owner);
        MethodInfo[] targets = Array.Empty<MethodInfo>();
        try
        {
            // The interpretation depends on the inspected native implementation.
            // Refuse other builds before adding any hooks. Hashing is local and
            // occurs only once at startup when this experiment is requested.
            using var process = Process.GetCurrentProcess();
            string root = Path.GetDirectoryName(process.MainModule!.FileName)!;
            VerifyHash(Path.Combine(root, "GameAssembly.dll"),
                "CAF18ADDE5B26658F65A8D040413624886FF90BB15C789A29DED4736F6E6689C");
            VerifyHash(typeof(Projectile).Assembly.Location,
                "B420556C3BD1EEE3CDBDBD1B5CA0F53FE0E327619614D60E3ADDE9838FDA825B");

            // Exact overloads: the IEnumerable overload has a different path.
            targets = new[] {
                Required(typeof(CollisionChecker), "Process", typeof(int)),
                Required(typeof(Projectile), "CollideBloons", typeof(CollisionGrid<Bloon>.CollisionState)),
                Required(typeof(Projectile), "CollideBloon", typeof(Bloon), typeof(bool))
            };
            mainThread = Environment.CurrentManagedThreadId;
            for (int i = 0; i < targets.Length; i++)
                harmony.Patch(targets[i],
                    prefix: new HarmonyMethod(typeof(CollisionProfiler), "Before" + i),
                    finalizer: new HarmonyMethod(typeof(CollisionProfiler), "After" + i));
            Installed = true;
            Status = "3/3";
        }
        catch (Exception error)
        {
            // Partial measurements would make subtraction misleading. Disable
            // collection and remove only this owner's patches, including a
            // potentially partially-installed failing target.
            Installed = false;
            Status = "unavailable";
            foreach (var target in targets)
            {
                try { harmony.Unpatch(target, HarmonyPatchType.All, Owner); }
                catch (Exception cleanup) { BTD_Mod_Helper.ModHelper.Msg<LateGameOptimisation>($"Collision profiler cleanup failed; restart with option off: {cleanup.Message}"); }
            }
            BTD_Mod_Helper.ModHelper.Msg<LateGameOptimisation>($"Collision profiling disabled: {error.Message}");
        }
    }

    private static MethodInfo Required(Type type, string name, params Type[] parameters)
        => type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            null, parameters, null) ?? throw new MissingMethodException(type.FullName, name);

    private static void VerifyHash(string path, string expected)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        if (Convert.ToHexString(sha.ComputeHash(stream)) != expected)
            throw new InvalidOperationException("Unreviewed binary: " + Path.GetFileName(path));
    }

    private static bool Collect => Installed && SimulationProfiler.Recording &&
        SimulationProfiler.InSimulation && Environment.CurrentManagedThreadId == mainThread;

    // No original arguments, results, instances, or exceptions are injected or
    // changed. Void finalizers preserve exceptions and balance timers on unwind.
    private static void Before0(out long __state) => __state = Collect ? timings.BeginParent(Stopwatch.GetTimestamp()) : 0;
    private static void After0(long __state) { if (__state != 0) timings.EndParent(__state, Stopwatch.GetTimestamp()); }
    private static void Before1(out long __state) => __state = Collect ? timings.BeginLoop(Stopwatch.GetTimestamp()) : 0;
    private static void After1(long __state) { if (__state != 0) timings.EndLoop(__state, Stopwatch.GetTimestamp()); }
    private static void Before2(out long __state) => __state = Collect ? timings.BeginHit(Stopwatch.GetTimestamp()) : 0;
    private static void After2(long __state) { if (__state != 0) timings.EndHit(__state, Stopwatch.GetTimestamp()); }

    public static CollisionTimings Drain() => timings.Drain(1000.0 / Stopwatch.Frequency);
}
