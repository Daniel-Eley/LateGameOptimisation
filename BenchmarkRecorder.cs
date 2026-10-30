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
    public const int Capacity = 120_000; // Under 1 MB of doubles.
    public const double DurationLimitMs = 600_000;
    private readonly double[] intervals = new double[Capacity];
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
        IsRecording = true;
        return true;
    }

    public bool Add(double milliseconds)
    {
        if (!IsRecording || !double.IsFinite(milliseconds) || milliseconds <= 0) return false;
        intervals[Count++] = milliseconds;
        ElapsedMs += milliseconds;
        if (Count < Capacity && ElapsedMs < DurationLimitMs) return false;
        Stop();
        return true; // Caller saves after the bounded recording finishes.
    }

    public void Stop()
    {
        if (!IsRecording) return;
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
            writer.WriteLine("sample,elapsed_ms,frame_interval_ms");
            double elapsed = 0;
            for (int i = 0; i < Count; i++)
            {
                elapsed += intervals[i];
                // Invariant formatting keeps decimal points valid in CSV on
                // computers whose regional settings normally use commas.
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:R},{2:R}", i + 1, elapsed, intervals[i]));
            }
        }
        // Includes successful flush/close. A failed write keeps the buffer so
        // Stop and Save can retry. Any partial file is not a completed benchmark.
        HasPendingSave = false;
        return path;
    }
}
