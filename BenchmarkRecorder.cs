using System;
using System.Globalization;
using System.IO;

namespace LateGameOptimisation;

/// <summary>
/// Owns a bounded buffer of frame intervals. No game objects or disk access
/// during collection; the file is written and closed only after Stop.
/// All methods are called on the game thread.
/// </summary>
internal sealed class BenchmarkRecorder
{
    public const int Capacity = 120_000; // Fixed-size buffers; no per-frame growth.
    public const double DurationLimitMs = 600_000;
    private readonly double[] intervals = new double[Capacity];
    private readonly string[] states = new string[Capacity];
    private readonly double[] simulationMs = new double[Capacity];
    private readonly int[] simulationCalls = new int[Capacity];
    private readonly DetailTimings[] details = new DetailTimings[Capacity];
    public int DetailedMask { get; set; }
    private readonly string[] events = new string[4096];
    private readonly double[] eventTimes = new double[4096];
    private int eventCount;
    private long started;
    public bool ProfileEnabled { get; set; }

    public void Mark(string name)
    {
        if (!IsRecording || eventCount == events.Length) return;
        if (eventCount == events.Length - 1) name = "event_capacity_reached";
        events[eventCount] = name;
        eventTimes[eventCount++] = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
    }
    public bool IsRecording { get; private set; }
    public bool HasPendingSave { get; private set; }
    public int Count { get; private set; }
    public double ElapsedMs { get; private set; }

    public bool Start()
    {
        // Never overwrite an unsaved run after a disk error.
        if (IsRecording || HasPendingSave) return false;
        Count = 0;
        ElapsedMs = 0;
        eventCount = 0;
        started = System.Diagnostics.Stopwatch.GetTimestamp();
        IsRecording = true;
        Mark("recording_start");
        return true;
    }

    public bool Add(double milliseconds, string state = "unknown", double simulationMilliseconds = 0, int calls = 0, DetailTimings detail = default)
    {
        if (!IsRecording || !double.IsFinite(milliseconds) || milliseconds <= 0) return false;
        states[Count] = state;
        details[Count] = detail;
        simulationMs[Count] = simulationMilliseconds;
        simulationCalls[Count] = calls;
        intervals[Count++] = milliseconds;
        ElapsedMs += milliseconds;
        if (Count < Capacity && ElapsedMs < DurationLimitMs) return false;
        Stop(Count == Capacity ? "sample_limit" : "duration_limit");
        return true; // Caller saves after the bounded recording finishes.
    }

    public void Stop(string reason = "manual_stop")
    {
        if (!IsRecording) return;
        Mark(reason);
        IsRecording = false;
        HasPendingSave = true;
    }

    public string Save(string directory)
    {
        if (IsRecording || !HasPendingSave) throw new InvalidOperationException("Stop a recording before saving.");
        Directory.CreateDirectory(directory);
        // Unique names plus CreateNew prevent overwriting previous runs.
        string path = Path.Combine(directory, $"benchmark-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.csv");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream))
        {
            writer.WriteLine("record_type,sample,elapsed_ms,frame_interval_ms,state,event,simulation_ms,simulation_calls,profiling_enabled,detailed_mask,collision_ms,collision_calls,target_ms,target_calls,weapon_ms,weapon_calls,bloon_ms,bloon_calls");
            double elapsed = 0;
            for (int i = 0; i < Count; i++)
            {
                elapsed += intervals[i];
                // Invariant formatting keeps decimal points valid in CSV on
                // computers whose regional settings normally use commas.
                var d = details[i];
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "frame,{0},{1:R},{2:R},{3},,{4:R},{5},{6},{7},{8:R},{9},{10:R},{11},{12:R},{13},{14:R},{15}", i + 1, elapsed, intervals[i], states[i], simulationMs[i], simulationCalls[i], ProfileEnabled ? 1 : 0, DetailedMask, d.CollisionMs, d.CollisionCalls, d.TargetMs, d.TargetCalls, d.WeaponMs, d.WeaponCalls, d.BloonMs, d.BloonCalls));
            }
            // Event rows follow frame rows; sort by elapsed_ms for a timeline.
            for (int i = 0; i < eventCount; i++)
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "event,,{0:R},,,{1},,,{2},{3},,,,,,,,", eventTimes[i], events[i], ProfileEnabled ? 1 : 0, DetailedMask));
        }
        // Includes successful flush/close. A failed write keeps the buffer so
        // Stop and Save can retry. Any partial file is not a completed benchmark.
        HasPendingSave = false;
        return path;
    }
}
