using System.Diagnostics;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.ModOptions;
using MelonLoader;
using LateGameOptimisation;

[assembly: MelonInfo(typeof(LateGameOptimisation.LateGameOptimisation), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6-Epic")]

namespace LateGameOptimisation;

public class LateGameOptimisation : BloonsTD6Mod
{
    // Exposed in Mod Helper's settings. Off by default: no periodic logging.
    public static readonly ModSettingBool EnableFrameTiming = new(false);
    private readonly FrameTimeWindow window = new();
    private long previousTimestamp;

    public override void OnApplicationStart()
    {
        ModHelper.Msg<LateGameOptimisation>("Diagnostics prototype loaded. No gameplay optimisations enabled.");
    }

    public override void OnUpdate()
    {
        // Main-thread callback intervals, NOT CPU timings of game methods.
        if (!EnableFrameTiming)
        {
            previousTimestamp = 0;
            window.Reset();
            return;
        }

        long now = Stopwatch.GetTimestamp();
        if (previousTimestamp != 0)
        {
            double milliseconds = (now - previousTimestamp) * 1000.0 / Stopwatch.Frequency;
            if (window.Add(milliseconds))
            {
                // One summary per ten seconds; no account or save data recorded.
                ModHelper.Msg<LateGameOptimisation>(
                    $"Frame intervals: {window.Count} samples, mean {window.Mean:F2} ms, " +
                    $"max {window.Maximum:F2} ms, {window.FramesPerSecond:F1} callbacks/s. " +
                    "Includes menus, pauses and loading; not simulation profiling.");
                window.Reset();
            }
        }
        previousTimestamp = now;
    }
}
